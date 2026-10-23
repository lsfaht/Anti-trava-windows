using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using KeepAwake.Core;

namespace KeepAwake.Windows;

internal sealed partial class MainForm : Form
{
    private readonly PowerRequest _request;
    private PresentationModeRequest? _presentationRequest;
    private readonly Action<string>? _logSink;
    private readonly Action<string>? _errorPresenter;
    private readonly Label _status = new() { AutoSize = true, Text = "Inativa" };
    private readonly Label _session = new() { AutoSize = true };
    private readonly Button _toggle = new() { Text = "Iniciar manutenção", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _presentationToggle = new() { Text = "Ativar modo de apresentação", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Label _presentationStatus = new() { AutoSize = true, Text = "Modo de apresentação: Inativo" };
    private readonly TextBox _filters = new() { Width = 420, PlaceholderText = "Ex.: chrome; excel (processo ou parte do título)" };
    private readonly CheckBox _display = new() { Text = "Manter também a tela ligada", Checked = true, AutoSize = true };
    private readonly ComboBox _duration = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly CheckBox _startup = new() { Text = "Iniciar com o Windows minimizado na bandeja", AutoSize = true };
    private readonly ListView _windows = new() { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill };
    private readonly TextBox _report = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill };
    private readonly Label _pageTitle = new() { AutoSize = true };
    private readonly Label _pageDescription = new() { AutoSize = true };
    private readonly Label _brandTitle = new() { AutoSize = true };
    private readonly Label _brandDescription = new() { AutoSize = true };
    private readonly PowerStatusIndicator _powerIndicator = new();
    private readonly Dictionary<string, Panel> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _navigationButtons = new(StringComparer.Ordinal);
    private TableLayoutPanel? _shellLayout;
    private TableLayoutPanel? _sidebarLayout;
    private ToolTip? _navigationToolTip;
    private readonly Font _navigationFullFont = new("Segoe UI", 9.5F, FontStyle.Regular);
    private readonly Font _navigationFullSelectedFont = new("Segoe UI Semibold", 9.5F, FontStyle.Regular);
    private readonly Font _navigationCompactFont = new("Segoe UI", 13F, FontStyle.Regular);
    private readonly Font _navigationCompactSelectedFont = new("Segoe UI Semibold", 13F, FontStyle.Regular);
    private bool _responsiveShellInitialized;
    private bool _navigationCompact;
    private readonly Label _dashboardAwakeState = new() { AutoSize = true };
    private readonly Label _dashboardPresentationState = new() { AutoSize = true };
    private readonly Label _dashboardDisplayState = new() { AutoSize = true };
    private readonly Label _dashboardDuration = new() { AutoSize = true };
    private readonly Label _dashboardStartup = new() { AutoSize = true };
    private readonly Label _diagnosticState = new() { AutoSize = true, Text = "Relatório ainda não atualizado." };
    private string _currentPage = "Início";
    private readonly string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsKeepAwake", "diagnostics.log");
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _trayOpen = new("Abrir janela");
    private readonly ToolStripMenuItem _trayStart = new("Iniciar manutenção de energia");
    private readonly ToolStripMenuItem _trayStop = new("Interromper manutenção de energia");
    private readonly ToolStripMenuItem _trayPresentation = new("Ativar modo de apresentação");
    private readonly ToolStripMenuItem _trayExit = new("Sair");
    private readonly Icon _trayIconImage;
    private readonly NotifyIcon _trayIcon;
    private readonly MaintenanceCenterControl _maintenanceCenter;
    private bool _allowExit;
    private bool _trayDisposed;
    private bool _explicitlyStopped;
    private readonly bool _autoActivatePresentation;
    private DateTimeOffset? _expiresAt;
    private readonly System.Windows.Forms.Timer _expiryTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _powerStatusTimer = new() { Interval = 15000 };
    private readonly Func<PowerSupplySnapshot> _powerStatusProvider;
    private string? _lastPowerStatusError;

    public MainForm() : this(null, null, null, null, true) { }

    internal MainForm(PowerRequest? request, Action<string>? logSink, Action<string>? errorPresenter, PresentationModeRequest? presentationRequest = null, bool autoActivatePresentation = false, IUserTempAnalysisService? maintenanceAnalysisService = null, IMaintenanceHistoryStore? maintenanceHistoryStore = null, Func<PowerSupplySnapshot>? powerStatusProvider = null)
    {
        _request = request ?? new PowerRequest(new WindowsExecutionStateApi());
        _presentationRequest = presentationRequest;
        _autoActivatePresentation = autoActivatePresentation;
        _logSink = logSink;
        _errorPresenter = errorPresenter;
        _powerStatusProvider = powerStatusProvider ?? WindowsPowerStatusReader.Read;
        var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsKeepAwake");
        _maintenanceCenter = new MaintenanceCenterControl(
            maintenanceAnalysisService ?? new WindowsUserTempAnalysisService(),
            maintenanceHistoryStore ?? new JsonMaintenanceHistoryStore(Path.Combine(appDataDirectory, "maintenance-history.json")));
        Text = "Windows Keep Awake";
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterScreen;
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
        ClientSize = new Size(Math.Min(1320, Math.Max(820, workArea.Width - 64)), Math.Min(820, Math.Max(540, workArea.Height - 80)));
        MinimumSize = new Size(800, 480);
        BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text;
        Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        BuildApplicationShell();
        _windows.Columns.Add("Processo", 170); _windows.Columns.Add("Título da janela", 450); _windows.Columns.Add("Estado", 90);
        _windows.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _windows.Resize += (_, _) => ResizeWindowColumns();
        ResizeWindowColumns();
        _toggle.Click += (_, _) => ToggleRequest();
        _presentationToggle.Click += (_, _) => TogglePresentationRequest();
        _timer.Tick += (_, _) => RefreshStatus();
        _powerStatusTimer.Tick += (_, _) => RefreshPowerStatus();
        _filters.TextChanged += (_, _) => RefreshStatus();
        _duration.Items.AddRange(["30 minutos", "1 hora", "2 horas", "Indefinidamente"]);
        _duration.SelectedIndex = 3;
        _duration.SelectedIndexChanged += (_, _) =>
        {
            if (_request.IsEnabled || _presentationRequest?.IsEnabled == true) StartExpiry();
            UpdateDashboard();
        };
        _startup.Checked = StartupRegistration.IsEnabled;
        _startup.CheckedChanged += HandleStartupChanged;
        _display.CheckedChanged += (_, _) => UpdateDashboard();
        _expiryTimer.Tick += (_, _) => CheckExpiration();
        _diagnosticState.Text = "Relatório atualizado automaticamente ao abrir.";
        _trayMenu.Items.AddRange([_trayOpen, new ToolStripSeparator(), _trayStart, _trayStop, _trayPresentation, new ToolStripSeparator(), _trayExit]);
        _trayOpen.Click += (_, _) => RestoreWindow();
        _trayStart.Click += (_, _) => ToggleRequest();
        _trayStop.Click += (_, _) => ToggleRequest();
        _trayPresentation.Click += (_, _) => TogglePresentationRequest();
        _trayExit.Click += (_, _) => ExitApplication();
        _trayIconImage = TrayIconAssets.Load();
        _trayIcon = new NotifyIcon { Icon = _trayIconImage, Text = "Windows Keep Awake", ContextMenuStrip = _trayMenu, Visible = true };
        _trayIcon.DoubleClick += (_, _) => HandleTrayDoubleClick();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) HideToTray(); };
        FormClosing += HandleFormClosing;
        Shown += (_, _) => { _timer.Start(); _expiryTimer.Start(); _powerStatusTimer.Start(); RefreshPowerStatus(); RefreshStatus(); _report.Text = BuildReport(); if (_autoActivatePresentation && !_explicitlyStopped && _presentationRequest?.IsEnabled != true) TogglePresentationRequest(); };
        SyncTrayMenu();
    }

    internal string StatusText => _status.Text;
    internal string ToggleText => _toggle.Text;
    internal Button ToggleButton => _toggle;
    internal string PresentationStatusText => _presentationStatus.Text;
    internal string PresentationToggleText => _presentationToggle.Text;
    internal Button PresentationToggleButton => _presentationToggle;
    internal NotifyIcon TrayIcon => _trayIcon;
    internal Icon TrayIconImage => _trayIconImage;
    internal ToolStripMenuItem TrayOpenItem => _trayOpen;
    internal ToolStripMenuItem TrayStartItem => _trayStart;
    internal ToolStripMenuItem TrayStopItem => _trayStop;
    internal ToolStripMenuItem TrayPresentationItem => _trayPresentation;
    internal ToolStripMenuItem TrayExitItem => _trayExit;
    internal bool TrayDisposed => _trayDisposed;
    internal MaintenanceCenterControl MaintenanceCenter => _maintenanceCenter;
    internal string CurrentPage => _currentPage;
    internal IReadOnlyList<string> NavigationPages => _pages.Keys.ToArray();
    internal Button GetNavigationButton(string page) => _navigationButtons[page];
    internal int SidebarWidth => _shellLayout is null ? 0 : (int)_shellLayout.ColumnStyles[0].Width;
    internal Control GetPageControl(string page) => _pages[page];
    internal Label DashboardAwakeState => _dashboardAwakeState;
    internal Label DashboardPresentationState => _dashboardPresentationState;
    internal Label DashboardDisplayState => _dashboardDisplayState;
    internal Label DashboardDuration => _dashboardDuration;
    internal PowerStatusIndicator PowerIndicator => _powerIndicator;
    internal ComboBox DurationSelector => _duration;
    internal CheckBox KeepDisplayOption => _display;
    internal CheckBox StartupOption => _startup;
    internal void ExpireForTesting() { _expiresAt = DateTimeOffset.Now.AddSeconds(-1); CheckExpiration(); }
    internal TextBox FilterInput => _filters;
    internal IReadOnlyList<WindowInfo> DisplayedWindows => _windows.Items.Cast<ListViewItem>()
        .Select(item => new WindowInfo(item.Text, item.SubItems[1].Text, item.SubItems[2].Text == "Ativa"))
        .ToArray();

    internal void RestoreWindow()
    {
        if (IsDisposed) return;
        ShowInTaskbar = true;
        if (!Visible) Show();
        WindowState = FormWindowState.Normal;
        EnsureWindowIsOnScreen();
        Activate();
        BringToFront();
    }

    private void EnsureWindowIsOnScreen()
    {
        var bounds = Bounds;
        var hasVisibleArea = Screen.AllScreens.Any(screen =>
        {
            var intersection = Rectangle.Intersect(bounds, screen.WorkingArea);
            return intersection.Width >= Math.Min(100, bounds.Width)
                && intersection.Height >= Math.Min(80, bounds.Height);
        });
        if (hasVisibleArea) return;

        var workArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        var width = Math.Min(Math.Max(Width, MinimumSize.Width), workArea.Width);
        var height = Math.Min(Math.Max(Height, MinimumSize.Height), workArea.Height);
        Bounds = new Rectangle(
            workArea.Left + Math.Max(0, (workArea.Width - width) / 2),
            workArea.Top + Math.Max(0, (workArea.Height - height) / 2),
            width,
            height);
    }

    internal void HandleTrayDoubleClick() => RestoreWindow();

    internal void HideToTray()
    {
        if (IsDisposed) return;
        ShowInTaskbar = false;
        Hide();
    }

    internal void ExitApplication()
    {
        if (IsDisposed) return;
        _allowExit = true;
        Close();
        if (!IsDisposed) _allowExit = false;
    }

    private void ToggleRequest()
    {
        try
        {
            if (_request.IsEnabled) { _request.Disable(); _explicitlyStopped = true; _status.Text = "Inativa"; _toggle.Text = "Iniciar manutenção"; _expiresAt = null; Log("Solicitação de energia liberada pelo usuário."); }
            else { _request.Enable(_display.Checked); _explicitlyStopped = false; _status.Text = "Ativa"; _toggle.Text = "Interromper manutenção"; StartExpiry(); Log("Solicitação de energia ativada pelo usuário."); }
            SyncTrayMenu();
        }
        catch (Exception ex)
        {
            _status.Text = _request.IsEnabled ? "Ativa — erro ao liberar" : "Erro — inativa";
            _toggle.Text = _request.IsEnabled ? "Interromper manutenção" : "Iniciar manutenção";
            SyncTrayMenu();
            Log("Erro de SetThreadExecutionState: " + ex.Message);
            if (_errorPresenter is null)
                MessageBox.Show(this, ex.Message, "Falha ao alterar solicitação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
                _errorPresenter(ex.Message);
        }
    }

    private void TogglePresentationRequest()
    {
        try
        {
            if (_presentationRequest?.IsEnabled == true)
            {
                _presentationRequest.Disable();
                _explicitlyStopped = true;
                _presentationStatus.Text = "Modo de apresentação: Inativo";
                _presentationToggle.Text = "Ativar modo de apresentação";
                Log("Modo de apresentação desativado pelo usuário.");
            }
            else
            {
                _presentationRequest ??= new PresentationModeRequest(new WindowsPresentationPowerApi());
                _presentationRequest.Enable();
                _explicitlyStopped = false;
                StartExpiry();
                _presentationStatus.Text = "Modo de apresentação: Ativo";
                _presentationToggle.Text = "Interromper modo de apresentação";
                Log("Modo de apresentação ativado pelo usuário.");
            }
            SyncTrayMenu();
        }
        catch (Exception ex)
        {
            _presentationStatus.Text = _presentationRequest?.IsEnabled == true ? "Modo de apresentação: Ativo — erro ao liberar" : "Modo de apresentação: Erro — inativo";
            _presentationToggle.Text = _presentationRequest?.IsEnabled == true ? "Interromper modo de apresentação" : "Ativar modo de apresentação";
            SyncTrayMenu();
            Log("Erro de PowerSetRequest/PowerClearRequest: " + ex.Message);
            if (_errorPresenter is null)
                MessageBox.Show(this, ex.Message, "Falha ao alterar modo de apresentação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
                _errorPresenter(ex.Message);
        }
    }

    private void SyncTrayMenu()
    {
        var requestActive = _request.IsEnabled;
        var presentationActive = _presentationRequest?.IsEnabled == true;
        _trayStart.Enabled = !requestActive;
        _trayStop.Enabled = requestActive;
        _trayPresentation.Text = presentationActive ? "Desativar modo de apresentação" : "Ativar modo de apresentação";
        _trayIcon.Text = "Windows Keep Awake — " + (requestActive || presentationActive ? "Ativo" : "Inativo");
        UpdateDashboard();
    }

    private void StartExpiry()
    {
        var duration = _duration.SelectedIndex switch { 0 => TimeSpan.FromMinutes(30), 1 => TimeSpan.FromHours(1), 2 => TimeSpan.FromHours(2), _ => (TimeSpan?)null };
        _expiresAt = duration.HasValue ? DateTimeOffset.Now + duration.Value : null;
    }

    private void CheckExpiration()
    {
        if (_expiresAt is not { } expiresAt || DateTimeOffset.Now < expiresAt) return;
        _expiresAt = null;
        var errors = new List<string>();
        try { _request.Disable(); } catch (Exception ex) { errors.Add(ex.Message); }
        try { _presentationRequest?.Disable(); } catch (Exception ex) { errors.Add(ex.Message); }
        _status.Text = _request.IsEnabled ? "Ativa — erro ao liberar" : errors.Count == 0 ? "Inativa" : "Erro — inativa";
        _toggle.Text = _request.IsEnabled ? "Interromper manutenção" : "Iniciar manutenção";
        _presentationStatus.Text = _presentationRequest?.IsEnabled == true ? "Modo de apresentação: Ativo — erro ao liberar" : errors.Count == 0 ? "Modo de apresentação: Inativo" : "Modo de apresentação: Erro — inativo";
        _presentationToggle.Text = _presentationRequest?.IsEnabled == true ? "Interromper modo de apresentação" : "Ativar modo de apresentação";
        Log(errors.Count == 0 ? "Temporizador expirou; solicitações liberadas." : "Temporizador expirou; falha ao liberar solicitações: " + string.Join("; ", errors));
        SyncTrayMenu();
        if (errors.Count > 0) _errorPresenter?.Invoke(string.Join(Environment.NewLine, errors));
    }

    private void SetStartup(bool enabled)
    {
        try { StartupRegistration.SetEnabled(enabled); Log(enabled ? "Inicialização com Windows habilitada pelo usuário." : "Inicialização com Windows desabilitada pelo usuário."); }
        catch (Exception ex) { _startup.CheckedChanged -= HandleStartupChanged; _startup.Checked = !enabled; _startup.CheckedChanged += HandleStartupChanged; Log("Falha ao configurar inicialização: " + ex.Message); MessageBox.Show(this, ex.Message, "Falha na inicialização", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void HandleStartupChanged(object? sender, EventArgs e) => SetStartup(_startup.Checked);

    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !_allowExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        var errors = new List<string>();
        try { _request.Dispose(); }
        catch (Exception ex) { errors.Add("Falha ao liberar estado de energia: " + ex.Message); }
        if (_presentationRequest is not null)
            try { _presentationRequest.Dispose(); }
            catch (Exception ex) { errors.Add("Falha ao liberar modo de apresentação: " + ex.Message); }

        if (errors.Count > 0)
        {
            foreach (var error in errors) Log(error);
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                _allowExit = false;
                _timer.Start();
                RestoreWindow();
                var message = string.Join(Environment.NewLine, errors);
                if (_errorPresenter is null)
                    MessageBox.Show(this, message, "Falha ao sair", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    _errorPresenter(message);
                SyncTrayMenu();
                return;
            }
        }

        _timer.Stop();
        _expiryTimer.Stop();
        _powerStatusTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIconImage.Dispose();
        _trayMenu.Dispose();
        _navigationToolTip?.Dispose();
        _navigationFullFont.Dispose();
        _navigationFullSelectedFont.Dispose();
        _navigationCompactFont.Dispose();
        _navigationCompactSelectedFont.Dispose();
        _trayDisposed = true;
    }

    protected override void WndProc(ref Message m)
    {
        const int wmPowerBroadcast = 0x0218;
        const int pbtApmPowerStatusChange = 0x000A;
        if (m.Msg == wmPowerBroadcast && m.WParam.ToInt32() == pbtApmPowerStatusChange)
            RefreshPowerStatus();
        base.WndProc(ref m);
    }

    internal void RefreshPowerStatus()
    {
        try
        {
            var snapshot = _powerStatusProvider();
            _powerIndicator.SetSnapshot(snapshot);
            _lastPowerStatusError = snapshot.Error;
        }
        catch (Exception ex)
        {
            _powerIndicator.SetSnapshot(PowerSupplySnapshot.Unavailable(ex.Message));
            if (!string.Equals(_lastPowerStatusError, ex.Message, StringComparison.Ordinal))
                Log("Falha ao consultar alimentação e bateria: " + ex.Message);
            _lastPowerStatusError = ex.Message;
        }
        _powerIndicator.SetKeepAwakeActive(_request.IsEnabled || _presentationRequest?.IsEnabled == true);
    }

    internal void RefreshStatus()
    {
        try
        {
            var all = WindowEnumerator.GetVisibleWindows();
            var matches = WindowFilter.Apply(all, _filters.Text);
            _windows.BeginUpdate(); _windows.Items.Clear();
            foreach (var w in matches)
                _windows.Items.Add(new ListViewItem([w.ProcessName, w.Title, w.IsForeground ? "Ativa" : "Visível"]));
            _windows.EndUpdate();
            var foreground = all.FirstOrDefault(w => w.IsForeground);
            var idle = SessionState.GetIdleTime();
            _session.Text = $"Desktop: {SessionState.GetDesktopState()} | Janelas visíveis: {matches.Count}/{all.Count}{Environment.NewLine}Última entrada: {(idle.HasValue ? $"{idle.Value.TotalSeconds:F0} s atrás" : "indisponível")}" + (foreground is null ? "" : $" | Janela ativa: {foreground.ProcessName}");
        }
        catch (Exception ex) { _session.Text = "Estado da sessão indisponível: " + ex.Message; }
        UpdateDashboard();
    }

    private string BuildReport()
    {
        var b = new StringBuilder();
        b.AppendLine("Windows Keep Awake — relatório de diagnóstico (somente leitura)");
        b.AppendLine("Coletado em: " + DateTimeOffset.Now.ToString("O"));
        b.AppendLine("\nConfirmado pela aplicação:");
        b.AppendLine("• SetThreadExecutionState solicita ES_SYSTEM_REQUIRED e, opcionalmente, ES_DISPLAY_REQUIRED enquanto ativada.");
        b.AppendLine("• Esta API não simula presença, não impede protetor de tela e não substitui políticas de bloqueio.");
        b.AppendLine("• O modo de apresentação opcional usa PowerSetRequest com PowerRequestDisplayRequired e PowerRequestSystemRequired; a documentação do Windows informa que o pedido de tela evita protetor de tela e bloqueio automático por inatividade enquanto estiver ativo.");
        b.AppendLine("• O modo de apresentação não desbloqueia uma sessão já bloqueada, não impede bloqueio manual e só deve ser usado se autorizado pela organização. Ele não altera a política configurada.");
        b.AppendLine("• A enumeração de janelas consulta apenas visibilidade, processo e título; não lê conteúdo de janelas.");
        b.AppendLine("\nValores consultados (ausente ou inacessível será indicado):");
        ReadRegistry(b, Registry.CurrentUser, @"Control Panel\Desktop", "ScreenSaveActive", "ScreenSaveTimeOut", "ScreenSaverIsSecure");
        ReadRegistry(b, Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Control Panel\Desktop", "ScreenSaveActive", "ScreenSaveTimeOut", "ScreenSaverIsSecure");
        ReadRegistry(b, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "InactivityTimeoutSecs");
        ReadRegistry(b, Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Control Panel\Desktop", "ScreenSaveActive", "ScreenSaveTimeOut", "ScreenSaverIsSecure");
        b.AppendLine("\nInterpretação e limites:");
        b.AppendLine("• Suspensão é transição de energia do sistema; desligar o monitor mantém o Windows em execução.");
        b.AppendLine("• Protetor de tela é uma preferência visual; a opção segura pode bloquear ao retornar.");
        b.AppendLine("• InactivityTimeoutSecs define um limite de inatividade para bloqueio de segurança quando aplicado; sua presença e valor, isoladamente, não provam a causa do bloqueio observado.");
        b.AppendLine("• Um bloqueio em cerca de 2 minutos pode vir de política local/domínio, protetor de tela ou ferramenta de gerenciamento corporativo. Este programa não consegue identificar qual mecanismo venceu sem evidência adicional.");
        b.AppendLine("• gpresult /scope computer /z retornou Acesso negado, conforme informado. Este relatório não tenta elevar privilégios nem ler logs de segurança protegidos.");
        b.AppendLine("• Eventos relevantes podem estar nos logs System, Security ou operacionais de gerenciamento; a leitura pode exigir autorização administrativa. Nenhum evento foi confirmado por este relatório.");
        b.AppendLine("\nSolicite ao suporte/administrador autorizado verificar a política efetiva de bloqueio interativo (incluindo tempo limite de máquina), protetor de tela seguro e ferramentas de gerenciamento aplicadas. Não altere políticas corporativas localmente.");
        b.AppendLine("\nLog local: " + _logPath);
        Log("Relatório somente leitura atualizado.");
        return b.ToString();
    }

    private static void ReadRegistry(StringBuilder b, RegistryKey root, string path, params string[] names)
    {
        b.AppendLine($"[{root.Name}\\{path}]");
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            foreach (var name in names)
            {
                if (key is null) { b.AppendLine($"  {name}: chave ausente ou sem acesso"); continue; }
                try { b.AppendLine($"  {name}: {key.GetValue(name, "<não definido>")}"); }
                catch (Exception ex) { b.AppendLine($"  {name}: inacessível ({ex.GetType().Name})"); }
            }
        }
        catch (Exception ex) { b.AppendLine("  inacessível (" + ex.GetType().Name + ")"); }
    }

    private void Log(string message)
    {
        if (_logSink is not null) { _logSink(message); return; }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
            var backup = _logPath + ".1";
            if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 1024 * 1024)
            {
                File.Delete(backup);
                File.Move(_logPath, backup);
            }
            File.AppendAllText(_logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }
}

internal static class SessionState
{
    [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder info, uint length, out uint needed);

    public static TimeSpan? GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return null;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    public static string GetDesktopState()
    {
        const uint desktopReadObjects = 0x0001;
        const int uoiName = 2;
        var handle = OpenInputDesktop(0, false, desktopReadObjects);
        if (handle == IntPtr.Zero) return "indisponível (" + Marshal.GetLastWin32Error() + ")";
        try
        {
            var name = new StringBuilder(128);
            return GetUserObjectInformation(handle, uoiName, name, (uint)(name.Capacity * 2), out _)
                ? name.ToString().Equals("Winlogon", StringComparison.OrdinalIgnoreCase) ? "tela segura/bloqueio provável" : "desktop interativo"
                : "indisponível";
        }
        finally { CloseDesktop(handle); }
    }
}
