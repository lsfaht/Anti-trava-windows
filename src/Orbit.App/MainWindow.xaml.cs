using System.ComponentModel;
using System.Diagnostics;
using KeepAwake.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using KeepAwakeViewModel = Orbit.Application.KeepAwakeViewModel;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using Orbit.Application;

namespace Orbit.App;

public sealed partial class MainWindow : Window
{
    private readonly KeepAwakeViewModel _keepAwake;
    private readonly IUserTempAnalysisService _temp = new OrbitUserTempAnalysisService();
    private readonly IMaintenanceHistoryStore _history = new OrbitHistoryStore();
    private TextBlock? _dashboardPowerStatus, _dashboardPresentationStatus;
    private TextBlock? _powerStatusText, _presentationStatusText, _keepAwakeMessageText;
    private Button? _powerToggleButton, _presentationToggleButton;
    private Button? _exportDiagnosticButton;
    private readonly IntPtr _windowHandle;
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.ContextMenuStrip _trayMenu;
    private readonly System.Windows.Forms.ToolStripMenuItem _trayOpen, _trayStart, _trayStop, _trayPresentation, _trayExit;
    private readonly System.Drawing.Icon _trayImage;
    private readonly DiagnosticJournal _journal;
    private readonly OrbitLifetime _lifetime;
    private WindowsSessionObserver? _sessionObserver;
    private readonly DispatcherTimer _heartbeat = new() { Interval = TimeSpan.FromSeconds(30) };
    private TextBlock? _diagnosticState;
    private TextBlock? _verificationText;
    private bool _trayDisposed;
    private CancellationTokenSource? _analysisCancellation;
    private string _activePage = "Dashboard";

