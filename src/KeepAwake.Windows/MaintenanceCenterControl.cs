using System.Globalization;
using KeepAwake.Core;

namespace KeepAwake.Windows;

internal sealed class MaintenanceCenterControl : UserControl
{
    private readonly IUserTempAnalysisService _analysisService;
    private readonly IMaintenanceHistoryStore _historyStore;
    private readonly MaintenanceExecutionRegistry _executionRegistry;
    private readonly TableLayoutPanel _layout = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = AppTheme.Background, Margin = Padding.Empty };
    private Control _header = null!;
    private Control _toolbar = null!;
    private Control _catalogCard = null!;
    private Control _detailsCard = null!;
    private Control _analysisCard = null!;
    private Control _historyCard = null!;
    private readonly ListView _catalog = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, GridLines = false, Scrollable = true };
    private readonly ComboBox _categoryFilter = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _operationSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "Buscar operação" };
    private readonly TextBox _details = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true };
    private readonly Button _analyze = new() { Text = "Analisar selecionada", AutoSize = true };
    private readonly Button _previewCategory = new() { Text = "Prévia da categoria", AutoSize = true };
    private readonly Button _previewAll = new() { Text = "Prévia de todas", AutoSize = true };
    private readonly Button _executeSelected = new() { Text = "Executar selecionada", AutoSize = true, Enabled = false };
    private readonly Button _executeCategory = new() { Text = "Executar categoria", AutoSize = true, Enabled = false };
    private readonly Button _executeAll = new() { Text = "Executar todas as manutenções", AutoSize = true, Enabled = false };
    private readonly Button _cancel = new() { Text = "Cancelar operação", AutoSize = true, Enabled = false };
    private readonly ProgressBar _progressBar = new() { Width = 150, Height = 20, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Visible = false };
    private readonly Label _progressLabel = AppTheme.CreateLabel("Pronta para análise", 9, false, AppTheme.Muted);
    private readonly TextBox _result = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true };
    private readonly ListView _history = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, Scrollable = true };
    private readonly Label _analysisFiles = AppTheme.CreateLabel("Arquivos: —", 9, false, AppTheme.Muted);
    private readonly Label _analysisBytes = AppTheme.CreateLabel("Potencial: —", 9, false, AppTheme.Muted);
    private readonly Label _analysisFailures = AppTheme.CreateLabel("Falhas: —", 9, false, AppTheme.Muted);
    private readonly Label _analysisDuration = AppTheme.CreateLabel("Duração: —", 9, false, AppTheme.Muted);
    private readonly Label _catalogSummary = AppTheme.CreateLabel("", 9, false, AppTheme.Muted);
    private readonly ToolTip _toolTip = new() { ShowAlways = true, InitialDelay = 400 };
    private CancellationTokenSource? _analysisCancellation;
    private bool _analysisRunning;
    private bool _wideLayout;

    internal MaintenanceCenterControl(IUserTempAnalysisService analysisService, IMaintenanceHistoryStore historyStore, MaintenanceExecutionRegistry? executionRegistry = null)
    {
        _analysisService = analysisService;
        _historyStore = historyStore;
        _executionRegistry = executionRegistry ?? new MaintenanceExecutionRegistry();
        BuildLayout();

        _catalog.SelectedIndexChanged += (_, _) => UpdateSelection();
        _analyze.Click += async (_, _) => await RunAnalysisAsync();
        _previewCategory.Click += (_, _) => ShowExecutionPreview(FilteredOperations(), "Prévia da categoria selecionada");
        _previewAll.Click += (_, _) => ShowExecutionPreview(MaintenanceCatalog.Operations, "Prévia de todas as operações");
        _executeSelected.Click += async (_, _) => await RequestExecutionAsync(SelectedOperation is { } selected ? [selected] : [], "Executar operação selecionada", includeHighRisk: true);
        _executeCategory.Click += async (_, _) => await RequestExecutionAsync(FilteredOperations(), "Executar categoria", includeHighRisk: false);
        _executeAll.Click += async (_, _) => await RequestExecutionAsync(MaintenanceCatalog.Operations, "Executar todas as manutenções", includeHighRisk: false);
        _cancel.Click += (_, _) => _analysisCancellation?.Cancel();
        _categoryFilter.SelectedIndexChanged += (_, _) => FilterCatalog();
        _operationSearch.TextChanged += (_, _) => FilterCatalog();
        _catalog.Resize += (_, _) => ResizeCatalogColumns();
        _history.Resize += (_, _) => ResizeHistoryColumns();
        Resize += (_, _) => UpdateResponsiveLayout();

        _categoryFilter.Items.AddRange(["Todas as categorias", "Limpeza básica", "Aplicativos", "Windows", "Diagnóstico"]);
        _categoryFilter.SelectedIndex = 0;
        FilterCatalog();
        LoadHistory();
        UpdateResponsiveLayout();
    }

    internal ListView CatalogList => _catalog;
    internal Button AnalyzeButton => _analyze;
    internal Button PreviewAllButton => _previewAll;
    internal Button PreviewCategoryButton => _previewCategory;
    internal Button ExecuteSelectedButton => _executeSelected;
    internal Button ExecuteCategoryButton => _executeCategory;
    internal Button ExecuteAllButton => _executeAll;
    internal Button CancelButton => _cancel;
    internal TextBox ResultBox => _result;
    internal ListView HistoryList => _history;
    internal ComboBox CategoryFilter => _categoryFilter;
    internal TextBox OperationSearch => _operationSearch;
    internal string SelectedOperationName => SelectedOperation?.Name ?? string.Empty;
    internal string DetailsText => _details.Text;
    internal bool AnalysisRunning => _analysisRunning;
    internal bool UsesWideLayout => _wideLayout;
    internal int CatalogOperationCount => _catalog.Items.Count;
    internal int CatalogColumnsWidth => _catalog.Columns.Cast<ColumnHeader>().Sum(column => column.Width);
    internal int HistoryColumnsWidth => _history.Columns.Cast<ColumnHeader>().Sum(column => column.Width);
    internal int ExecuteButtonCount => 3;
    internal int AvailableExecutionCount => MaintenanceCatalog.Operations.Count(operation => _executionRegistry.CanExecute(operation.Id));
    internal void SelectOperationAt(int index)
    {
        if (index < 0 || index >= _catalog.Items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _catalog.Items[index].Selected = true;
        _catalog.Items[index].Focused = true;
        UpdateSelection();
    }

    private MaintenanceOperationDescriptor? SelectedOperation => _catalog.SelectedItems.Count > 0
        ? _catalog.SelectedItems[0].Tag as MaintenanceOperationDescriptor
        : null;

    private void BuildLayout()
    {
        BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text;
        Padding = new Padding(8);
        AutoScroll = true;

        _layout.ColumnCount = 2;
        _layout.RowCount = 4;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 236));
        _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 254));

        _header = BuildHeader();
        _toolbar = BuildToolbar();

        var catalogCard = CreateMaintenanceCard("Catálogo de operações");
        var catalogLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8), BackColor = AppTheme.Card };
        catalogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        catalogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        catalogLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        catalogLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _categoryFilter.Margin = Padding.Empty;
        catalogLayout.Controls.Add(_categoryFilter, 0, 0);
        _operationSearch.Margin = new Padding(0, 4, 0, 0);
        catalogLayout.Controls.Add(_operationSearch, 0, 1);
        _catalog.Columns.Add("Categoria", 105);
        _catalog.Columns.Add("Operação", 180);
        _catalog.Columns.Add("Descrição", 220);
        _catalog.Columns.Add("Risco", 64);
        _catalog.Columns.Add("Estado", 82);
        _catalog.Columns.Add("Admin", 54);
        catalogLayout.Controls.Add(_catalog, 0, 2);
        _catalogSummary.Dock = DockStyle.Fill;
        _catalogSummary.Margin = new Padding(2, 6, 2, 0);
        catalogLayout.Controls.Add(_catalogSummary, 0, 3);
        catalogCard.BodyHost!.Controls.Add(catalogLayout);
        _catalogCard = catalogCard;

        var detailsCard = CreateMaintenanceCard("Detalhes e prévia da seleção");
        _details.BorderStyle = BorderStyle.None;
        _details.Padding = new Padding(8);
        detailsCard.BodyHost!.Padding = new Padding(8);
        detailsCard.BodyHost.Controls.Add(_details);
        _detailsCard = detailsCard;

        var analysisCard = CreateMaintenanceCard("Resultados da análise ou execução");
        var analysisLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8), BackColor = AppTheme.Card };
        analysisLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        analysisLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        analysisLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var progress = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, AutoSize = true, BackColor = AppTheme.Card, Margin = Padding.Empty };
        progress.Controls.AddRange([_progressBar, _progressLabel]);
        analysisLayout.Controls.Add(progress, 0, 0);
        var metrics = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = AppTheme.Card, Margin = Padding.Empty };
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        metrics.Controls.Add(StyleMetric(_analysisFiles), 0, 0);
        metrics.Controls.Add(StyleMetric(_analysisBytes), 1, 0);
        metrics.Controls.Add(StyleMetric(_analysisFailures), 0, 1);
        metrics.Controls.Add(StyleMetric(_analysisDuration), 1, 1);
        analysisLayout.Controls.Add(metrics, 0, 1);
        _result.BorderStyle = BorderStyle.FixedSingle;
        analysisLayout.Controls.Add(_result, 0, 2);
        analysisCard.BodyHost!.Controls.Add(analysisLayout);
        _analysisCard = analysisCard;

        var historyCard = CreateMaintenanceCard("Histórico técnico · 30 dias");
        _history.Columns.Add("Data", 118);
        _history.Columns.Add("Operação", 180);
        _history.Columns.Add("Ação", 76);
        _history.Columns.Add("Resultado", 105);
        _history.Columns.Add("Itens", 65);
        _history.Columns.Add("Espaço", 112);
        _history.Columns.Add("Falhas", 55);
        historyCard.BodyHost!.Padding = new Padding(8);
        historyCard.BodyHost.Controls.Add(_history);
        _historyCard = historyCard;

        Controls.Add(_layout);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel { Name = "maintenanceHeader", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true, BackColor = AppTheme.Background, Padding = new Padding(2, 2, 4, 7), Margin = new Padding(0, 0, 0, 6) };
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var description = AppTheme.CreateLabel("Operações locais com análise somente leitura. A execução real permanece bloqueada até haver executores aprovados.", 9, false, AppTheme.Muted);
        description.AutoSize = true;
        description.MaximumSize = new Size(1100, 0);
        description.Dock = DockStyle.Fill;
        var available = MaintenanceCatalog.Operations.Count(operation => operation.CanAnalyze);
        var planned = MaintenanceCatalog.Operations.Count - available;
        var title = AppTheme.CreateLabel($"{MaintenanceCatalog.Operations.Count} operações  ·  {available} disponível(is) para análise  ·  {planned} planejadas", 10, true, AppTheme.Text);
        title.Dock = DockStyle.Fill;
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(description, 0, 1);
        return header;
    }

    private Control BuildToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Name = "maintenanceToolbar",
            Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
            BackColor = AppTheme.Background, Margin = new Padding(0, 0, 0, 8), Padding = Padding.Empty
        };
        AppTheme.StyleButton(_analyze, true);
        AppTheme.StyleButton(_previewCategory);
        AppTheme.StyleButton(_previewAll);
        AppTheme.StyleButton(_executeSelected);
        AppTheme.StyleButton(_executeCategory);
        AppTheme.StyleButton(_executeAll);
        AppTheme.StyleButton(_cancel);
        _analyze.AccessibleDescription = "Executa a análise somente leitura da operação selecionada, quando disponível.";
        _executeSelected.AccessibleDescription = "Indisponível: nenhum executor real foi aprovado e validado.";
        _executeCategory.AccessibleDescription = "Indisponível: categoria sem executores aprovados e validados.";
        _executeAll.AccessibleDescription = "Indisponível: nenhuma manutenção possui executor real aprovado.";
        _cancel.AccessibleDescription = "Cancela a análise em andamento.";
        _toolTip.SetToolTip(_executeSelected, _executeSelected.AccessibleDescription);
        _toolTip.SetToolTip(_executeCategory, _executeCategory.AccessibleDescription);
        _toolTip.SetToolTip(_executeAll, _executeAll.AccessibleDescription);
        toolbar.Controls.AddRange([_analyze, _previewCategory, _previewAll, _executeSelected, _executeCategory, _executeAll, _cancel]);
        return toolbar;
    }

    private static Label StyleMetric(Label label)
    {
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.AutoEllipsis = true;
        label.Margin = new Padding(2);
        return label;
    }

    private static CardPanel CreateMaintenanceCard(string title)
    {
        var card = AppTheme.CreateCard();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 10, 10);
        card.Padding = new Padding(8);
        var heading = AppTheme.CreateLabel(title, 9.5F, true, AppTheme.Muted);
        heading.Dock = DockStyle.Top;
        heading.Height = 26;
        var body = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Card, Padding = new Padding(0, 4, 0, 0) };
        card.Controls.Add(body);
        card.Controls.Add(heading);
        heading.BringToFront();
        card.BodyHost = body;
        return card;
    }

    private void UpdateResponsiveLayout()
    {
        var scrollbar = VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
        var width = Math.Max(320, ClientSize.Width - Padding.Horizontal - scrollbar);
        _layout.Width = width;
        var wide = width >= 980;
        if (_wideLayout == wide && _layout.ColumnStyles.Count == (wide ? 2 : 1)) return;
        _wideLayout = wide;
        _layout.SuspendLayout();
        try
        {
            _layout.Controls.Clear();
            _layout.ColumnStyles.Clear();
            _layout.RowStyles.Clear();
            _layout.ColumnCount = wide ? 2 : 1;
            _layout.RowCount = wide ? 4 : 6;
            if (wide)
            {
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 236));
                _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 254));
                AddSharedHeaderAndToolbar(2);
                _layout.Controls.Add(_catalogCard, 0, 2);
                _layout.Controls.Add(_detailsCard, 1, 2);
                _layout.Controls.Add(_analysisCard, 0, 3);
                _layout.Controls.Add(_historyCard, 1, 3);
            }
            else
            {
                _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 246));
                _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 216));
                _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 264));
                _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 264));
                AddSharedHeaderAndToolbar(1);
                _layout.Controls.Add(_catalogCard, 0, 2);
                _layout.Controls.Add(_detailsCard, 0, 3);
                _layout.Controls.Add(_analysisCard, 0, 4);
                _layout.Controls.Add(_historyCard, 0, 5);
            }
        }
        finally { _layout.ResumeLayout(true); }
        _layout.PerformLayout();
        _layout.Invalidate(true);
        ResizeCatalogColumns();
        ResizeHistoryColumns();
    }

    private void AddSharedHeaderAndToolbar(int columnSpan)
    {
        _layout.Controls.Add(_header, 0, 0);
        _layout.SetColumnSpan(_header, columnSpan);
        _layout.Controls.Add(_toolbar, 0, 1);
        _layout.SetColumnSpan(_toolbar, columnSpan);
    }

    private void FilterCatalog()
    {
        var selectedId = SelectedOperation?.Id;
        _catalog.BeginUpdate();
        _catalog.Items.Clear();
        foreach (var operation in FilteredOperations())
        {
            var item = new ListViewItem(CategoryLabel(operation.Category)) { Tag = operation };
            item.SubItems.Add(operation.Name);
            item.SubItems.Add(operation.Description);
            item.SubItems.Add(RiskLabel(operation.Risk));
            item.SubItems.Add(operation.CanAnalyze ? "Análise" : "Planejada");
            item.SubItems.Add(operation.RequiresAdministrator ? "Sim" : "Não");
            _catalog.Items.Add(item);
        }
        var selectedIndex = -1;
        if (selectedId is not null)
            for (var index = 0; index < _catalog.Items.Count; index++)
                if (_catalog.Items[index].Tag is MaintenanceOperationDescriptor item && item.Id == selectedId) selectedIndex = index;
        if (selectedIndex < 0 && _catalog.Items.Count > 0) selectedIndex = 0;
        if (selectedIndex >= 0) _catalog.Items[selectedIndex].Selected = true;
        _catalog.EndUpdate();
        UpdateSelection();
        ResizeCatalogColumns();
    }

    private IEnumerable<MaintenanceOperationDescriptor> FilteredOperations()
    {
        IEnumerable<MaintenanceOperationDescriptor> operations = MaintenanceCatalog.Operations;
        if (_categoryFilter.SelectedIndex > 0)
        {
            var selectedCategory = _categoryFilter.SelectedIndex switch
            {
                1 => MaintenanceCategory.BasicCleanup,
                2 => MaintenanceCategory.Applications,
                3 => MaintenanceCategory.Windows,
                _ => MaintenanceCategory.Diagnostics
            };
            operations = operations.Where(operation => operation.Category == selectedCategory);
        }
        var query = _operationSearch.Text.Trim();
        return query.Length == 0 ? operations : operations.Where(operation =>
            operation.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || operation.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || CategoryLabel(operation.Category).Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }

    private void ResizeCatalogColumns()
    {
        if (_catalog.Columns.Count != 6) return;
        var width = Math.Max(240, _catalog.ClientSize.Width - 8);
        if (width < 520)
        {
            _catalog.Columns[0].Width = 0;
            _catalog.Columns[1].Width = Math.Max(105, width - 118);
            _catalog.Columns[2].Width = 0;
            _catalog.Columns[3].Width = 48;
            _catalog.Columns[4].Width = 70;
            _catalog.Columns[5].Width = 0;
            return;
        }
        if (width < 760)
        {
            _catalog.Columns[0].Width = 82;
            _catalog.Columns[1].Width = Math.Max(120, width - 82 - 48 - 70 - 42);
            _catalog.Columns[2].Width = 0;
            _catalog.Columns[3].Width = 48;
            _catalog.Columns[4].Width = 70;
            _catalog.Columns[5].Width = 42;
            return;
        }
        _catalog.Columns[0].Width = 95;
        _catalog.Columns[1].Width = Math.Clamp(width / 4, 150, 230);
        _catalog.Columns[3].Width = 62;
        _catalog.Columns[4].Width = 74;
        _catalog.Columns[5].Width = 48;
        _catalog.Columns[2].Width = Math.Max(120, width - _catalog.Columns[0].Width - _catalog.Columns[1].Width - 184);
    }

    private void ResizeHistoryColumns()
    {
        if (_history.Columns.Count != 7) return;
        var width = Math.Max(300, _history.ClientSize.Width - 8);
        if (width < 620)
        {
            var compact = new[] { 112, Math.Max(100, width - 112 - 68 - 78), 68, 78, 0, 0, 0 };
            for (var index = 0; index < compact.Length; index++) _history.Columns[index].Width = compact[index];
            return;
        }
        if (width < 900)
        {
            var medium = new[] { 112, Math.Max(120, width - 112 - 68 - 78 - 60 - 48), 68, 78, 60, 0, 48 };
            for (var index = 0; index < medium.Length; index++) _history.Columns[index].Width = medium[index];
            return;
        }
        var full = new[] { 118, Math.Max(130, width - 118 - 76 - 105 - 65 - 112 - 55), 76, 105, 65, 112, 55 };
        for (var index = 0; index < full.Length; index++) _history.Columns[index].Width = full[index];
    }

    private void UpdateSelection()
    {
        var operation = SelectedOperation;
        if (operation is null)
        {
            _details.Text = "Selecione uma operação para ver descrição, risco, escopo, pré-requisitos e disponibilidade.";
            _analyze.Enabled = false;
            return;
        }

        var preview = _executionRegistry.Preview([operation]);
        var item = preview.Items[0];
        _details.Text = string.Join(Environment.NewLine,
            $"Operação: {operation.Name}",
            $"Categoria: {CategoryLabel(operation.Category)}",
            $"Estado: {AvailabilityLabel(operation.Availability)}",
            $"Risco: {RiskLabel(operation.Risk)}",
            $"Requer administrador: {(operation.RequiresAdministrator ? "Sim" : "Não")}",
            $"Escopo: {(operation.Id == UserTempAnalyzer.OperationId ? _analysisService.TargetDirectory : "a definir; não acessado nesta fase")}",
            "Pré-requisitos: consulta do catálogo não requer elevação.",
            string.Empty,
            "Objetivo e impacto esperado:",
            operation.Description,
            string.Empty,
            "Análise:",
            operation.CanAnalyze ? "Disponível em modo somente leitura." : "Ainda não implementada.",
            "Execução:",
            item.Included ? "Executor disponível; uma confirmação explícita continuará obrigatória." : item.Reason,
            operation.Id == UserTempAnalyzer.OperationId ? "A análise mede espaço potencial; não modifica arquivos." : "Nenhum comando é iniciado para esta operação planejada.");
        _analyze.Enabled = !_analysisRunning && operation.CanAnalyze && operation.Availability == MaintenanceAvailability.Available;
        _executeSelected.Enabled = !_analysisRunning && _executionRegistry.CanExecute(operation.Id);
        var categoryOperations = FilteredOperations().ToArray();
        var categoryPreview = _executionRegistry.Preview(categoryOperations);
        _executeCategory.Enabled = !_analysisRunning && categoryPreview.Included.Count > 0;
        var allPreview = _executionRegistry.Preview(MaintenanceCatalog.Operations);
        _executeAll.Enabled = !_analysisRunning && allPreview.Included.Count > 0;
        _catalogSummary.Text = $"{categoryOperations.Length} nesta categoria · {categoryOperations.Count(value => value.CanAnalyze)} para análise · {categoryPreview.Included.Count} executável(is) · {categoryOperations.Count(value => value.Risk == MaintenanceRisk.High)} risco alto";
    }

    private void ShowExecutionPreview(IEnumerable<MaintenanceOperationDescriptor> operations, string heading)
    {
        var preview = _executionRegistry.Preview(operations);
        _details.Text = FormatExecutionPreview(preview, heading);
    }

    private static string FormatExecutionPreview(MaintenanceExecutionPreview preview, string heading)
    {
        var entries = preview.Items.Select(item =>
        {
            var operation = MaintenanceCatalog.Operations.FirstOrDefault(value => value.Id == item.OperationId);
            if (operation is null) return item.Reason;
            return $"{(item.Included ? "INCLUÍDA" : "IGNORADA")} · {operation.Name} · risco {RiskLabel(item.Risk)} · administrador {(item.RequiresAdministrator ? "sim" : "não")} — {item.Reason}";
        });
        return string.Join(Environment.NewLine, [heading,
            $"Incluídas: {preview.Included.Count} · indisponíveis/ignoradas: {preview.Items.Count - preview.Included.Count} · alto risco não incluído automaticamente.",
            "Nenhuma ação foi executada. Uma execução futura ainda exigirá confirmação explícita.",
            string.Empty, .. entries]);
    }

    private async Task RequestExecutionAsync(IEnumerable<MaintenanceOperationDescriptor> operations, string title, bool includeHighRisk)
    {
        if (_analysisRunning) return;
        var preview = _executionRegistry.Preview(operations, includeHighRisk);
        var previewText = FormatExecutionPreview(preview, title);
        _details.Text = previewText;
        if (preview.Included.Count == 0) return;

        var answer = MessageBox.Show(this,
            previewText + Environment.NewLine + Environment.NewLine + "Deseja executar as operações incluídas?",
            "Confirmar manutenção", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        _analysisRunning = true;
        _analysisCancellation = new CancellationTokenSource();
        _analyze.Enabled = false;
        _executeSelected.Enabled = _executeCategory.Enabled = _executeAll.Enabled = false;
        _cancel.Enabled = true;
        _progressBar.Visible = true;
        _progressLabel.Text = "Execução iniciada após confirmação.";
        _result.Text = previewText + Environment.NewLine + Environment.NewLine + "Execução em andamento…";
        var progress = new Progress<MaintenanceExecutionProgress>(value =>
        {
            if (!IsDisposed && !Disposing)
                _progressLabel.Text = $"{value.CompletedOperations}/{value.TotalOperations} · {value.Message}";
        });
        try
        {
            var batch = await _executionRegistry.ExecuteSequentiallyAsync(preview,
                new MaintenanceExecutionApproval(preview.Id, DateTimeOffset.UtcNow), progress, _analysisCancellation.Token);
            if (IsDisposed || Disposing) return;
            var historyFailures = new List<string>();
            foreach (var result in batch.Operations)
            {
                try { _historyStore.Add(MaintenanceHistoryEntryFactory.FromResult(result)); }
                catch (Exception ex) { historyFailures.Add(ex.Message); }
            }
            LoadHistory();
            var summary = new List<string>
            {
                $"Resultado consolidado: {batch.Status}",
                $"Operações incluídas: {preview.Included.Count}",
                $"Operações com resultado: {batch.Operations.Count}",
                $"Itens afetados: {batch.Operations.Sum(item => item.ItemsAffected):N0}",
                $"Espaço recuperado: {FormatBytes(batch.Operations.Sum(item => item.BytesRecovered))}",
                $"Erros: {batch.Operations.Sum(item => item.Errors.Count):N0}",
                $"Duração: {batch.Duration.TotalSeconds:F2} s"
            };
            summary.AddRange(historyFailures.Select(error => "Falha ao gravar histórico: " + error));
            _result.Text = string.Join(Environment.NewLine, summary);
            _progressLabel.Text = batch.Status.ToString();
            _analysisFiles.Text = $"Itens afetados: {batch.Operations.Sum(item => item.ItemsAffected):N0}";
            _analysisBytes.Text = $"Recuperado: {FormatBytes(batch.Operations.Sum(item => item.BytesRecovered))}";
            _analysisFailures.Text = $"Erros: {batch.Operations.Sum(item => item.Errors.Count):N0}";
            _analysisDuration.Text = $"Duração: {batch.Duration.TotalSeconds:F2} s";
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing)
            {
                _result.Text = "A execução não pôde ser concluída: " + ex.Message;
                _progressLabel.Text = "Execução com erro";
            }
        }
        finally
        {
            _analysisCancellation.Dispose();
            _analysisCancellation = null;
            _analysisRunning = false;
            if (!IsDisposed && !Disposing)
            {
                _cancel.Enabled = false;
                _progressBar.Visible = false;
                UpdateSelection();
            }
        }
    }

    private async Task RunAnalysisAsync()
    {
        if (_analysisRunning || SelectedOperation is not { CanAnalyze: true, Availability: MaintenanceAvailability.Available } operation) return;

        _analysisRunning = true;
        _analysisCancellation = new CancellationTokenSource();
        _analyze.Enabled = false;
        _executeSelected.Enabled = _executeCategory.Enabled = _executeAll.Enabled = false;
        _cancel.Enabled = true;
        _progressBar.Visible = true;
        _progressLabel.Text = "Analisando em modo somente leitura; é possível cancelar.";
        _result.Text = $"Pasta analisada: {_analysisService.TargetDirectory}{Environment.NewLine}A análise não altera arquivos.";
        UpdateSelection();

        var progress = new Progress<MaintenanceAnalysisProgress>(value =>
        {
            if (!IsDisposed && !Disposing)
                _progressLabel.Text = $"Analisados: {value.FilesIdentified:N0} arquivos em {value.DirectoriesVisited:N0} pastas; {FormatBytes(value.PotentialBytes)} potencialmente recuperáveis.";
        });
        try
        {
            var result = await _analysisService.AnalyzeAsync(progress, _analysisCancellation.Token);
            if (IsDisposed || Disposing) return;
            _result.Text = FormatResult(result);
            _progressLabel.Text = ProgressSummary(result);
            UpdateAnalysisMetrics(result);
            try
            {
                _historyStore.Add(MaintenanceHistoryEntryFactory.FromResult(result));
                LoadHistory();
            }
            catch (Exception ex) { _result.AppendText(Environment.NewLine + Environment.NewLine + "Não foi possível gravar o histórico local: " + ex.Message); }
        }
        catch (Exception ex)
        {
            if (IsDisposed || Disposing) return;
            var failure = new MaintenanceAnalysisResult(UserTempAnalyzer.OperationId, DateTimeOffset.Now, TimeSpan.Zero,
                MaintenanceAnalysisStatus.Error, 0, 0, 1, 0);
            _result.Text = $"Falha inesperada durante a análise somente leitura.{Environment.NewLine}{ex.Message}";
            _progressLabel.Text = ProgressSummary(failure);
            UpdateAnalysisMetrics(failure);
            try
            {
                _historyStore.Add(MaintenanceHistoryEntryFactory.FromResult(failure));
                LoadHistory();
            }
            catch (Exception historyException) { _result.AppendText(Environment.NewLine + "Falha ao gravar o erro no histórico local: " + historyException.Message); }
        }
        finally
        {
            _analysisCancellation.Dispose();
            _analysisCancellation = null;
            _analysisRunning = false;
            if (!IsDisposed && !Disposing)
            {
                _cancel.Enabled = false;
                _progressBar.Visible = false;
                UpdateSelection();
            }
        }
    }

    private void LoadHistory()
    {
        try
        {
            _history.BeginUpdate();
            _history.Items.Clear();
            foreach (var entry in _historyStore.Load())
            {
                var operation = MaintenanceCatalog.Operations.FirstOrDefault(item => item.Id == entry.OperationId)?.Name ?? entry.OperationId;
                var action = entry.Action == MaintenanceActionType.Analysis ? "Análise" : "Execução";
                var items = entry.Action == MaintenanceActionType.Execution ? entry.ItemsAffected?.ToString("N0", CultureInfo.CurrentCulture) ?? "—" : entry.FilesIdentified.ToString("N0", CultureInfo.CurrentCulture);
                var bytes = entry.Action == MaintenanceActionType.Execution ? FormatBytes(entry.BytesRecovered ?? 0) : FormatBytes(entry.PotentialBytes) + " potencial";
                var row = new ListViewItem(entry.StartedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
                row.SubItems.Add(operation);
                row.SubItems.Add(action);
                row.SubItems.Add(AvailabilityStatus(entry.Status));
                row.SubItems.Add(items);
                row.SubItems.Add(bytes);
                row.SubItems.Add(entry.Failures.ToString("N0", CultureInfo.CurrentCulture));
                _history.Items.Add(row);
            }
        }
        catch (Exception ex)
        {
            _history.Items.Clear();
            var row = new ListViewItem(["Histórico indisponível", ex.Message, "—", "Erro", "—", "—", "—"]);
            _history.Items.Add(row);
        }
        finally { _history.EndUpdate(); }
    }

    private string FormatResult(MaintenanceAnalysisResult result)
    {
        var status = result.Status switch
        {
            MaintenanceAnalysisStatus.Completed => "Análise concluída",
            MaintenanceAnalysisStatus.Empty => "Análise concluída — nenhum arquivo elegível encontrado",
            MaintenanceAnalysisStatus.Partial => "Análise parcialmente concluída",
            MaintenanceAnalysisStatus.Cancelled => "Análise cancelada",
            _ => "Análise com erro"
        };

        return string.Join(Environment.NewLine,
            status,
            $"Pasta analisada: {_analysisService.TargetDirectory}",
            $"Início: {result.StartedAt.ToLocalTime().ToString("G", CultureInfo.CurrentCulture)}",
            $"Duração: {result.Duration.TotalSeconds:F2} s",
            $"Arquivos identificados: {result.FilesIdentified:N0}",
            $"Espaço potencialmente recuperável: {FormatBytes(result.PotentialBytes)}",
            $"Falhas de leitura/acesso: {result.Failures:N0}",
            $"Links simbólicos/junctions ignorados: {result.ReparsePointsSkipped:N0}",
            "Nenhum arquivo foi aberto para leitura de conteúdo, alterado, movido ou excluído.");
    }

    private void UpdateAnalysisMetrics(MaintenanceAnalysisResult result)
    {
        _analysisFiles.Text = $"Arquivos analisados: {result.FilesIdentified:N0}";
        _analysisBytes.Text = $"Potencial: {FormatBytes(result.PotentialBytes)}";
        _analysisFailures.Text = $"Falhas: {result.Failures:N0}";
        _analysisDuration.Text = $"Duração: {result.Duration.TotalSeconds:F2} s";
    }

    private static string ProgressSummary(MaintenanceAnalysisResult result) => result.Status switch
    {
        MaintenanceAnalysisStatus.Completed => "Análise concluída",
        MaintenanceAnalysisStatus.Empty => "Análise concluída — nenhum arquivo encontrado",
        MaintenanceAnalysisStatus.Partial => "Parcial — há falhas de leitura/acesso",
        MaintenanceAnalysisStatus.Cancelled => "Análise cancelada pelo usuário",
        _ => "Erro durante a análise"
    };

    private static string AvailabilityStatus(MaintenanceAnalysisStatus status) => status switch
    {
        MaintenanceAnalysisStatus.Completed => "Concluída",
        MaintenanceAnalysisStatus.Empty => "Vazia",
        MaintenanceAnalysisStatus.Partial => "Parcial",
        MaintenanceAnalysisStatus.Cancelled => "Cancelada",
        _ => "Erro"
    };

    private static string AvailabilityLabel(MaintenanceAvailability availability) => availability switch
    {
        MaintenanceAvailability.Available => "Disponível para análise",
        MaintenanceAvailability.Planned => "Planejada",
        _ => "Indisponível"
    };

    private static string CategoryLabel(MaintenanceCategory category) => category switch
    {
        MaintenanceCategory.BasicCleanup => "Limpeza básica",
        MaintenanceCategory.Applications => "Aplicativos",
        MaintenanceCategory.Windows => "Windows",
        _ => "Diagnóstico"
    };

    private static string RiskLabel(MaintenanceRisk risk) => risk switch
    {
        MaintenanceRisk.Low => "Baixo",
        MaintenanceRisk.Medium => "Médio",
        _ => "Alto"
    };

    private static string FormatBytes(long bytes) => bytes < 1024 ? $"{bytes} bytes" : bytes < 1024 * 1024
        ? $"{bytes / 1024d:N1} KB" : bytes < 1024L * 1024 * 1024
            ? $"{bytes / (1024d * 1024):N1} MB" : $"{bytes / (1024d * 1024 * 1024):N2} GB";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _analysisCancellation?.Cancel();
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
