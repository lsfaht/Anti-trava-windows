namespace KeepAwake.Windows;

internal sealed partial class MainForm
{
    private void BuildApplicationShell()
    {
        _shellLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = AppTheme.Background, Margin = Padding.Empty };
        _shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 252));
        _shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var sidebar = BuildSidebar();
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.Background, Margin = Padding.Empty };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(BuildPageHeader(), 0, 0);
        _pageHost.BackColor = AppTheme.Background;
        content.Controls.Add(_pageHost, 0, 1);
        _shellLayout.Controls.Add(sidebar, 0, 0);
        _shellLayout.Controls.Add(content, 1, 0);
        Controls.Add(_shellLayout);

        AddPage("Início", BuildDashboardPage());
        AddPage("Keep Awake", BuildKeepAwakePage());
        AddPage("Janelas e sessão", BuildWindowsPage());
        AddPage("Central de Manutenção", BuildMaintenancePage());
        AddPage("Diagnóstico", BuildDiagnosticsPage());
        AddPage("Configurações", BuildSettingsPage());

        AppTheme.Apply(this);
        NavigateToPage("Início");
        Resize += (_, _) => UpdateResponsiveShell();
        UpdateResponsiveShell();
    }

    private Control BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AppTheme.Surface,
            Padding = new Padding(14, 18, 14, 14),
            Margin = Padding.Empty
        };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

        var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.Surface, Padding = new Padding(4, 5, 0, 0) };
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _brandTitle.Text = "●  Windows Keep Awake";
        _brandTitle.Font = new Font("Segoe UI Semibold", 13, FontStyle.Regular);
        _brandTitle.ForeColor = AppTheme.GreenLight;
        _brandTitle.Margin = new Padding(0, 3, 0, 3);
        _brandDescription.Text = "Controle e diagnóstico local";
        _brandDescription.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        _brandDescription.ForeColor = AppTheme.Muted;
        _brandDescription.Margin = new Padding(0, 3, 0, 3);
        brand.Controls.Add(_brandTitle, 0, 0);
        brand.Controls.Add(_brandDescription, 0, 1);
        sidebar.Controls.Add(brand, 0, 0);

        var navigation = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, RowCount = 6, BackColor = AppTheme.Surface, AutoSize = true };
        _navigationToolTip = new ToolTip { ShowAlways = true, InitialDelay = 450, ReshowDelay = 100 };
        var pageNames = new[] { "Início", "Keep Awake", "Janelas e sessão", "Central de Manutenção", "Diagnóstico", "Configurações" };
        for (var index = 0; index < pageNames.Length; index++)
        {
            var pageName = pageNames[index];
            navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            var button = new Button
            {
                Text = NavigationGlyph(pageName) + "   " + pageName,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = AppTheme.Surface,
                ForeColor = AppTheme.Muted,
                Font = _navigationFullFont,
                Padding = new Padding(10, 0, 4, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                AccessibleName = pageName
            };
            _navigationToolTip.SetToolTip(button, pageName);
            button.Click += (_, _) => NavigateToPage(pageName);
            _navigationButtons.Add(pageName, button);
            navigation.Controls.Add(button, 0, index);
        }
        sidebar.Controls.Add(navigation, 0, 1);

        var statusCard = AppTheme.CreateCard();
        statusCard.Dock = DockStyle.Fill;
        statusCard.Padding = new Padding(8, 5, 8, 5);
        statusCard.Margin = Padding.Empty;
        _powerIndicator.Dock = DockStyle.Fill;
        statusCard.Controls.Add(_powerIndicator);
        sidebar.Controls.Add(statusCard, 0, 2);
        _sidebarLayout = sidebar;
        return sidebar;
    }

    private Control BuildPageHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(26, 14, 20, 8),
            BackColor = AppTheme.Background
        };
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _pageTitle.Font = new Font("Segoe UI Semibold", 18, FontStyle.Regular);
        _pageTitle.ForeColor = AppTheme.Text;
        _pageTitle.Margin = new Padding(0, 0, 0, 2);
        _pageDescription.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        _pageDescription.ForeColor = AppTheme.Muted;
        header.Controls.Add(_pageTitle, 0, 0);
        header.Controls.Add(_pageDescription, 0, 1);
        return header;
    }

    private Panel BuildDashboardPage()
    {
        var page = CreatePage();
        var stack = CreateStack(page);
        var welcome = CreateWelcomeCard();
        welcome.Dock = DockStyle.Top;
        stack.Controls.Add(welcome, 0, 0);

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight, WrapContents = true,
            BackColor = AppTheme.Background, Margin = new Padding(0, 0, 0, 14), Padding = Padding.Empty
        };
        var metrics = new[]
        {
            CreateMetricCard("Keep Awake", _dashboardAwakeState),
            CreateMetricCard("Modo de apresentação", _dashboardPresentationState),
            CreateMetricCard("Tela", _dashboardDisplayState),
            CreateMetricCard("Duração", _dashboardDuration),
            CreateMetricCard("Inicialização", _dashboardStartup),
            CreateSafetyCard()
        };
        foreach (var metric in metrics) cards.Controls.Add(metric);
        cards.Resize += (_, _) => ResizeDashboardCards(cards);
        ResizeDashboardCards(cards);
        stack.Controls.Add(cards, 0, 1);

        var shortcuts = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, WrapContents = true, BackColor = AppTheme.Background, Margin = Padding.Empty };
        var keepAwake = CreateActionButton("Abrir Keep Awake", false);
        keepAwake.Click += (_, _) => NavigateToPage("Keep Awake");
        var maintenance = CreateActionButton("Abrir Central de Manutenção", true);
        maintenance.Click += (_, _) => NavigateToPage("Central de Manutenção");
        var diagnostics = CreateActionButton("Ver diagnóstico", false);
        diagnostics.Click += (_, _) => NavigateToPage("Diagnóstico");
        shortcuts.Controls.AddRange([keepAwake, maintenance, diagnostics]);
        stack.Controls.Add(shortcuts, 0, 2);
        return page;
    }

    private Control CreateWelcomeCard()
    {
        var card = AppTheme.CreateCard();
        card.MinimumSize = new Size(0, 108);
        card.AutoSize = true;
        card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        card.Padding = new Padding(18, 14, 18, 14);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true, BackColor = AppTheme.Card, Margin = Padding.Empty };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = AppTheme.CreateLabel("Seu computador, sob controle", 16, true);
        heading.Dock = DockStyle.Fill;
        var description = AppTheme.CreateLabel("Confira os estados ativos e acesse rapidamente as ferramentas locais.", 9.5F, false, AppTheme.Muted);
        description.Dock = DockStyle.Fill;
        description.Margin = new Padding(0, 8, 0, 0);
        content.Controls.Add(heading, 0, 0);
        content.Controls.Add(description, 0, 1);
        card.Controls.Add(content);
        return card;
    }

    private static Control CreateMetricCard(string title, Label value)
    {
        var card = AppTheme.CreateCard();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 12, 12);
        card.MinimumSize = new Size(0, 104);
        card.Padding = new Padding(14, 12, 14, 10);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.Card, Margin = Padding.Empty };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = AppTheme.CreateLabel(title, 9, false, AppTheme.Muted);
        heading.Dock = DockStyle.Fill;
        heading.AutoEllipsis = true;
        value.Dock = DockStyle.Fill;
        value.AutoEllipsis = true;
        value.TextAlign = ContentAlignment.MiddleLeft;
        value.Font = new Font("Segoe UI Semibold", 13, FontStyle.Regular);
        value.ForeColor = AppTheme.Text;
        content.Controls.Add(heading, 0, 0);
        content.Controls.Add(value, 0, 1);
        card.Controls.Add(content);
        return card;
    }

    private static Control CreateSafetyCard()
    {
        var card = AppTheme.CreateCard();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 0, 12);
        card.MinimumSize = new Size(0, 104);
        card.Padding = new Padding(14, 12, 14, 10);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.Card, Margin = Padding.Empty };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var heading = AppTheme.CreateLabel("Uso responsável", 9, false, AppTheme.Warning);
        heading.Dock = DockStyle.Fill;
        var text = AppTheme.CreateLabel("O pedido de energia não substitui as políticas de segurança da sua organização.", 9.5F, false, AppTheme.Muted);
        text.Dock = DockStyle.Fill;
        text.AutoSize = false;
        content.Controls.Add(heading, 0, 0);
        content.Controls.Add(text, 0, 1);
        card.Controls.Add(content);
        return card;
    }

    private static void ResizeDashboardCards(FlowLayoutPanel grid)
    {
        if (grid.Controls.Count == 0) return;
        var width = Math.Max(320, grid.ClientSize.Width);
        var columns = Math.Clamp((width + 12) / 300, 1, 3);
        var cardWidth = Math.Max(260, (width - (columns - 1) * 12 - 8) / columns);
        foreach (Control child in grid.Controls) child.Width = cardWidth;
    }

    private Panel BuildKeepAwakePage()
    {
        var page = CreatePage();
        var stack = CreateStack(page);
        var stateCard = CreateSectionCard("Estado e ações", 174);
        var stateLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.Card, Padding = new Padding(4, 4, 4, 0) };
        stateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        stateLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        _status.Font = new Font("Segoe UI Semibold", 17, FontStyle.Regular);
        _status.ForeColor = AppTheme.Muted;
        _status.Margin = new Padding(4, 4, 4, 6);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, BackColor = AppTheme.Card, Padding = new Padding(0, 5, 0, 0) };
        AppTheme.StyleButton(_toggle, true);
        AppTheme.StyleButton(_presentationToggle, false);
        _toggle.AutoSize = true;
        _presentationToggle.AutoSize = true;
        _toggle.MinimumSize = new Size(172, 40);
        _presentationToggle.MinimumSize = new Size(220, 40);
        _presentationStatus.ForeColor = AppTheme.Muted;
        _presentationStatus.Margin = new Padding(12, 12, 12, 6);
        actions.Controls.AddRange([_toggle, _presentationToggle, _presentationStatus]);
        stateLayout.Controls.Add(_status, 0, 0);
        stateLayout.Controls.Add(actions, 0, 1);
        stateCard.BodyHost!.Controls.Add(stateLayout);
        stack.Controls.Add(stateCard, 0, 0);

        var settingsCard = CreateSectionCard("Preferências da solicitação", 160);
        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = AppTheme.Card, Padding = new Padding(4, 4, 4, 0) };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        settings.Controls.Add(AppTheme.CreateLabel("Duração", 9, false, AppTheme.Muted), 0, 0);
        settings.Controls.Add(AppTheme.CreateLabel("Tela", 9, false, AppTheme.Muted), 1, 0);
        _duration.Width = 200;
        _duration.Dock = DockStyle.Fill;
        _duration.Margin = Padding.Empty;
        _duration.Height = 36;
        _display.AutoSize = true;
        _display.Margin = new Padding(0, 8, 8, 0);
        settings.Controls.Add(_duration, 0, 1);
        settings.Controls.Add(_display, 1, 1);
        settingsCard.BodyHost!.Controls.Add(settings);
        stack.Controls.Add(settingsCard, 0, 1);

        var note = AppTheme.CreateLabel("O modo de apresentação solicita que o Windows mantenha a tela e o sistema ativos. Ele não substitui políticas de segurança, bloqueios definidos pela organização ou ações manuais de bloqueio.", 9.5F, false, AppTheme.Warning);
        note.Dock = DockStyle.Fill;
        note.AutoSize = false;
        note.Padding = new Padding(4, 6, 4, 6);
        var noteCard = CreateSectionCard("Atenção", 112);
        noteCard.BodyHost!.Controls.Add(note);
        stack.Controls.Add(noteCard, 0, 2);
        return page;
    }

    private Panel BuildWindowsPage()
    {
        var page = CreatePage();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(22, 6, 22, 18), BackColor = AppTheme.Background };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _session.Dock = DockStyle.Fill;
        _session.AutoSize = false;
        _session.Padding = new Padding(12, 8, 12, 8);
        _session.TextAlign = ContentAlignment.MiddleLeft;
        _session.ForeColor = AppTheme.Muted;
        _session.BackColor = AppTheme.Card;
        layout.Controls.Add(_session, 0, 0);

        var filterRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = AppTheme.Background, Padding = new Padding(0, 8, 0, 8) };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var filterLabel = AppTheme.CreateLabel("Filtrar por processo ou título", 9.5F, false, AppTheme.Muted);
        filterLabel.Dock = DockStyle.Fill;
        filterLabel.AutoSize = false;
        filterLabel.Width = 230;
        filterLabel.TextAlign = ContentAlignment.MiddleLeft;
        _filters.Dock = DockStyle.Fill;
        _filters.Margin = new Padding(0);
        filterRow.Controls.Add(filterLabel, 0, 0);
        filterRow.Controls.Add(_filters, 1, 0);
        layout.Controls.Add(filterRow, 0, 1);

        _windows.Dock = DockStyle.Fill;
        _windows.Margin = Padding.Empty;
        _windows.Scrollable = true;
        var listCard = AppTheme.CreateCard();
        listCard.Dock = DockStyle.Fill;
        listCard.Padding = new Padding(8);
        listCard.Controls.Add(_windows);
        layout.Controls.Add(listCard, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private Panel BuildMaintenancePage()
    {
        var page = CreatePage();
        page.AutoScroll = false;
        _maintenanceCenter.Dock = DockStyle.Fill;
        _maintenanceCenter.Margin = Padding.Empty;
        page.Padding = new Padding(10, 0, 10, 10);
        page.Controls.Add(_maintenanceCenter);
        return page;
    }

    private Panel BuildDiagnosticsPage()
    {
        var page = CreatePage();
        var stack = CreateStack(page);
        var statusCard = CreateSectionCard("Estado do relatório", 76);
        _diagnosticState.Dock = DockStyle.Fill;
        _diagnosticState.TextAlign = ContentAlignment.MiddleLeft;
        _diagnosticState.ForeColor = AppTheme.Muted;
        statusCard.BodyHost!.Controls.Add(_diagnosticState);
        stack.Controls.Add(statusCard, 0, 0);

        var reportCard = CreateSectionCard("Informações técnicas, avisos e limitações", 440);
        var reportLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.Card, Padding = new Padding(2) };
        reportLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        reportLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var refresh = CreateActionButton("Atualizar relatório", true);
        refresh.Click += (_, _) =>
        {
            _report.Text = BuildReport();
            _diagnosticState.Text = "Relatório atualizado em " + DateTimeOffset.Now.ToLocalTime().ToString("g");
        };
        var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = AppTheme.Card, WrapContents = false };
        commands.Controls.Add(refresh);
        _report.Dock = DockStyle.Fill;
        _report.Margin = Padding.Empty;
        _report.WordWrap = true;
        _report.ScrollBars = ScrollBars.Vertical;
        reportLayout.Controls.Add(commands, 0, 0);
        reportLayout.Controls.Add(_report, 0, 1);
        reportCard.BodyHost!.Controls.Add(reportLayout);
        stack.Controls.Add(reportCard, 0, 1);
        return page;
    }

    private Panel BuildSettingsPage()
    {
        var page = CreatePage();
        var stack = CreateStack(page);
        var startupCard = CreateSectionCard("Inicialização", 150);
        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.Card, Padding = new Padding(4, 4, 4, 0) };
        settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _startup.AutoSize = true;
        settings.Controls.Add(_startup, 0, 0);
        var note = AppTheme.CreateLabel("A opção registra ou remove uma entrada somente no perfil do usuário. Ela não solicita elevação administrativa.", 9.5F, false, AppTheme.Muted);
        note.AutoSize = false;
        note.Dock = DockStyle.Fill;
        note.Margin = new Padding(0, 4, 0, 0);
        settings.Controls.Add(note, 0, 1);
        startupCard.BodyHost!.Controls.Add(settings);
        stack.Controls.Add(startupCard, 0, 0);

        var helpCard = CreateSectionCard("Onde encontrar as outras preferências", 128);
        var links = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = AppTheme.Card, WrapContents = true };
        var power = CreateActionButton("Preferências de energia", false);
        power.Click += (_, _) => NavigateToPage("Keep Awake");
        var windows = CreateActionButton("Filtros de janelas", false);
        windows.Click += (_, _) => NavigateToPage("Janelas e sessão");
        links.Controls.AddRange([power, windows]);
        helpCard.BodyHost!.Controls.Add(links);
        stack.Controls.Add(helpCard, 0, 1);
        return page;
    }

    private Panel CreatePage() => new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.Background, Visible = false };

    private static TableLayoutPanel CreateStack(Panel page)
    {
        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            BackColor = AppTheme.Background,
            Padding = new Padding(18, 6, 18, 18),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        page.Controls.Add(stack);
        void ResizeStack()
        {
            var scrollbar = page.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
            var targetWidth = Math.Max(320, page.ClientSize.Width - scrollbar);
            if (stack.Width != targetWidth) stack.Width = targetWidth;
        }
        page.Resize += (_, _) => ResizeStack();
        page.Layout += (_, _) => ResizeStack();
        ResizeStack();
        return stack;
    }

    private static CardPanel CreateSectionCard(string title, int height)
    {
        var card = AppTheme.CreateCard();
        card.Dock = DockStyle.Fill;
        card.Height = height;
        card.MinimumSize = new Size(0, height);
        card.AutoSize = true;
        card.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        var heading = AppTheme.CreateLabel(title, 10, true, AppTheme.Muted);
        heading.Dock = DockStyle.Top;
        heading.Height = 25;
        var body = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Card, Padding = new Padding(0, 5, 0, 0) };
        card.Controls.Add(body);
        card.Controls.Add(heading);
        heading.BringToFront();
        card.BodyHost = body;
        return card;
    }

    private static Button CreateActionButton(string text, bool primary)
    {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        AppTheme.StyleButton(button, primary);
        return button;
    }

    private void AddPage(string name, Panel page)
    {
        page.Name = "page" + string.Concat(name.Where(char.IsLetterOrDigit));
        _pages.Add(name, page);
        _pageHost.Controls.Add(page);
    }

    internal void NavigateToPage(string pageName)
    {
        if (!_pages.TryGetValue(pageName, out var page)) throw new ArgumentOutOfRangeException(nameof(pageName), pageName, "Seção de navegação desconhecida.");
        _currentPage = pageName;
        foreach (var pair in _pages) pair.Value.Visible = pair.Key == pageName;
        page.BringToFront();
        _pageTitle.Text = pageName;
        _pageDescription.Text = pageName switch
        {
            "Início" => "Visão geral do estado atual e atalhos para as ferramentas.",
            "Keep Awake" => "Controle explícito das solicitações oficiais de energia do Windows.",
            "Janelas e sessão" => "Estado da sessão e janelas visíveis; nenhum conteúdo é lido.",
            "Central de Manutenção" => "Operações locais organizadas; somente a análise TEMP está disponível.",
            "Diagnóstico" => "Consultas somente leitura, avisos e limites conhecidos.",
            _ => "Preferências que já são suportadas pelo aplicativo."
        };
        foreach (var pair in _navigationButtons)
        {
            var selected = pair.Key == pageName;
            pair.Value.BackColor = selected ? AppTheme.Selected : AppTheme.Surface;
            pair.Value.ForeColor = selected ? AppTheme.GreenLight : AppTheme.Muted;
            pair.Value.Font = _navigationCompact
                ? selected ? _navigationCompactSelectedFont : _navigationCompactFont
                : selected ? _navigationFullSelectedFont : _navigationFullFont;
            pair.Value.FlatAppearance.MouseOverBackColor = selected ? AppTheme.Selected : AppTheme.Hover;
        }
        if (pageName == "Central de Manutenção") _maintenanceCenter.Invalidate();
        UpdateResponsiveShell();
    }

    private void UpdateDashboard()
    {
        _dashboardAwakeState.Text = _status.Text;
        _dashboardAwakeState.ForeColor = _request.IsEnabled ? AppTheme.GreenLight : AppTheme.Muted;
        _dashboardPresentationState.Text = _presentationStatus.Text.Replace("Modo de apresentação: ", "", StringComparison.Ordinal);
        _dashboardPresentationState.ForeColor = _presentationRequest?.IsEnabled == true ? AppTheme.GreenLight : AppTheme.Muted;
        _dashboardDisplayState.Text = _display.Checked ? "Solicitar tela ligada" : "Sem solicitação de tela";
        _dashboardDuration.Text = _duration.SelectedItem?.ToString() ?? "Não configurada";
        _dashboardStartup.Text = _startup.Checked ? "Ativa para este usuário" : "Desativada";
        _powerIndicator.SetKeepAwakeActive(_request.IsEnabled || _presentationRequest?.IsEnabled == true);
        UpdateResponsiveShell();
    }

    private void ResizeWindowColumns()
    {
        if (_windows.Columns.Count < 3) return;
        var available = Math.Max(300, _windows.ClientSize.Width - 8);
        _windows.Columns[0].Width = Math.Min(180, available / 4);
        _windows.Columns[2].Width = Math.Min(100, available / 5);
        _windows.Columns[1].Width = Math.Max(120, available - _windows.Columns[0].Width - _windows.Columns[2].Width);
    }

    private void UpdateResponsiveShell()
    {
        if (_shellLayout is null || _sidebarLayout is null) return;
        var compact = ClientSize.Width < 1120;
        var sidebarWidth = compact ? 72 : 252;
        if (!_responsiveShellInitialized || _navigationCompact != compact)
        {
            _shellLayout.ColumnStyles[0].Width = sidebarWidth;
            _sidebarLayout.Padding = compact ? new Padding(8, 18, 8, 12) : new Padding(14, 18, 14, 14);
            _brandTitle.Text = compact ? "●" : "●  Windows Keep Awake";
            _brandDescription.Visible = !compact;
            _navigationToolTip?.SetToolTip(_brandTitle, "Windows Keep Awake — Controle e diagnóstico local");
            foreach (var pair in _navigationButtons)
            {
                pair.Value.Text = compact ? NavigationGlyph(pair.Key) : NavigationGlyph(pair.Key) + "   " + pair.Key;
                pair.Value.TextAlign = compact ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
                pair.Value.Padding = compact ? new Padding(0) : new Padding(10, 0, 4, 0);
                pair.Value.Font = compact
                    ? pair.Key == _currentPage ? _navigationCompactSelectedFont : _navigationCompactFont
                    : pair.Key == _currentPage ? _navigationFullSelectedFont : _navigationFullFont;
                if (_navigationToolTip is not null) _navigationToolTip.SetToolTip(pair.Value, pair.Key);
            }
            _navigationCompact = compact;
            _responsiveShellInitialized = true;
        }
        _powerIndicator.SetCompact(compact);
    }

    private static string NavigationGlyph(string page) => page switch
    {
        "Início" => "⌂",
        "Keep Awake" => "◉",
        "Janelas e sessão" => "▣",
        "Central de Manutenção" => "⚙",
        "Diagnóstico" => "⌕",
        _ => "☰"
    };
}