    public MainWindow(DiagnosticJournal journal, IExecutionStateApi? executionApi = null, Func<PresentationModeRequest>? presentationFactory = null)
    {
        _journal = journal;
        InitializeComponent();
        Title = "Orbit";
        _keepAwake = new KeepAwakeViewModel(executionApi ?? new WindowsExecutionStateApi(), presentationFactory ?? (() => new PresentationModeRequest(new WindowsPresentationPowerApi())));
        _lifetime = new OrbitLifetime(_keepAwake, journal);
        _keepAwake.DiagnosticEvent += (name, error) =>
        {
            UpdateSnapshot();
            if (error is null) _journal.Record(name, _lifetime.Snapshot);
            else _journal.RecordException(name, error, _lifetime.Snapshot);
        };
        _windowHandle = WindowNative.GetWindowHandle(this);
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_windowHandle));
        appWindow.Closing += (_, args) =>
        {
            if (_lifetime.ExitRequested) return;
            args.Cancel = true;
            HideToTray();
        };
        appWindow.Changed += (_, args) =>
        {
            if (args.DidSizeChange && IsIconic(_windowHandle)) HideToTray();
        };
        _keepAwake.PropertyChanged += KeepAwake_PropertyChanged;
        _trayMenu = new System.Windows.Forms.ContextMenuStrip();
        _trayOpen = new System.Windows.Forms.ToolStripMenuItem("Abrir janela", null, (_, _) => RestoreFromTray());
        _trayStart = new System.Windows.Forms.ToolStripMenuItem("Iniciar manutenção de energia", null, (_, _) => SetPowerState(true));
        _trayStop = new System.Windows.Forms.ToolStripMenuItem("Interromper manutenção de energia", null, (_, _) => SetPowerState(false));
        _trayPresentation = new System.Windows.Forms.ToolStripMenuItem("Ativar modo de apresentação", null, (_, _) => { _keepAwake.TogglePresentation(); SyncTrayMenu(); });
        _trayExit = new System.Windows.Forms.ToolStripMenuItem("Sair", null, (_, _) => ExitApplication());
        _trayMenu.Items.AddRange([_trayOpen, new System.Windows.Forms.ToolStripSeparator(), _trayStart, _trayStop, _trayPresentation, new System.Windows.Forms.ToolStripSeparator(), _trayExit]);
        using (var iconStream = typeof(MainWindow).Assembly.GetManifestResourceStream("Orbit.App.Assets.Orbit.ico") ?? throw new InvalidOperationException("Recurso do ícone Orbit ausente."))
            _trayImage = new System.Drawing.Icon(iconStream);
        _trayIcon = new System.Windows.Forms.NotifyIcon { Icon = _trayImage, Text = "Orbit — Inativo", ContextMenuStrip = _trayMenu, Visible = true };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        Navigation.SelectedItem = Navigation.MenuItems[0];
        ShowPage("Dashboard");
        Closed += (_, _) =>
        {
            _heartbeat.Stop();
            _keepAwake.PropertyChanged -= KeepAwake_PropertyChanged;
            var succeeded = true;
            foreach (var cleanup in new Action[] { () => _analysisCancellation?.Cancel(), () => _sessionObserver?.Dispose(), DisposeTray })
                try { cleanup(); } catch (Exception ex) { succeeded = false; _journal.RecordException("host_cleanup_error", ex, _lifetime.Snapshot); }
            _lifetime.Finish(succeeded);
            UpdateSnapshot();
        };
        try
        {
            _sessionObserver = new WindowsSessionObserver(observed =>
            {
                _lifetime.Observe(observed);
                UpdateSnapshot();
            }, (name, error) => _journal.RecordException(name, error, _lifetime.Snapshot));
            _journal.Record("observer_started", _lifetime.Snapshot, $"SessionNotifications={_sessionObserver.SessionNotificationsAvailable}; PowerNotifications={_sessionObserver.PowerNotificationsAvailable}");
        }
        catch (Exception ex) { _journal.RecordException("observer_unavailable", ex, _lifetime.Snapshot); }
        _heartbeat.Tick += (_, _) => { UpdateSnapshot(); _journal.Heartbeat(_lifetime.Snapshot); };
        _heartbeat.Start();
        UpdateSnapshot();
        Log("Orbit iniciado.");
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string page) ShowPage(page);
    }

    private void ShowPage(string key)
    {
        _activePage = key;
        var (title, view) = key switch
        {
            "Dashboard" => ("Início", BuildDashboard()),
            "KeepAwake" => ("Keep Awake", BuildKeepAwake()),
            "Maintenance" => ("Manutenção", BuildMaintenance()),
            "Diagnostics" => ("Diagnóstico", BuildDiagnostics()),
            "History" => ("Histórico", BuildHistory()),
            _ => ("Início", BuildDashboard())
        };
        PageHeading.Text = title;
        ContentFrame.Content = view;
    }

    private UIElement BuildDashboard()
    {
        var stack = PageStack();
        stack.Children.Add(Eyebrow("AMBIENTE DE TRABALHO"));
        stack.Children.Add(Heading("Seu espaço de trabalho, em órbita."));
        stack.Children.Add(Body("Acompanhe a sessão, mantenha o sistema disponível e consulte diagnósticos em um só lugar."));
        var status = Card();
        var row = new Grid { ColumnSpacing = 24 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(StatusTile("KEEP AWAKE", _keepAwake.Status, "Solicitação de energia do Windows"));
        var presentation = StatusTile("MODO DE APRESENTAÇÃO", _keepAwake.PresentationStatus, "Controle separado e opcional");
        Grid.SetColumn(presentation, 1); row.Children.Add(presentation); status.Child = row; stack.Children.Add(status);
        var quick = Card(); var quickStack = new StackPanel { Spacing = 12 };
        quickStack.Children.Add(Heading("Ações rápidas", 20));
        quickStack.Children.Add(ActionButton(_keepAwake.ToggleLabel, (_, _) => _keepAwake.TogglePower()));
        quickStack.Children.Add(ActionButton("Abrir diagnóstico", (_, _) => SelectPage("Diagnostics"), secondary: true));
        quick.Child = quickStack; stack.Children.Add(quick);
        stack.Children.Add(Body("A interface Orbit está em homologação. O host Windows Keep Awake legado e seu instalador permanecem disponíveis durante a migração."));
        stack.Children.Add(Body(_journal.PreviousRunNotice));
        return Scroll(stack);
    }

    private UIElement BuildKeepAwake()
    {
        var stack = PageStack();
        stack.Children.Add(Eyebrow("ENERGIA DO WINDOWS"));
        stack.Children.Add(Heading("Keep Awake"));
        stack.Children.Add(Body("Solicita que o Windows mantenha o sistema ativo. Isso não simula teclado ou mouse e não garante a presença do Google Chat."));
        _verificationText = Body(_keepAwake.VerificationNotice); stack.Children.Add(_verificationText);
        var card = Card(); var content = new StackPanel { Spacing = 16 };
        content.Children.Add(Eyebrow("ESTADO DA SOLICITAÇÃO"));
        _powerStatusText = new TextBlock { Text = _keepAwake.Status, FontSize = 30, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush("OrbitTextBrush") };
        content.Children.Add(_powerStatusText);
        _powerToggleButton = ActionButton(_keepAwake.ToggleLabel, (_, _) => _keepAwake.TogglePower()); content.Children.Add(_powerToggleButton);
        var displayOption = new CheckBox { Content = "Manter também a tela ligada", IsChecked = _keepAwake.KeepDisplayOn, Foreground = Brush("OrbitTextBrush") };
        displayOption.Checked += (_, _) => _keepAwake.KeepDisplayOn = true;
        displayOption.Unchecked += (_, _) => _keepAwake.KeepDisplayOn = false;
        content.Children.Add(displayOption);
        var divider = new Border { Height = 1, Background = Brush("OrbitSurfaceAltBrush"), Margin = new Thickness(0, 4, 0, 4) }; content.Children.Add(divider);
        content.Children.Add(Eyebrow("MODO DE APRESENTAÇÃO"));
        content.Children.Add(Body("Usa solicitações oficiais de energia do Windows. Organizações podem aplicar políticas adicionais de bloqueio."));
        _presentationToggleButton = ActionButton(_keepAwake.PresentationLabel, (_, _) => _keepAwake.TogglePresentation(), secondary: true); content.Children.Add(_presentationToggleButton);
        _presentationStatusText = new TextBlock { Text = "Estado: " + _keepAwake.PresentationStatus, Foreground = Brush("OrbitMutedBrush") }; content.Children.Add(_presentationStatusText);
        card.Child = content; stack.Children.Add(card);
        var notice = Card(); _keepAwakeMessageText = Body(_keepAwake.Message); notice.Child = _keepAwakeMessageText; stack.Children.Add(notice);
        return Scroll(stack);
    }

    private UIElement BuildMaintenance()
    {
        var stack = PageStack();
        stack.Children.Add(Eyebrow("CENTRAL DE MANUTENÇÃO"));
        stack.Children.Add(Heading("Análise e operações disponíveis"));
        stack.Children.Add(Body("Somente a análise da TEMP do usuário está habilitada. Esta operação é somente leitura; nenhuma limpeza de arquivos é executada."));
        var search = new TextBox { PlaceholderText = "Buscar por nome, categoria ou descrição", MinWidth = 240, MaxWidth = 620 };
        var category = new ComboBox { Width = 230, PlaceholderText = "Todas as categorias" };
        category.Items.Add("Todas as categorias"); category.Items.Add("Limpeza básica"); category.Items.Add("Aplicativos"); category.Items.Add("Windows"); category.Items.Add("Diagnóstico"); category.SelectedIndex = 0;
        var filters = new Grid { ColumnSpacing = 12 }; filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }); filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filters.Children.Add(search); Grid.SetColumn(category, 1); filters.Children.Add(category); stack.Children.Add(filters);
        var split = new Grid { ColumnSpacing = 16 }; split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); split.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var catalogCard = Card(); var catalogPanel = new StackPanel { Spacing = 10 }; catalogPanel.Children.Add(Heading("Catálogo", 18));
        var catalog = new ListView { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 360, MinHeight = 200, Background = Brush("OrbitSurfaceBrush") }; catalog.DisplayMemberPath = nameof(MaintenanceOperationDescriptor.Name); catalogPanel.Children.Add(catalog);
        var catalogEmpty = Body("Nenhuma operação corresponde à busca."); catalogEmpty.Visibility = Visibility.Collapsed; catalogPanel.Children.Add(catalogEmpty);
        catalogCard.Child = catalogPanel; split.Children.Add(catalogCard);
        var detailsCard = Card(); var details = new StackPanel { Spacing = 12 }; details.Children.Add(Heading("Detalhes", 18));
        var detailText = Body("Selecione uma operação para ver detalhes."); details.Children.Add(detailText);
        var analyze = ActionButton("Analisar TEMP do usuário", (_, _) => { }); analyze.IsEnabled = false; details.Children.Add(analyze);
        var cancel = ActionButton("Cancelar análise", (_, _) => _analysisCancellation?.Cancel(), secondary: true); cancel.IsEnabled = false; details.Children.Add(cancel);
        var progressText = Body("Nenhuma análise executada."); details.Children.Add(progressText);
        detailsCard.Child = details; Grid.SetColumn(detailsCard, 1); split.Children.Add(detailsCard); stack.Children.Add(split);
        analyze.Click += async (_, _) => await AnalyzeTempAsync();
        void RefreshCatalog()
        {
            var term = search.Text.Trim(); var cat = category.SelectedIndex;
            var filtered = MaintenanceCatalog.Operations.Where(op =>
                (cat == 0 || (int)op.Category == cat - 1) && (term.Length == 0 || op.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || op.Description.Contains(term, StringComparison.OrdinalIgnoreCase) || op.Category.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            catalog.ItemsSource = filtered;
            catalogEmpty.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        search.TextChanged += (_, _) => RefreshCatalog(); category.SelectionChanged += (_, _) => RefreshCatalog();
        catalog.SelectionChanged += (_, _) =>
        {
            if (catalog.SelectedItem is not MaintenanceOperationDescriptor operation) return;
            detailText.Text = $"{operation.Name}\n\n{operation.Description}\n\nCategoria: {CategoryName(operation.Category)}  ·  Risco: {RiskName(operation.Risk)}\nEstado: {(operation.CanAnalyze ? "Disponível para análise" : "Planejada")}\nRequer administrador: {(operation.RequiresAdministrator ? "Sim" : "Não")}";
            analyze.IsEnabled = operation.CanAnalyze && _analysisCancellation is null;
        };
        RefreshCatalog();
        return Scroll(stack);

        async Task AnalyzeTempAsync()
        {
            _analysisCancellation = new CancellationTokenSource(); analyze.IsEnabled = false; cancel.IsEnabled = true; progressText.Text = "Analisando somente a estrutura e os tamanhos...";
            try
            {
                var progress = new Progress<MaintenanceAnalysisProgress>(p => progressText.Text = $"Arquivos: {p.FilesIdentified:N0}  ·  Tamanho potencial: {FormatBytes(p.PotentialBytes)}  ·  Pastas: {p.DirectoriesVisited:N0}");
                var result = await _temp.AnalyzeAsync(progress, _analysisCancellation.Token);
                _history.Add(MaintenanceHistoryEntryFactory.FromResult(result));
                progressText.Text = $"{AnalysisName(result.Status)}\n{result.FilesIdentified:N0} arquivos  ·  {FormatBytes(result.PotentialBytes)} potencialmente recuperáveis  ·  {result.Failures:N0} falhas  ·  {result.ReparsePointsSkipped:N0} links ignorados\n{_temp.TargetDirectory}";
                Log($"Análise TEMP: {result.Status}; arquivos={result.FilesIdentified}; falhas={result.Failures}.");
            }
            catch (Exception ex) { progressText.Text = "Falha na análise: " + ex.Message; _journal.RecordException("analysis_failed", ex, _lifetime.Snapshot); }
            finally { _analysisCancellation.Dispose(); _analysisCancellation = null; cancel.IsEnabled = false; if (catalog.SelectedItem is MaintenanceOperationDescriptor current) analyze.IsEnabled = current.CanAnalyze; }
        }
    }

    private UIElement BuildDiagnostics()
    {
        var stack = PageStack(); stack.Children.Add(Eyebrow("DIAGNÓSTICO LOCAL")); stack.Children.Add(Heading("Janelas e sessão"));
        stack.Children.Add(Body(_journal.PreviousRunNotice));
        _diagnosticState = Body(DiagnosticStateText()); stack.Children.Add(_diagnosticState);
        var exportStatus = Body("O relatório inclui somente eventos técnicos do Orbit e consultas numéricas de configuração; não inclui a lista de janelas.");
        _exportDiagnosticButton = ActionButton("Exportar diagnóstico", (_, _) =>
        {
            try
            {
                var folder = _journal.ReportDirectory;
                var path = _journal.Export(folder, _lifetime.Snapshot, DiagnosticReadOnlyChecks.Collect(_sessionObserver?.SessionNotificationsAvailable == true, _sessionObserver?.PowerNotificationsAvailable == true));
                exportStatus.Text = "Relatório salvo em: " + path;
                _journal.Record("report_exported", _lifetime.Snapshot);
            }
            catch (Exception ex) { _journal.RecordException("report_export_failed", ex, _lifetime.Snapshot); exportStatus.Text = "Não foi possível salvar o relatório: " + ex.GetType().Name; }
        });
        stack.Children.Add(_exportDiagnosticButton);
        stack.Children.Add(exportStatus);
        stack.Children.Add(Body("Lista apenas processo e título de janelas visíveis. O Orbit não captura telas nem lê conteúdo de páginas, documentos ou formulários."));
        var idle = OrbitWindowDiagnostics.GetIdleTime(); stack.Children.Add(Body("Tempo desde a última entrada informada pelo Windows: " + (idle?.ToString(@"hh\:mm\:ss") ?? "indisponível") + ". Esse contador não comprova presença física nem o estado de bloqueio."));
        var search = new TextBox { PlaceholderText = "Filtrar por processo ou trecho do título", MaxWidth = 700 }; stack.Children.Add(search);
        var list = new ListView { MinHeight = 240, MaxHeight = 480, Background = Brush("OrbitSurfaceBrush") }; stack.Children.Add(Card(list));
        var refresh = ActionButton("Atualizar janelas", (_, _) => Refresh()); stack.Children.Add(refresh);
        void Refresh()
        {
            var term = search.Text.Trim();
            list.ItemsSource = OrbitWindowDiagnostics.GetWindows().Where(w => term.Length == 0 || w.Process.Contains(term, StringComparison.OrdinalIgnoreCase) || w.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(w => $"{(w.IsForeground ? "●  " : "   ")}{w.Process}   ·   {w.Title}").ToList();
        }
        search.TextChanged += (_, _) => Refresh(); Refresh();
        return Scroll(stack);
    }

    private UIElement BuildHistory()
    {
        var stack = PageStack(); stack.Children.Add(Eyebrow("HISTÓRICO TÉCNICO")); stack.Children.Add(Heading("Atividade recente"));
        stack.Children.Add(Body("Registros locais existentes em %LOCALAPPDATA%\\WindowsKeepAwake. Caminhos individuais de arquivos não são registrados."));
        var card = Card(); var list = new ListView { Background = Brush("OrbitSurfaceBrush"), MinHeight = 240 };
        try
        {
            var entries = _history.Load().Select(entry => $"{entry.StartedAt.ToLocalTime():g}  ·  {(entry.Action == MaintenanceActionType.Analysis ? "Análise" : "Execução")}  ·  {entry.OperationId}  ·  {AnalysisName(entry.Status)}  ·  {entry.FilesIdentified:N0} itens  ·  {FormatBytes(entry.PotentialBytes)}").ToList();
            if (entries.Count == 0) entries.Add("Ainda não há registros.");
            list.ItemsSource = entries;
        }
        catch (Exception ex) { _journal.RecordException("history_read_failed", ex, _lifetime.Snapshot); list.ItemsSource = new[] { "Não foi possível ler o histórico." }; }
        card.Child = list; stack.Children.Add(card); return Scroll(stack);
    }

    private StackPanel StatusTile(string eyebrow, string value, string description)
    {
        var panel = new StackPanel { Spacing = 6, Padding = new Thickness(12) };
        panel.Children.Add(Eyebrow(eyebrow));
        var valueText = new TextBlock { Text = value, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush("OrbitTextBrush"), TextWrapping = TextWrapping.Wrap };
        if (eyebrow == "KEEP AWAKE") _dashboardPowerStatus = valueText;
        if (eyebrow == "MODO DE APRESENTAÇÃO") _dashboardPresentationStatus = valueText;
        panel.Children.Add(valueText); panel.Children.Add(Body(description)); return panel;
    }
    private void KeepAwake_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_powerStatusText is not null) _powerStatusText.Text = _keepAwake.Status;
        if (_presentationStatusText is not null) _presentationStatusText.Text = "Estado: " + _keepAwake.PresentationStatus;
        if (_keepAwakeMessageText is not null) _keepAwakeMessageText.Text = _keepAwake.Message;
        if (_powerToggleButton is not null) _powerToggleButton.Content = _keepAwake.ToggleLabel;
        if (_presentationToggleButton is not null) _presentationToggleButton.Content = _keepAwake.PresentationLabel;
        if (_dashboardPowerStatus is not null) _dashboardPowerStatus.Text = _keepAwake.Status;
        if (_dashboardPresentationStatus is not null) _dashboardPresentationStatus.Text = _keepAwake.PresentationStatus;
        SyncTrayMenu();
        if (_verificationText is not null) _verificationText.Text = _keepAwake.VerificationNotice;
        UpdateSnapshot();
    }
    private void SelectPage(string tag)
    {
        var item = Navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(i => Equals(i.Tag, tag)); if (item is not null) Navigation.SelectedItem = item;
    }
    private void SetPowerState(bool enabled)
    {
        if (_keepAwake.IsActive != enabled) _keepAwake.TogglePower();
        SyncTrayMenu();
    }
    private void SyncTrayMenu()
    {
        if (_trayDisposed) return;
        _trayStart.Enabled = !_keepAwake.IsActive; _trayStop.Enabled = _keepAwake.IsActive;
        _trayPresentation.Text = _keepAwake.IsPresentationActive ? "Desativar modo de apresentação" : "Ativar modo de apresentação";
        _trayIcon.Text = "Orbit — " + (_keepAwake.IsActive || _keepAwake.IsPresentationActive ? "Pedidos registrados (estado interno)" : "Sem pedidos");
    }
    internal void RestoreFromTray()
    {
        if (_trayDisposed) return;
        ShowWindow(_windowHandle, 9); Activate();
        if (IsWindowVisible(_windowHandle) && !IsIconic(_windowHandle)) _lifetime.Restore();
        else _journal.Record("window_restore_unconfirmed", _lifetime.Snapshot);
        UpdateSnapshot();
    }
    private void HideToTray()
    {
        if (_trayDisposed) return;
        _trayIcon.Visible = true; ShowWindow(_windowHandle, 0);
        if (!IsWindowVisible(_windowHandle)) _lifetime.Hide();
        else _journal.Record("window_hide_unconfirmed", _lifetime.Snapshot);
        UpdateSnapshot();
    }
    private void ExitApplication()
    {
        if (_lifetime.ExitRequested) return;
        _lifetime.RequestExit();
        Close();
    }
    private void DisposeTray()
    {
        if (_trayDisposed) return;
        _trayDisposed = true; _trayIcon.Visible = false; _trayIcon.Dispose(); _trayMenu.Dispose(); _trayImage.Dispose();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    private StackPanel PageStack() => new() { Spacing = 18, MaxWidth = 1120, HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0, 8, 0, 24) };
    private ScrollViewer Scroll(UIElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private Border Card(UIElement? child = null) => new() { Child = child, Background = Brush("OrbitSurfaceBrush"), CornerRadius = new CornerRadius(16), Padding = new Thickness(20), BorderBrush = Brush("OrbitSurfaceAltBrush"), BorderThickness = new Thickness(1) };
    private TextBlock Heading(string text, double size = 34) => new() { Text = text, FontSize = size, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush("OrbitTextBrush"), TextWrapping = TextWrapping.Wrap };
    private TextBlock Body(string text) => new() { Text = text, FontSize = 15, Foreground = Brush("OrbitMutedBrush"), TextWrapping = TextWrapping.Wrap, MaxWidth = 900 };
    private TextBlock Eyebrow(string text) => new() { Text = text, FontSize = 12, CharacterSpacing = 130, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Brush("OrbitAccentBrush"), TextWrapping = TextWrapping.Wrap };
    private Button ActionButton(string text, RoutedEventHandler clicked, bool secondary = false)
    {
        var button = new Button { Content = text, Padding = new Thickness(18, 11, 18, 11), MinHeight = 44, HorizontalAlignment = HorizontalAlignment.Left, Background = secondary ? Brush("OrbitSurfaceAltBrush") : Brush("OrbitAccentBrush"), Foreground = Brush(secondary ? "OrbitTextBrush" : "OrbitBackgroundBrush") };
        button.Click += clicked; return button;
    }
    private Brush Brush(string key) => (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
    private void UpdateSnapshot()
    {
        App.LatestSnapshot = _lifetime.Snapshot;
        if (_diagnosticState is not null) _diagnosticState.Text = DiagnosticStateText();
    }
    private string DiagnosticStateText() => $"Último estado de sessão recebido: {_lifetime.Session}\nEnergia: {_lifetime.Power}\n" +
        $"Observador da sessão: {(_sessionObserver?.SessionNotificationsAvailable == true ? "registrado" : "indisponível")}\n" +
        $"Observador de energia: {(_sessionObserver?.PowerNotificationsAvailable == true ? "registrado" : "indisponível")}\n" +
        $"Persistência do diagnóstico: {_journal.LastError ?? "sem falhas registradas"}\n{_keepAwake.VerificationNotice}";
    private void Log(string message)
    {
        _journal.Record("application_event", _lifetime.Snapshot, message);
    }
    private static string FormatBytes(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{bytes / 1024d:0.0} KiB" : $"{bytes / (1024d * 1024):0.0} MiB";
    private static string AnalysisName(MaintenanceAnalysisStatus status) => status switch { MaintenanceAnalysisStatus.Completed => "Concluída", MaintenanceAnalysisStatus.Empty => "Vazia", MaintenanceAnalysisStatus.Partial => "Parcial", MaintenanceAnalysisStatus.Cancelled => "Cancelada", _ => "Erro" };
    private static string CategoryName(MaintenanceCategory category) => category switch { MaintenanceCategory.BasicCleanup => "Limpeza básica", MaintenanceCategory.Applications => "Aplicativos", MaintenanceCategory.Windows => "Windows", _ => "Diagnóstico" };
    private static string RiskName(MaintenanceRisk risk) => risk switch { MaintenanceRisk.Low => "Baixo", MaintenanceRisk.Medium => "Médio", _ => "Alto" };
}
