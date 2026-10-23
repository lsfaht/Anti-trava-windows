using System.Diagnostics;
using KeepAwake.Core;
using KeepAwake.Windows;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Contains("--ui-preview", StringComparer.OrdinalIgnoreCase))
        {
            using var preview = new MainForm(
                new PowerRequest(new FakeApi()),
                _ => { },
                _ => { },
                autoActivatePresentation: false,
                maintenanceAnalysisService: new WindowsUserTempAnalysisService(),
                maintenanceHistoryStore: new MemoryMaintenanceHistoryStore());
            var pageArgument = args.FirstOrDefault(argument => argument.StartsWith("--page=", StringComparison.OrdinalIgnoreCase));
            if (pageArgument is not null)
            {
                var pageName = pageArgument["--page=".Length..];
                preview.Shown += (_, _) => preview.NavigateToPage(pageName);
            }
            Application.Run(preview);
            return 0;
        }
        var checks = new (string Name, Action Run)[]
        {
            ("request enables system and display", EnableSetsBothFlags),
            ("request can leave display power unchanged", EnableSetsSystemFlagOnly),
            ("repeated enable with same options is idempotent", RepeatedEnableIsIdempotent),
            ("changing display option updates the request", DisplayOptionCanChangeWhileEnabled),
            ("disable clears request exactly once", DisableIsIdempotent),
            ("API activation errors are surfaced", EnableFailureIsSurfaced),
            ("failed release remains retryable", FailedReleaseCanBeRetried),
            ("dispose releases enabled request", DisposeReleasesRequest),
            ("dispose does not repeat successful release", DisposeIsIdempotent),
            ("presentation mode requests system and display", PresentationModeRequestsSystemAndDisplay),
            ("presentation mode release is idempotent", PresentationModeReleaseIsIdempotent),
            ("presentation mode activation failure rolls back", PresentationModeActivationFailureRollsBack),
            ("presentation mode release failure remains retryable", PresentationModeReleaseFailureCanBeRetried),
            ("presentation mode dispose releases requests and handle", PresentationModeDisposeReleasesRequests),
            ("WinForms presentation mode toggles and releases on close", FormTogglesPresentationMode),
            ("WinForms reports presentation API failures", FormReportsPresentationApiFailure),
            ("closing an active presentation mode clears requests", FormCloseReleasesPresentationMode),
            ("Windows PowerSetRequest activation and clearing work", RealPresentationRequestCanBeSetAndCleared),
            ("real presentation request is released by closing the form", RealPresentationRequestIsReleasedOnFormClose),
            ("empty window filter keeps all rows", EmptyFilterKeepsAll),
            ("window filter matches process and title case-insensitively", WindowFilterMatchesProcessAndTitle),
            ("nonmatching window filter returns no rows", WindowFilterCanReturnNoRows),
            ("WinForms opens inactive and toggles active/inactive", FormOpensAndToggles),
            ("tray icon is visible and uses the embedded application icon", TrayIconIsCreatedWithEmbeddedIcon),
            ("minimize hides the window and tray double-click restores it", MinimizeAndTrayRestore),
            ("main navigation exposes all implemented screens", MainNavigationShowsAllScreens),
            ("dashboard mirrors the current power and preference states", DashboardTracksCurrentState),
            ("power snapshots distinguish AC, battery, unknown and unavailable states", PowerSnapshotsAreMapped),
            ("power indicator updates on changes and compacts without conflating Keep Awake", PowerIndicatorTracksIndependentStates),
            ("GetSystemPowerStatus returns a real Windows power state", RealWindowsPowerStatusIsReadable),
            ("main navigation changes the visible page", NavigationChangesVisiblePage),
            ("maintenance category filter and details stay connected", MaintenanceCategoryFilterAndDetailsWork),
            ("maintenance operation search filters results and restores them when cleared", MaintenanceOperationSearchWorks),
            ("maintenance list columns adapt to available width", MaintenanceListColumnsFitResponsiveWidth),
            ("main pages remain hosted and resizable at target window sizes", MainPagesResizeAtTargetSizes),
            ("maintenance execution buttons stay safe without a registered executor", MaintenanceExecutionControlsRemainSafe),
            ("maintenance execution preview excludes unavailable and high-risk batch work", MaintenanceExecutionPreviewIsSafe),
            ("maintenance executor requires approval for the exact preview", MaintenanceExecutionRequiresMatchingApproval),
            ("maintenance history distinguishes analysis from execution", MaintenanceHistoryDistinguishesActions),
            ("all pages avoid sibling overlap and horizontal control overflow", ResponsivePagesKeepControlBounds),
            ("tray restore moves an off-screen window back onto a monitor", TrayRestoreRecoversOffScreenWindow),
            ("second-instance activation signal reaches the primary instance", SecondInstanceActivationSignalsPrimary),
            ("closing the window hides it without releasing power requests", ClosingWindowKeepsBackgroundRequests),
            ("tray maintenance commands stay synchronized with the form", TrayMaintenanceCommandsStaySynchronized),
            ("tray presentation command tracks active state", TrayPresentationCommandTracksState),
            ("tray exit releases requests and disposes the tray icon", TrayExitReleasesAndDisposes),
            ("initial options and automatic presentation activation", InitialOptionsAndAutomaticPresentation),
            ("timer expiration releases active requests", TimerExpirationReleasesActiveRequests),
            ("WinForms process/title/no-match/clear filters work", FormFiltersLiveWindows),
            ("WinForms reports and logs API failures", FormReportsApiFailure),
            ("closing a form after release failure logs the error and allows retry", FormCloseLogsReleaseFailure),
            ("closing an active form releases the real Windows request", RealWindowsRequestIsReleasedOnClose),
            ("maintenance catalog lists operations and only user TEMP is available", MaintenanceCatalogHasSafeInitialAvailability),
            ("TEMP analyzer identifies its root and counts files and bytes", TempAnalyzerCountsFilesAndBytes),
            ("TEMP analyzer reports an empty directory", TempAnalyzerReportsEmptyDirectory),
            ("TEMP analyzer reports inaccessible files and partial results", TempAnalyzerReportsAccessFailures),
            ("TEMP analyzer reports directory enumeration failures", TempAnalyzerReportsEnumerationFailures),
            ("TEMP analyzer supports cancellation", TempAnalyzerCanCancel),
            ("TEMP analyzer skips reparse points without traversing them", TempAnalyzerSkipsReparsePoints),
            ("real symbolic link is skipped when Windows permits creation", RealReparseLinkIsSkipped),
            ("TEMP analyzer refuses entries outside the authorized root", TempAnalyzerRejectsOutsideEntries),
            ("TEMP analyzer rejects missing or linked roots", TempAnalyzerRejectsUnsafeRoots),
            ("maintenance history persists records and prunes entries older than 30 days", MaintenanceHistoryPersistsAndPrunes),
            ("maintenance history enforces the record and file-size limits", MaintenanceHistoryEnforcesLimits),
            ("real TEMP analyzer reads isolated files without changing them", RealTempAnalysisIsReadOnly),
            ("TEMP analyzer resource sample over 1000 isolated files", TempAnalyzerReportsResourceSample),
            ("Windows analyzer targets the current user TEMP path", WindowsAnalyzerUsesUserTempPath),
            ("maintenance interface lists planned operations and runs analysis", MaintenanceInterfaceRunsAnalysisAndStoresHistory),
            ("maintenance interface exposes cancellation while a scan is running", MaintenanceInterfaceCanCancelAnalysis)
        };

        var failed = 0;
        foreach (var check in checks)
        {
            try { check.Run(); Console.WriteLine($"PASS {check.Name}"); }
            catch (Exception ex) { failed++; Console.WriteLine($"FAIL {check.Name}: {ex.Message}"); }
        }
        Console.WriteLine($"{checks.Length - failed}/{checks.Length} checks passed");
        return failed == 0 ? 0 : 1;
    }

    private static void EnableSetsBothFlags()
    {
        var api = new FakeApi(); using var request = new PowerRequest(api);
        request.Enable(keepDisplayOn: true);
        Equal(ExecutionFlags.Continuous | ExecutionFlags.SystemRequired | ExecutionFlags.DisplayRequired, api.Calls[0]);
        True(request.IsEnabled);
    }

    private static void EnableSetsSystemFlagOnly()
    {
        var api = new FakeApi(); using var request = new PowerRequest(api);
        request.Enable(keepDisplayOn: false);
        Equal(ExecutionFlags.Continuous | ExecutionFlags.SystemRequired, api.Calls[0]);
    }

    private static void RepeatedEnableIsIdempotent()
    {
        var api = new FakeApi(); using var request = new PowerRequest(api);
        request.Enable(keepDisplayOn: true);
        request.Enable(keepDisplayOn: true);
        Equal(1, api.Calls.Count);
        True(request.IsEnabled);
    }

    private static void DisplayOptionCanChangeWhileEnabled()
    {
        var api = new FakeApi(); using var request = new PowerRequest(api);
        request.Enable(keepDisplayOn: true);
        request.Enable(keepDisplayOn: false);
        Equal(2, api.Calls.Count);
        Equal(ExecutionFlags.Continuous | ExecutionFlags.SystemRequired, api.Calls[1]);
        request.Disable();
    }

    private static void DisableIsIdempotent()
    {
        var api = new FakeApi(); using var request = new PowerRequest(api);
        request.Enable(keepDisplayOn: false);
        request.Disable();
        request.Disable();
        Equal(2, api.Calls.Count);
        Equal(ExecutionFlags.Continuous, api.Calls[1]);
        True(!request.IsEnabled);
    }

    private static void EnableFailureIsSurfaced()
    {
        var request = new PowerRequest(new FakeApi(0));
        Throws<InvalidOperationException>(() => request.Enable(keepDisplayOn: false));
        True(!request.IsEnabled);
    }

    private static void FailedReleaseCanBeRetried()
    {
        var api = new FakeApi(1, 0, 1); var request = new PowerRequest(api);
        request.Enable(keepDisplayOn: false);
        Throws<InvalidOperationException>(request.Dispose);
        True(request.IsEnabled);
        request.Dispose();
        True(!request.IsEnabled);
        Throws<ObjectDisposedException>(() => request.Enable(keepDisplayOn: false));
        Equal(3, api.Calls.Count);
    }

    private static void DisposeReleasesRequest()
    {
        var api = new FakeApi(); var request = new PowerRequest(api);
        request.Enable(keepDisplayOn: false);
        request.Dispose();
        Equal(ExecutionFlags.Continuous, api.Calls[1]);
        True(!request.IsEnabled);
    }

    private static void DisposeIsIdempotent()
    {
        var api = new FakeApi(); var request = new PowerRequest(api);
        request.Enable(keepDisplayOn: false);
        request.Dispose();
        request.Dispose();
        Equal(2, api.Calls.Count);
    }

    private static void PresentationModeRequestsSystemAndDisplay()
    {
        var api = new FakePresentationApi(); using var request = new PresentationModeRequest(api);
        request.Enable();
        Equal("Set:SystemRequired", api.Calls[0]);
        Equal("Set:DisplayRequired", api.Calls[1]);
        True(request.IsEnabled);
    }

    private static void PresentationModeReleaseIsIdempotent()
    {
        var api = new FakePresentationApi(); using var request = new PresentationModeRequest(api);
        request.Enable();
        request.Enable();
        request.Disable();
        request.Disable();
        Equal(4, api.Calls.Count);
        Equal("Clear:DisplayRequired", api.Calls[2]);
        Equal("Clear:SystemRequired", api.Calls[3]);
        True(!request.IsEnabled);
    }

    private static void PresentationModeActivationFailureRollsBack()
    {
        var api = new FakePresentationApi(failOnCalls: [2]); using var request = new PresentationModeRequest(api);
        Throws<InvalidOperationException>(request.Enable);
        Equal("Clear:SystemRequired", api.Calls[2]);
        True(!request.IsEnabled);
    }

    private static void PresentationModeReleaseFailureCanBeRetried()
    {
        var api = new FakePresentationApi(failOnCalls: [3]); using var request = new PresentationModeRequest(api);
        request.Enable();
        Throws<AggregateException>(request.Disable);
        True(request.IsEnabled);
        request.Disable();
        True(!request.IsEnabled);
        Equal("Clear:DisplayRequired", api.Calls[4]);
    }

    private static void PresentationModeDisposeReleasesRequests()
    {
        var api = new FakePresentationApi(); var request = new PresentationModeRequest(api);
        request.Enable();
        request.Dispose();
        True(!request.IsEnabled);
        True(api.IsDisposed);
        Throws<ObjectDisposedException>(request.Enable);
    }

    private static void FormTogglesPresentationMode()
    {
        var logs = new List<string>(); var api = new FakePresentationApi();
        var presentationRequest = new PresentationModeRequest(api);
        var form = new MainForm(new PowerRequest(new FakeApi()), logs.Add, _ => { }, presentationRequest);
        try
        {
            Show(form);
            form.NavigateToPage("Keep Awake");
            Equal("Modo de apresentação: Inativo", form.PresentationStatusText);
            form.PresentationToggleButton.PerformClick(); Application.DoEvents();
            Equal("Modo de apresentação: Ativo", form.PresentationStatusText);
            Equal("Interromper modo de apresentação", form.PresentationToggleText);
            form.PresentationToggleButton.PerformClick(); Application.DoEvents();
            Equal("Modo de apresentação: Inativo", form.PresentationStatusText);
            True(!presentationRequest.IsEnabled);
        }
        finally { Close(form); }
        True(api.IsDisposed);
        True(logs.Any(message => message.Contains("Modo de apresentação ativado", StringComparison.Ordinal)));
    }

    private static void FormCloseReleasesPresentationMode()
    {
        var api = new FakePresentationApi(); var presentationRequest = new PresentationModeRequest(api);
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, presentationRequest);
        Show(form);
        form.NavigateToPage("Keep Awake");
        form.PresentationToggleButton.PerformClick(); Application.DoEvents();
        True(presentationRequest.IsEnabled);
        form.ExitApplication(); Application.DoEvents();
        True(form.IsDisposed);
        True(!presentationRequest.IsEnabled);
        True(api.IsDisposed);
        Equal("Clear:DisplayRequired", api.Calls[2]);
        Equal("Clear:SystemRequired", api.Calls[3]);
    }

    private static void FormReportsPresentationApiFailure()
    {
        var logs = new List<string>(); var errors = new List<string>();
        var request = new PresentationModeRequest(new FakePresentationApi(failOnCalls: [1]));
        var form = new MainForm(new PowerRequest(new FakeApi()), logs.Add, errors.Add, request);
        try
        {
            Show(form);
            form.NavigateToPage("Keep Awake");
            form.PresentationToggleButton.PerformClick(); Application.DoEvents();
            Equal("Modo de apresentação: Erro — inativo", form.PresentationStatusText);
            Equal(1, errors.Count);
            True(logs.Any(message => message.Contains("Erro de PowerSetRequest", StringComparison.Ordinal)));
            True(!request.IsEnabled);
        }
        finally { Close(form); }
    }

    private static void RealPresentationRequestCanBeSetAndCleared()
    {
        using var api = new WindowsPresentationPowerApi();
        api.Set(PresentationPowerRequestType.SystemRequired);
        api.Set(PresentationPowerRequestType.DisplayRequired);
        api.Clear(PresentationPowerRequestType.DisplayRequired);
        api.Clear(PresentationPowerRequestType.SystemRequired);
    }

    private static void RealPresentationRequestIsReleasedOnFormClose()
    {
        var request = new PresentationModeRequest(new WindowsPresentationPowerApi());
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, request);
        Show(form);
        form.NavigateToPage("Keep Awake");
        form.PresentationToggleButton.PerformClick(); Application.DoEvents();
        Equal("Modo de apresentação: Ativo", form.PresentationStatusText);
        True(request.IsEnabled);
        form.ExitApplication(); Application.DoEvents();
        True(form.IsDisposed);
        True(!request.IsEnabled);
    }

    private static void EmptyFilterKeepsAll()
    {
        Equal(2, WindowFilter.Apply(Samples(), " ").Count);
        Equal(2, WindowFilter.Apply(Samples(), null).Count);
    }

    private static void WindowFilterMatchesProcessAndTitle()
    {
        Equal(1, WindowFilter.Apply(Samples(), "EXCEL").Count);
        Equal(1, WindowFilter.Apply(Samples(), "Payroll").Count);
        Equal(2, WindowFilter.Apply(Samples(), "EXCEL; news").Count);
    }

    private static void WindowFilterCanReturnNoRows() => Equal(0, WindowFilter.Apply(Samples(), "not-open").Count);

    private static void FormOpensAndToggles()
    {
        var api = new FakeApi(); var errors = new List<string>(); var logs = new List<string>();
        var request = new PowerRequest(api); var form = new MainForm(request, logs.Add, errors.Add);
        try
        {
            Show(form);
            form.NavigateToPage("Keep Awake");
            Equal("Inativa", form.StatusText);
            Equal("Iniciar manutenção", form.ToggleText);
            form.ToggleButton.PerformClick(); Application.DoEvents();
            Equal("Ativa", form.StatusText);
            Equal("Interromper manutenção", form.ToggleText);
            Equal(ExecutionFlags.Continuous | ExecutionFlags.SystemRequired | ExecutionFlags.DisplayRequired, api.Calls[0]);
            form.ToggleButton.PerformClick(); Application.DoEvents();
            Equal("Inativa", form.StatusText);
            Equal("Iniciar manutenção", form.ToggleText);
            Equal(ExecutionFlags.Continuous, api.Calls[1]);
        }
        finally { Close(form); }
    }

    private static void MinimizeAndTrayRestore()
    {
        var request = new PowerRequest(new FakeApi());
        var form = new MainForm(request, _ => { }, _ => { });
        try
        {
            Show(form);
            form.NavigateToPage("Keep Awake");
            form.ToggleButton.PerformClick(); Application.DoEvents();
            form.WindowState = FormWindowState.Minimized; Application.DoEvents();
            True(!form.Visible, "minimize should hide the form");
            True(!form.ShowInTaskbar, "minimize should remove the taskbar button");
            True(form.TrayIcon.Visible, "tray icon should remain visible after minimize");
            True(request.IsEnabled);

            form.TrayOpenItem.PerformClick(); Application.DoEvents();
            True(form.Visible, "tray Open should show the form");
            True(form.ShowInTaskbar, "tray Open should restore the taskbar button");
            Equal(FormWindowState.Normal, form.WindowState);

            form.HideToTray(); Application.DoEvents();
            True(!form.Visible, "hide should conceal the form before tray restoration");
            form.HandleTrayDoubleClick(); Application.DoEvents();
            True(form.Visible, "tray double-click handler should show the form");
            True(form.ShowInTaskbar, "tray double-click should restore the taskbar button");
            Equal(FormWindowState.Normal, form.WindowState);
            True(request.IsEnabled);
        }
        finally { Close(form); }
    }

    private static void TrayRestoreRecoversOffScreenWindow()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { });
        try
        {
            Show(form);
            form.Bounds = new Rectangle(-32000, -32000, form.Width, form.Height);
            form.HideToTray();
            form.HandleTrayDoubleClick();
            Application.DoEvents();

            True(form.Visible, "tray restore should show the form");
            Equal(FormWindowState.Normal, form.WindowState);
            True(form.ShowInTaskbar, "tray restore should add the taskbar button");
            True(Screen.AllScreens.Any(screen => Rectangle.Intersect(form.Bounds, screen.WorkingArea).Width >= 100
                && Rectangle.Intersect(form.Bounds, screen.WorkingArea).Height >= 80),
                "restored form should have a visible area on a connected monitor");
        }
        finally { Close(form); }
    }

    private static void SecondInstanceActivationSignalsPrimary()
    {
        var eventName = $@"Local\WindowsKeepAwake-Test-{Guid.NewGuid():N}";
        using var activated = new ManualResetEventSlim();
        using var primarySignal = new InstanceActivationSignal(eventName, () => activated.Set());

        True(InstanceActivationSignal.TrySignalExisting(eventName), "second instance should signal the existing activation event");
        True(activated.Wait(TimeSpan.FromSeconds(2)), "primary activation handler should be called");
    }

    private static void TrayIconIsCreatedWithEmbeddedIcon()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { });
        try
        {
            Show(form);
            True(form.TrayIcon.Visible);
            True(form.TrayIcon.Icon is not null);
            True(form.TrayIcon.Icon!.Handle != IntPtr.Zero);
            True(form.TrayIconImage.Size.Width >= 16);
            True(form.TrayIconImage.Size.Height >= 16);
            Equal("Windows Keep Awake — Inativo", form.TrayIcon.Text);
        }
        finally { Close(form); }
        True(form.TrayDisposed);
    }

    private static void ClosingWindowKeepsBackgroundRequests()
    {
        var api = new FakeApi(); var request = new PowerRequest(api);
        var form = new MainForm(request, _ => { }, _ => { });
        Show(form);
        form.NavigateToPage("Keep Awake");
        form.ToggleButton.PerformClick(); Application.DoEvents();
        form.Close(); Application.DoEvents();
        True(!form.Visible);
        True(!form.IsDisposed);
        True(!form.ShowInTaskbar);
        True(request.IsEnabled);
        Equal(1, api.Calls.Count);
        True(form.TrayIcon.Visible);
        form.ExitApplication(); Application.DoEvents();
        True(form.IsDisposed);
        True(!request.IsEnabled);
    }

    private static void TrayMaintenanceCommandsStaySynchronized()
    {
        var api = new FakeApi(); var request = new PowerRequest(api);
        var form = new MainForm(request, _ => { }, _ => { });
        try
        {
            Show(form);
            True(form.TrayStartItem.Enabled);
            True(!form.TrayStopItem.Enabled);
            form.TrayStartItem.PerformClick(); Application.DoEvents();
            True(request.IsEnabled);
            Equal("Ativa", form.StatusText);
            True(!form.TrayStartItem.Enabled);
            True(form.TrayStopItem.Enabled);
            form.TrayStopItem.PerformClick(); Application.DoEvents();
            True(!request.IsEnabled);
            Equal("Inativa", form.StatusText);
            True(form.TrayStartItem.Enabled);
            True(!form.TrayStopItem.Enabled);
            Equal(ExecutionFlags.Continuous, api.Calls[1]);
        }
        finally { Close(form); }
    }

    private static void TrayPresentationCommandTracksState()
    {
        var api = new FakePresentationApi(); var request = new PresentationModeRequest(api);
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, request);
        try
        {
            Show(form);
            Equal("Ativar modo de apresentação", form.TrayPresentationItem.Text);
            form.TrayPresentationItem.PerformClick(); Application.DoEvents();
            True(request.IsEnabled);
            Equal("Modo de apresentação: Ativo", form.PresentationStatusText);
            Equal("Windows Keep Awake — Ativo", form.TrayIcon.Text);
            Equal("Desativar modo de apresentação", form.TrayPresentationItem.Text);
            form.TrayPresentationItem.PerformClick(); Application.DoEvents();
            True(!request.IsEnabled);
            Equal("Modo de apresentação: Inativo", form.PresentationStatusText);
            Equal("Ativar modo de apresentação", form.TrayPresentationItem.Text);
        }
        finally { Close(form); }
    }

    private static void TrayExitReleasesAndDisposes()
    {
        var powerApi = new FakeApi(); var powerRequest = new PowerRequest(powerApi);
        var presentationApi = new FakePresentationApi(); var presentationRequest = new PresentationModeRequest(presentationApi);
        var form = new MainForm(powerRequest, _ => { }, _ => { }, presentationRequest);
        Show(form);
        form.TrayStartItem.PerformClick();
        form.TrayPresentationItem.PerformClick();
        True(powerRequest.IsEnabled);
        True(presentationRequest.IsEnabled);
        form.TrayExitItem.PerformClick(); Application.DoEvents();
        True(form.IsDisposed);
        True(!powerRequest.IsEnabled);
        True(!presentationRequest.IsEnabled);
        True(presentationApi.IsDisposed);
        True(form.TrayDisposed);
        True(!form.TrayIcon.Visible);
        Equal(ExecutionFlags.Continuous, powerApi.Calls[1]);
    }

    private static void InitialOptionsAndAutomaticPresentation()
    {
        var api = new FakePresentationApi();
        var request = new PresentationModeRequest(api);
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, request, autoActivatePresentation: true);
        try
        {
            True(form.KeepDisplayOption.Checked);
            Equal("Indefinidamente", form.DurationSelector.SelectedItem?.ToString());
            Show(form);
            True(request.IsEnabled);
            Equal("Modo de apresentação: Ativo", form.PresentationStatusText);
        }
        finally { Close(form); }
    }

    private static void TimerExpirationReleasesActiveRequests()
    {
        var api = new FakeApi(); var presentationApi = new FakePresentationApi();
        var request = new PowerRequest(api);
        var presentationRequest = new PresentationModeRequest(presentationApi);
        var logs = new List<string>();
        var form = new MainForm(request, logs.Add, _ => { }, presentationRequest);
        try
        {
            Show(form);
            form.NavigateToPage("Keep Awake");
            form.ToggleButton.PerformClick();
            form.PresentationToggleButton.PerformClick();
            form.DurationSelector.SelectedIndex = 0;
            form.ExpireForTesting();
            True(!request.IsEnabled && !presentationRequest.IsEnabled);
            Equal("Inativa", form.StatusText);
            Equal("Modo de apresentação: Inativo", form.PresentationStatusText);
            True(logs.Any(x => x.Contains("Temporizador expirou", StringComparison.Ordinal)));
        }
        finally { Close(form); }
    }

    private static void FormFiltersLiveWindows()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { });
        try
        {
            var uniqueTitle = "KeepAwake test window " + Guid.NewGuid().ToString("N");
            form.Text = uniqueTitle;
            Show(form);
            form.FilterInput.Text = Process.GetCurrentProcess().ProcessName;
            form.RefreshStatus();
            var byProcess = form.DisplayedWindows;
            True(byProcess.Count > 0);
            True(byProcess.All(window => window.ProcessName.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase)));

            form.FilterInput.Text = uniqueTitle;
            form.RefreshStatus();
            var byTitle = form.DisplayedWindows;
            Equal(1, byTitle.Count);
            Equal(Process.GetCurrentProcess().ProcessName, byTitle[0].ProcessName);

            form.FilterInput.Text = "__keepawake_no_such_window_9f31__";
            form.RefreshStatus();
            Equal(0, form.DisplayedWindows.Count);

            form.FilterInput.Clear();
            form.RefreshStatus();
            True(form.DisplayedWindows.Count > 0);
        }
        finally { Close(form); }
    }

    private static void FormReportsApiFailure()
    {
        var logs = new List<string>(); var errors = new List<string>();
        var request = new PowerRequest(new FakeApi(0)); var form = new MainForm(request, logs.Add, errors.Add);
        try
        {
            Show(form);
            form.NavigateToPage("Keep Awake");
            form.ToggleButton.PerformClick(); Application.DoEvents();
            Equal("Erro — inativa", form.StatusText);
            Equal(1, errors.Count);
            True(errors[0].Contains("SetThreadExecutionState", StringComparison.Ordinal));
            True(logs.Any(message => message.Contains("Erro de SetThreadExecutionState", StringComparison.Ordinal)));
            True(!request.IsEnabled);
        }
        finally { Close(form); }
    }

    private static void FormCloseLogsReleaseFailure()
    {
        var logs = new List<string>(); var api = new FakeApi(1, 0, 1); var request = new PowerRequest(api);
        var form = new MainForm(request, logs.Add, _ => { });
        Show(form);
        form.NavigateToPage("Keep Awake");
        form.ToggleButton.PerformClick();
        Equal("Ativa", form.StatusText);
        form.ExitApplication(); Application.DoEvents();
        True(!form.IsDisposed);
        True(form.Visible);
        True(logs.Any(message => message.Contains("Falha ao liberar estado de energia", StringComparison.Ordinal)));
        True(request.IsEnabled);
        form.ExitApplication(); Application.DoEvents();
        True(form.IsDisposed);
        True(!request.IsEnabled);
    }

    private static void RealWindowsRequestIsReleasedOnClose()
    {
        var api = new WindowsExecutionStateApi();
        True(api.Set(ExecutionFlags.Continuous) != 0); // establish a known cleared state on this thread
        var request = new PowerRequest(api);
        var form = new MainForm(request, _ => { }, _ => { });
        try
        {
            Show(form);
            form.NavigateToPage("Keep Awake");
            form.ToggleButton.PerformClick(); Application.DoEvents();
            Equal("Ativa", form.StatusText);
            form.ExitApplication(); Application.DoEvents();
            True(form.IsDisposed);

            // The API returns the prior thread state. This same-thread read also clears residue.
            var stateAfterClose = api.Set(ExecutionFlags.Continuous);
            var requestFlags = ExecutionFlags.SystemRequired | ExecutionFlags.DisplayRequired;
            True((stateAfterClose & (uint)requestFlags) == 0, $"Request flags remained set: 0x{stateAfterClose:X8}");
        }
        finally
        {
            if (!form.IsDisposed) Close(form);
            _ = api.Set(ExecutionFlags.Continuous);
        }
    }

    private static void MaintenanceCatalogHasSafeInitialAvailability()
    {
        True(MaintenanceCatalog.Operations.Count >= 17);
        var implemented = MaintenanceCatalog.Operations.Where(operation => operation.Availability == MaintenanceAvailability.Available).ToArray();
        Equal(1, implemented.Length);
        Equal(UserTempAnalyzer.OperationId, implemented[0].Id);
        True(implemented[0].CanAnalyze);
        True(!implemented[0].RequiresAdministrator);
        True(MaintenanceCatalog.Operations.Any(operation => operation.Id == "chrome-maximum" && operation.Availability != MaintenanceAvailability.Available));
        True(MaintenanceCatalog.Operations.All(operation => !operation.Id.Contains("prefetch", StringComparison.OrdinalIgnoreCase)
            && !operation.Id.Contains("windows-logs", StringComparison.OrdinalIgnoreCase)));
    }

    private static void TempAnalyzerCountsFilesAndBytes()
    {
        var tree = FakeFileTree.CreateStandardRoot();
        tree.AddDirectory(@"C:\Users\test\AppData\Local\Temp\nested");
        tree.AddFile(@"C:\Users\test\AppData\Local\Temp\one.tmp", 12);
        tree.AddFile(@"C:\Users\test\AppData\Local\Temp\nested\two.tmp", 30);
        var result = new UserTempAnalyzer(tree).Analyze(FakeFileTree.Root);
        Equal(MaintenanceAnalysisStatus.Completed, result.Status);
        Equal(2, result.FilesIdentified);
        Equal(42L, result.PotentialBytes);
        Equal(2, tree.EnumerationCalls);
    }

    private static void TempAnalyzerReportsEmptyDirectory()
    {
        var tree = FakeFileTree.CreateStandardRoot();
        var result = new UserTempAnalyzer(tree).Analyze(FakeFileTree.Root);
        Equal(MaintenanceAnalysisStatus.Empty, result.Status);
        Equal(0, result.FilesIdentified);
        Equal(0L, result.PotentialBytes);
        Equal(0, result.Failures);
    }

    private static void TempAnalyzerReportsAccessFailures()
    {
        var tree = FakeFileTree.CreateStandardRoot();
        tree.AddFile(@"C:\Users\test\AppData\Local\Temp\good.tmp", 5);
        tree.AddDeniedFile(@"C:\Users\test\AppData\Local\Temp\denied.tmp");
        var result = new UserTempAnalyzer(tree).Analyze(FakeFileTree.Root);
        Equal(MaintenanceAnalysisStatus.Partial, result.Status);
        Equal(1, result.FilesIdentified);
        Equal(5L, result.PotentialBytes);
        Equal(1, result.Failures);
    }

    private static void TempAnalyzerReportsEnumerationFailures()
    {
        var tree = FakeFileTree.CreateStandardRoot();
        tree.AddDirectory(@"C:\Users\test\AppData\Local\Temp\blocked");
        tree.AddFile(@"C:\Users\test\AppData\Local\Temp\good.tmp", 7);
        tree.FailEnumeration(@"C:\Users\test\AppData\Local\Temp\blocked");
        var result = new UserTempAnalyzer(tree).Analyze(FakeFileTree.Root);
        Equal(MaintenanceAnalysisStatus.Partial, result.Status);
        Equal(1, result.FilesIdentified);
        Equal(1, result.Failures);
    }

    private static void TempAnalyzerCanCancel()
    {
        var tree = FakeFileTree.CreateStandardRoot();
        tree.AddFile(@"C:\Users\test\AppData\Local\Temp\first.tmp", 3);
        tree.AddFile(@"C:\Users\test\AppData\Local\Temp\second.tmp", 4);
        using var cancellation = new CancellationTokenSource();
        var progress = new InlineProgress<MaintenanceAnalysisProgress>(_ => cancellation.Cancel());
        var result = new UserTempAnalyzer(tree).Analyze(FakeFileTree.Root, progress, cancellation.Token);
        Equal(MaintenanceAnalysisStatus.Cancelled, result.Status);
        Equal(1, result.FilesIdentified);
    }

    private static void TempAnalyzerSkipsReparsePoints()
    {
        var tree = FakeFileTree.CreateStandardRoot();
        tree.AddDirectory(@"C:\Users\test\AppData\Local\Temp\junction", reparsePoint: true);
        tree.AddFile(@"C:\Users\test\AppData\Local\Temp\junction\outside.tmp", 100);
        var result = new UserTempAnalyzer(tree).Analyze(FakeFileTree.Root);
        Equal(MaintenanceAnalysisStatus.Empty, result.Status);
        Equal(1, result.ReparsePointsSkipped);
        True(!tree.EnumeratedDirectories.Contains(@"C:\Users\test\AppData\Local\Temp\junction"));
    }

    private static void RealReparseLinkIsSkipped()
    {
        using var directory = new TestDirectory();
        var scanRoot = Directory.CreateDirectory(Path.Combine(directory.Path, "scan-root"));
        var target = Directory.CreateDirectory(Path.Combine(directory.Path, "outside-target"));
        File.WriteAllBytes(Path.Combine(target.FullName, "not-to-follow.tmp"), [1, 2, 3]);
        var symbolicLink = Path.Combine(scanRoot.FullName, "symbolic-link");
        var linksCreated = 0;

        try
        {
            Directory.CreateSymbolicLink(symbolicLink, target.FullName);
            linksCreated++;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Console.WriteLine("SKIP symbolic-link creation unavailable: " + ex.GetType().Name);
        }

        var result = new UserTempAnalyzer(new WindowsReadOnlyFileTree()).Analyze(scanRoot.FullName);
        Equal(MaintenanceAnalysisStatus.Empty, result.Status);
        Equal(0, result.FilesIdentified);
        Equal((int)Directory.EnumerateFileSystemEntries(scanRoot.FullName).Count(entry =>
            (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0), result.ReparsePointsSkipped);
        Equal(linksCreated, result.ReparsePointsSkipped);
        if (linksCreated == 0) Console.WriteLine("SKIP no live symbolic link could be created; fake filesystem test covers junction/reparse-point traversal logic.");
        True(File.Exists(Path.Combine(target.FullName, "not-to-follow.tmp")));
    }

    private static void TempAnalyzerRejectsOutsideEntries()
    {
        var tree = FakeFileTree.CreateStandardRoot();
        tree.AddFile(@"C:\outside\private.tmp", 500);
        tree.AddEnumeratorEntry(FakeFileTree.Root, @"C:\outside\private.tmp");
        var result = new UserTempAnalyzer(tree).Analyze(FakeFileTree.Root);
        Equal(MaintenanceAnalysisStatus.Partial, result.Status);
        Equal(0, result.FilesIdentified);
        Equal(1, result.Failures);
        True(!tree.AttributeReads.Contains(@"C:\outside\private.tmp"));
    }

    private static void TempAnalyzerRejectsUnsafeRoots()
    {
        var tree = FakeFileTree.CreateStandardRoot();
        Equal(MaintenanceAnalysisStatus.Error, new UserTempAnalyzer(tree).Analyze(@"C:\missing\temp").Status);
        tree.AddDirectory(FakeFileTree.Root, reparsePoint: true);
        var linkedRoot = new UserTempAnalyzer(tree).Analyze(FakeFileTree.Root);
        Equal(MaintenanceAnalysisStatus.Error, linkedRoot.Status);
        Equal(0, tree.EnumerationCalls);
    }

    private static void MaintenanceHistoryPersistsAndPrunes()
    {
        using var directory = new TestDirectory();
        var now = DateTimeOffset.Now;
        var clock = new FixedTimeProvider(now);
        var path = Path.Combine(directory.Path, "maintenance-history.json");
        var store = new JsonMaintenanceHistoryStore(path, clock);
        store.Add(HistoryEntry(now.AddDays(-31), Guid.NewGuid()));
        store.Add(HistoryEntry(now.AddHours(-1), Guid.NewGuid()));
        var loaded = store.Load();
        Equal(1, loaded.Count);
        Equal(now.AddHours(-1), loaded[0].StartedAt);
        Equal(1, new JsonMaintenanceHistoryStore(path, clock).Load().Count);
        True(!File.ReadAllText(path).Contains("private-path", StringComparison.OrdinalIgnoreCase));
    }

    private static void MaintenanceHistoryEnforcesLimits()
    {
        using var directory = new TestDirectory();
        var now = DateTimeOffset.Now;
        var store = new JsonMaintenanceHistoryStore(Path.Combine(directory.Path, "maintenance-history.json"), new FixedTimeProvider(now));
        for (var index = 0; index <= JsonMaintenanceHistoryStore.MaximumRecords; index++)
            store.Add(HistoryEntry(now.AddSeconds(index), Guid.NewGuid()));
        Equal(JsonMaintenanceHistoryStore.MaximumRecords, store.Load().Count);
        True(new FileInfo(Path.Combine(directory.Path, "maintenance-history.json")).Length <= JsonMaintenanceHistoryStore.MaximumFileBytes);
    }

    private static void RealTempAnalysisIsReadOnly()
    {
        using var directory = new TestDirectory();
        var nested = Directory.CreateDirectory(Path.Combine(directory.Path, "nested"));
        var firstPath = Path.Combine(directory.Path, "one.tmp");
        var secondPath = Path.Combine(nested.FullName, "two.tmp");
        File.WriteAllBytes(firstPath, [1, 2, 3, 4]);
        File.WriteAllBytes(secondPath, [5, 6, 7]);
        var beforeFirst = File.ReadAllBytes(firstPath);
        var beforeSecond = File.ReadAllBytes(secondPath);
        var beforeFirstWrite = File.GetLastWriteTimeUtc(firstPath);
        var beforeSecondWrite = File.GetLastWriteTimeUtc(secondPath);

        var result = new UserTempAnalyzer(new WindowsReadOnlyFileTree()).Analyze(directory.Path);

        Equal(MaintenanceAnalysisStatus.Completed, result.Status);
        Equal(2, result.FilesIdentified);
        Equal(7L, result.PotentialBytes);
        True(beforeFirst.SequenceEqual(File.ReadAllBytes(firstPath)));
        True(beforeSecond.SequenceEqual(File.ReadAllBytes(secondPath)));
        Equal(beforeFirstWrite, File.GetLastWriteTimeUtc(firstPath));
        Equal(beforeSecondWrite, File.GetLastWriteTimeUtc(secondPath));
        Equal(2, Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories).Length);
    }

    private static void WindowsAnalyzerUsesUserTempPath() =>
        Equal(Path.GetFullPath(Path.GetTempPath()), new WindowsUserTempAnalysisService().TargetDirectory);

    private static void TempAnalyzerReportsResourceSample()
    {
        using var directory = new TestDirectory();
        for (var index = 0; index < 1000; index++)
            File.WriteAllBytes(Path.Combine(directory.Path, $"sample-{index:D4}.tmp"), [1, 2, 3, 4]);

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var beforeCpu = process.TotalProcessorTime;
        var beforeMemory = process.WorkingSet64;
        var stopwatch = Stopwatch.StartNew();
        var result = new UserTempAnalyzer(new WindowsReadOnlyFileTree()).Analyze(directory.Path);
        stopwatch.Stop();
        process.Refresh();
        var cpu = process.TotalProcessorTime - beforeCpu;
        var memoryDelta = process.WorkingSet64 - beforeMemory;
        Equal(MaintenanceAnalysisStatus.Completed, result.Status);
        Equal(1000, result.FilesIdentified);
        Equal(4000L, result.PotentialBytes);
        Console.WriteLine($"INFO TEMP sample: files={result.FilesIdentified}; wall={stopwatch.Elapsed.TotalMilliseconds:F0}ms; process CPU={cpu.TotalMilliseconds:F0}ms; working-set delta={memoryDelta / (1024d * 1024):F2}MiB");
    }

    private static void MaintenanceInterfaceRunsAnalysisAndStoresHistory()
    {
        var now = DateTimeOffset.Now;
        var result = new MaintenanceAnalysisResult(UserTempAnalyzer.OperationId, now, TimeSpan.FromMilliseconds(25), MaintenanceAnalysisStatus.Completed, 2, 15, 0, 0);
        var service = new FakeTempAnalysisService(result);
        var history = new MemoryMaintenanceHistoryStore();
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, maintenanceAnalysisService: service, maintenanceHistoryStore: history);
        try
        {
            Show(form);
            var center = form.MaintenanceCenter;
            form.NavigateToPage("Central de Manutenção");
            True(center.Parent is Panel, "maintenance control should be hosted in the maintenance navigation page");
            Equal("Central de Manutenção", form.CurrentPage);
            Equal(MaintenanceCatalog.Operations.Count, center.CatalogList.Items.Count);
            True(center.AnalyzeButton.Enabled, "TEMP analysis should be enabled for the available catalog item");
            center.AnalyzeButton.PerformClick();
            Application.DoEvents();
            True(center.ResultBox.Text.Contains("Análise concluída", StringComparison.Ordinal), center.ResultBox.Text);
            True(center.ResultBox.Text.Contains("15 bytes", StringComparison.Ordinal), center.ResultBox.Text);
            True(center.ResultBox.Text.Contains("Nenhum arquivo foi aberto para leitura de conteúdo, alterado, movido ou excluído", StringComparison.Ordinal), center.ResultBox.Text);
            Equal(1, history.Entries.Count);
            Equal(1, center.HistoryList.Items.Count);
            Equal("Análise", center.HistoryList.Items[0].SubItems[2].Text);
            center.SelectOperationAt(1);
            True(!center.AnalyzeButton.Enabled);
            True(center.DetailsText.Contains("Planejada", StringComparison.Ordinal));
        }
        finally { Close(form); }
    }

    private static void MainNavigationShowsAllScreens()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false);
        try
        {
            Show(form);
            var expected = new[] { "Início", "Keep Awake", "Janelas e sessão", "Central de Manutenção", "Diagnóstico", "Configurações" };
            True(expected.SequenceEqual(form.NavigationPages));
            foreach (var page in expected)
            {
                form.GetNavigationButton(page).PerformClick();
                Application.DoEvents();
                Equal(page, form.CurrentPage);
                True(form.GetNavigationButton(page).BackColor == AppTheme.Selected, "selected navigation item should be highlighted");
            }
        }
        finally { Close(form); }
    }

    private static void DashboardTracksCurrentState()
    {
        var presentation = new PresentationModeRequest(new FakePresentationApi());
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, presentation, autoActivatePresentation: false);
        try
        {
            Show(form);
            Equal("Inativa", form.DashboardAwakeState.Text);
            Equal("Inativo", form.DashboardPresentationState.Text);
            form.NavigateToPage("Keep Awake");
            form.ToggleButton.PerformClick();
            Application.DoEvents();
            Equal("Ativa", form.DashboardAwakeState.Text);
            form.PresentationToggleButton.PerformClick();
            Application.DoEvents();
            Equal("Ativo", form.DashboardPresentationState.Text);
            form.DurationSelector.SelectedIndex = 1;
            Equal("1 hora", form.DashboardDuration.Text);
            form.KeepDisplayOption.Checked = false;
            Equal("Sem solicitação de tela", form.DashboardDisplayState.Text);
        }
        finally { Close(form); }
    }

    private static void PowerSnapshotsAreMapped()
    {
        var mains = PowerSupplySnapshot.FromNativeStatus(true, 1, 0x08, 86);
        Equal(PowerSourceKind.External, mains.Source);
        Equal(true, mains.HasBattery);
        Equal((byte)86, mains.BatteryPercent);
        True(mains.IsCharging);
        True(mains.Describe().Contains("Conectado à tomada", StringComparison.Ordinal));
        True(mains.Describe().Contains("86%", StringComparison.Ordinal));

        var desktop = PowerSupplySnapshot.FromNativeStatus(true, 1, 0x80, 255);
        Equal(false, desktop.HasBattery);
        True(desktop.Describe().Contains("sem bateria detectada", StringComparison.Ordinal));

        var battery = PowerSupplySnapshot.FromNativeStatus(true, 0, 0x01, 78);
        Equal(PowerSourceKind.Battery, battery.Source);
        Equal((byte)78, battery.BatteryPercent);
        True(battery.Describe().Contains("Na bateria — 78%", StringComparison.Ordinal));

        var unknown = PowerSupplySnapshot.FromNativeStatus(true, 255, 255, 255);
        Equal(PowerSourceKind.Unknown, unknown.Source);
        Equal<byte?>(null, unknown.BatteryPercent);
        True(unknown.Describe().Contains("Alimentação desconhecida", StringComparison.Ordinal));

        var unavailable = PowerSupplySnapshot.FromNativeStatus(false, 255, 255, 255, "falha simulada");
        Equal(PowerSourceKind.Unavailable, unavailable.Source);
        True(unavailable.Describe().Contains("indisponível", StringComparison.Ordinal));
        Equal("falha simulada", unavailable.Error);
    }

    private static void PowerIndicatorTracksIndependentStates()
    {
        var current = PowerSupplySnapshot.FromNativeStatus(true, 1, 0x08, 86);
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false,
            powerStatusProvider: () => current);
        try
        {
            Show(form);
            form.RefreshPowerStatus();
            True(form.PowerIndicator.PowerDescription.Contains("86%", StringComparison.Ordinal));
            Equal("Keep Awake: Inativo", form.PowerIndicator.KeepAwakeDescription);
            form.NavigateToPage("Keep Awake");
            form.ToggleButton.PerformClick();
            form.RefreshPowerStatus();
            Equal("Keep Awake: Ativo", form.PowerIndicator.KeepAwakeDescription);

            current = PowerSupplySnapshot.FromNativeStatus(true, 0, 0x01, 78);
            form.RefreshPowerStatus();
            True(form.PowerIndicator.PowerDescription.Contains("Na bateria — 78%", StringComparison.Ordinal));
            Equal("Keep Awake: Ativo", form.PowerIndicator.KeepAwakeDescription);

            form.ClientSize = new Size(900, 600);
            Application.DoEvents();
            True(form.PowerIndicator.IsCompact);
            Equal("⚡", form.PowerIndicator.PowerLabel.Text);
            Equal("◉", form.PowerIndicator.KeepAwakeLabel.Text);
            True(form.PowerIndicator.PowerLabel.AccessibleName!.Contains("78%", StringComparison.Ordinal));
        }
        finally { Close(form); }
    }

    private static void RealWindowsPowerStatusIsReadable()
    {
        var status = WindowsPowerStatusReader.Read();
        Console.WriteLine($"INFO GetSystemPowerStatus on Windows: {status.Source}; battery={(status.HasBattery?.ToString() ?? "unknown")}; percent={(status.BatteryPercent?.ToString() ?? "unknown")}; charging={status.IsCharging}");
        True(status.Source != PowerSourceKind.Unavailable, status.Error ?? "GetSystemPowerStatus returned unavailable");
    }

    private static void NavigationChangesVisiblePage()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false);
        try
        {
            Show(form);
            form.GetNavigationButton("Central de Manutenção").PerformClick();
            Application.DoEvents();
            Equal("Central de Manutenção", form.CurrentPage);
            True(form.MaintenanceCenter.Parent!.Visible, "maintenance page should be visible after navigation");
            form.GetNavigationButton("Diagnóstico").PerformClick();
            Application.DoEvents();
            Equal("Diagnóstico", form.CurrentPage);
            True(form.MaintenanceCenter.Parent!.Visible == false, "maintenance page should be hidden after leaving it");
        }
        finally { Close(form); }
    }

    private static void MaintenanceCategoryFilterAndDetailsWork()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false);
        try
        {
            Show(form);
            var center = form.MaintenanceCenter;
            form.NavigateToPage("Central de Manutenção");
            Equal(MaintenanceCatalog.Operations.Count, center.CatalogList.Items.Count);
            Equal(6, center.CatalogList.Columns.Count);
            Equal("Categoria", center.CatalogList.Columns[0].Text);
            Equal("Descrição", center.CatalogList.Columns[2].Text);
            center.CategoryFilter.SelectedIndex = 2;
            Application.DoEvents();
            True(center.CatalogList.Items.Count > 0);
            True(center.SelectedOperationName.Contains("Chrome", StringComparison.Ordinal));
            True(center.DetailsText.Contains("Aplicativos", StringComparison.Ordinal));
            True(!center.AnalyzeButton.Enabled);
            center.CategoryFilter.SelectedIndex = 0;
            Application.DoEvents();
            Equal(MaintenanceCatalog.Operations.Count, center.CatalogList.Items.Count);
            center.SelectOperationAt(0);
            Equal("TEMP do usuário", center.SelectedOperationName);
            True(center.AnalyzeButton.Enabled);
        }
        finally { Close(form); }
    }

    private static void MaintenanceOperationSearchWorks()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false);
        try
        {
            Show(form);
            form.NavigateToPage("Central de Manutenção");
            var center = form.MaintenanceCenter;
            var total = center.CatalogList.Items.Count;
            center.OperationSearch.Text = "Chrome";
            Application.DoEvents();
            True(center.CatalogList.Items.Count > 0 && center.CatalogList.Items.Count < total);
            True(center.CatalogList.Items.Cast<ListViewItem>().All(item => item.SubItems[1].Text.Contains("Chrome", StringComparison.OrdinalIgnoreCase)
                || item.SubItems[2].Text.Contains("Chrome", StringComparison.OrdinalIgnoreCase)));
            center.OperationSearch.Text = "filtro-sem-correspondencia";
            Application.DoEvents();
            Equal(0, center.CatalogList.Items.Count);
            center.OperationSearch.Clear();
            Application.DoEvents();
            Equal(total, center.CatalogList.Items.Count);
        }
        finally { Close(form); }
    }

    private static void MaintenanceListColumnsFitResponsiveWidth()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false);
        try
        {
            Show(form);
            form.NavigateToPage("Central de Manutenção");
            foreach (var size in new[] { new Size(800, 480), new Size(900, 600), new Size(1366, 768), new Size(1920, 1080) })
            {
                form.ClientSize = size;
                Application.DoEvents();
                var center = form.MaintenanceCenter;
                True(center.CatalogColumnsWidth <= center.CatalogList.ClientSize.Width + 2,
                    $"catalog columns {center.CatalogColumnsWidth}px exceed list width {center.CatalogList.ClientSize.Width}px at {size}");
                True(center.HistoryColumnsWidth <= center.HistoryList.ClientSize.Width + 2,
                    $"history columns {center.HistoryColumnsWidth}px exceed list width {center.HistoryList.ClientSize.Width}px at {size}");
            }
        }
        finally { Close(form); }
    }

    private static void MainPagesResizeAtTargetSizes()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false);
        try
        {
            Show(form);
            foreach (var size in new[] { new Size(900, 600), new Size(1366, 768), new Size(1920, 1080), new Size(800, 480) })
            {
                form.ClientSize = size;
                form.NavigateToPage("Central de Manutenção");
                Application.DoEvents();
                True(form.MaintenanceCenter.ClientSize.Width > 500);
                True(form.MaintenanceCenter.ClientSize.Height > 250);
                True(form.MaintenanceCenter.AnalyzeButton.Bounds.Width >= 120);
                True(form.MaintenanceCenter.HistoryList.Columns.Count == 7);
                Equal(size.Width < 1120 ? 72 : 252, form.SidebarWidth);
                Equal(size.Width >= 1120, form.MaintenanceCenter.UsesWideLayout);
            }
        }
        finally { Close(form); }
    }

    private static void MaintenanceExecutionControlsRemainSafe()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false,
            maintenanceAnalysisService: new FakeTempAnalysisService(new MaintenanceAnalysisResult(UserTempAnalyzer.OperationId, DateTimeOffset.Now, TimeSpan.Zero, MaintenanceAnalysisStatus.Empty, 0, 0, 0, 0)),
            maintenanceHistoryStore: new MemoryMaintenanceHistoryStore());
        try
        {
            Show(form);
            form.NavigateToPage("Central de Manutenção");
            var center = form.MaintenanceCenter;
            Equal(3, center.ExecuteButtonCount);
            Equal(0, center.AvailableExecutionCount);
            True(!center.ExecuteSelectedButton.Enabled);
            True(!center.ExecuteCategoryButton.Enabled);
            True(!center.ExecuteAllButton.Enabled);
            True(center.ExecuteSelectedButton.AccessibleDescription!.Contains("nenhum executor", StringComparison.OrdinalIgnoreCase));
            center.PreviewAllButton.PerformClick();
            True(center.DetailsText.Contains("Prévia de todas as operações", StringComparison.Ordinal));
            True(center.DetailsText.Contains("IGNORADA", StringComparison.Ordinal));
            True(center.DetailsText.Contains("Nenhuma ação foi executada", StringComparison.Ordinal));
            center.CategoryFilter.SelectedIndex = 2;
            center.PreviewCategoryButton.PerformClick();
            True(center.DetailsText.Contains("Prévia da categoria selecionada", StringComparison.Ordinal));
            True(center.DetailsText.Contains("Chrome", StringComparison.Ordinal));
            center.ExecuteSelectedButton.PerformClick();
            center.ExecuteCategoryButton.PerformClick();
            center.ExecuteAllButton.PerformClick();
            True(!center.AnalysisRunning);
            True(center.HistoryList.Items.Count == 0, "unavailable actions must not create fake execution history");
        }
        finally { Close(form); }
    }

    private static void MaintenanceExecutionPreviewIsSafe()
    {
        var preview = new MaintenanceExecutionRegistry().Preview(MaintenanceCatalog.Operations);
        Equal(MaintenanceCatalog.Operations.Count, preview.Items.Count);
        Equal(0, preview.Included.Count);
        Equal(MaintenanceCatalog.Operations.Count, preview.Unavailable.Count);
        True(preview.Items.All(item => !item.ExecutorAvailable && !item.Included));
        True(preview.Items.Where(item => item.Risk == MaintenanceRisk.High).All(item => !item.Included));

        var safe = MaintenanceCatalog.Operations.Single(item => item.Id == "thumbnail-cache");
        var highRisk = MaintenanceCatalog.Operations.Single(item => item.Id == "chrome-maximum");
        var withFakeExecutors = new MaintenanceExecutionRegistry([
            new FakeMaintenanceExecutor(safe.Id, safe.Risk),
            new FakeMaintenanceExecutor(highRisk.Id, highRisk.Risk)
        ]).Preview([safe, highRisk]);
        Equal(1, withFakeExecutors.Included.Count);
        True(withFakeExecutors.Items.Single(item => item.OperationId == highRisk.Id).ExecutorAvailable);
        True(!withFakeExecutors.Items.Single(item => item.OperationId == highRisk.Id).Included);
        True(withFakeExecutors.Items.Single(item => item.OperationId == highRisk.Id).Reason.Contains("risco alto", StringComparison.Ordinal));
        var explicitSelection = withFakeExecutors.WithSelectedOperations([highRisk.Id]);
        Equal(1, explicitSelection.Included.Count);
        Equal(highRisk.Id, explicitSelection.Included[0].OperationId);
        True(!explicitSelection.Items.Single(item => item.OperationId == safe.Id).Included);
    }

    private static void MaintenanceExecutionRequiresMatchingApproval()
    {
        var operation = MaintenanceCatalog.Operations.Single(item => item.Id == "thumbnail-cache");
        var executor = new FakeMaintenanceExecutor(operation.Id, operation.Risk);
        var registry = new MaintenanceExecutionRegistry([executor]);
        var preview = registry.Preview([operation]);
        var rejected = false;
        try
        {
            registry.ExecuteSequentiallyAsync(preview, new MaintenanceExecutionApproval(Guid.NewGuid(), DateTimeOffset.Now), null, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (InvalidOperationException) { rejected = true; }
        True(rejected);
        Equal(0, executor.Calls);

        var result = registry.ExecuteSequentiallyAsync(preview, new MaintenanceExecutionApproval(preview.Id, DateTimeOffset.Now), null, CancellationToken.None)
            .GetAwaiter().GetResult();
        Equal(1, executor.Calls);
        Equal(MaintenanceBatchStatus.Completed, result.Status);
        Equal(1, result.Operations.Count);
    }

    private static void MaintenanceHistoryDistinguishesActions()
    {
        var timestamp = DateTimeOffset.Now;
        var analysis = MaintenanceHistoryEntryFactory.FromResult(new MaintenanceAnalysisResult(
            UserTempAnalyzer.OperationId, timestamp, TimeSpan.FromMilliseconds(10), MaintenanceAnalysisStatus.Completed, 3, 240, 0, 0));
        var execution = MaintenanceHistoryEntryFactory.FromResult(new MaintenanceOperationExecutionResult(
            "future-operation", timestamp, TimeSpan.FromMilliseconds(20), MaintenanceOperationState.Completed, 2, 120, []));
        Equal(MaintenanceActionType.Analysis, analysis.Action);
        Equal(MaintenanceActionType.Execution, execution.Action);
        Equal(2, execution.ItemsAffected);
        Equal(120L, execution.BytesRecovered);
        var roundtrip = System.Text.Json.JsonSerializer.Deserialize<MaintenanceHistoryEntry>(System.Text.Json.JsonSerializer.Serialize(execution));
        True(roundtrip is not null);
        Equal(MaintenanceActionType.Execution, roundtrip!.Action);
        Equal(120L, roundtrip.BytesRecovered);
    }

    private static void ResponsivePagesKeepControlBounds()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, autoActivatePresentation: false);
        try
        {
            Show(form);
            Console.WriteLine($"INFO geometry matrix running at DeviceDpi={form.DeviceDpi} (Windows DPI setting was not changed).");
            foreach (var size in new[] { new Size(800, 480), new Size(900, 600), new Size(1366, 768), new Size(1920, 1080) })
            {
                form.ClientSize = size;
                Console.WriteLine($"INFO horizontal geometry: requested={size.Width}x{size.Height}; deviceDpi={form.DeviceDpi}");
                foreach (var pageName in form.NavigationPages)
                {
                    form.NavigateToPage(pageName);
                    Application.DoEvents();
                    var page = form.GetPageControl(pageName);
                    AssertHorizontalBounds(page, pageName);
                }
            }

            form.Size = form.MinimumSize;
            Console.WriteLine($"INFO minimum outer window={form.Size.Width}x{form.Size.Height}; client={form.ClientSize.Width}x{form.ClientSize.Height}");
            foreach (var pageName in form.NavigationPages)
            {
                form.NavigateToPage(pageName);
                Application.DoEvents();
                AssertHorizontalBounds(form.GetPageControl(pageName), pageName + " (minimum outer window)");
            }
        }
        finally { Close(form); }
    }

    private static void AssertHorizontalBounds(Control parent, string pageName)
    {
        var visibleChildren = parent.Controls.Cast<Control>().Where(child => child.Visible).ToArray();
        if (parent is TableLayoutPanel or FlowLayoutPanel)
        {
            for (var left = 0; left < visibleChildren.Length; left++)
            for (var right = left + 1; right < visibleChildren.Length; right++)
            {
                var first = visibleChildren[left];
                var second = visibleChildren[right];
                if (first.Bounds.IntersectsWith(second.Bounds))
                    True(false, $"{pageName}: siblings overlap in {parent.GetType().Name}: {first.GetType().Name} {first.Bounds} / {second.GetType().Name} {second.Bounds}");
            }
        }

        foreach (var child in visibleChildren)
        {
            True(child.Left >= -SystemInformation.VerticalScrollBarWidth && child.Right <= parent.ClientSize.Width + 2,
                $"{pageName}: {child.GetType().Name} {child.Name} bounds {child.Bounds} exceed parent {parent.GetType().Name} width {parent.ClientSize.Width}; path={ControlPath(child)}");
            AssertHorizontalBounds(child, pageName);
        }
    }

    private static string ControlPath(Control control)
    {
        var names = new List<string>();
        for (Control? current = control; current is not null; current = current.Parent)
            names.Add(current.GetType().Name + (string.IsNullOrEmpty(current.Name) ? "" : $"({current.Name})") + $"[{current.Width}x{current.Height}]");
        return string.Join(" <- ", names);
    }

    private static void MaintenanceInterfaceCanCancelAnalysis()
    {
        var service = new CancellableTempAnalysisService();
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { }, maintenanceAnalysisService: service, maintenanceHistoryStore: new MemoryMaintenanceHistoryStore());
        try
        {
            Show(form);
            var center = form.MaintenanceCenter;
            form.NavigateToPage("Central de Manutenção");
            center.AnalyzeButton.PerformClick();
            Application.DoEvents();
            True(center.AnalysisRunning, "analysis should enter the running state");
            True(center.CancelButton.Enabled, "cancel should be enabled while analysis is running");
            center.CancelButton.PerformClick();
            var timeout = Stopwatch.StartNew();
            while (center.AnalysisRunning && timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            True(!center.AnalysisRunning, "analysis should finish after cancellation");
            True(center.ResultBox.Text.Contains("Análise cancelada", StringComparison.Ordinal), center.ResultBox.Text);
        }
        finally { Close(form); }
    }

    private static MaintenanceHistoryEntry HistoryEntry(DateTimeOffset startedAt, Guid id) => new(
        id, UserTempAnalyzer.OperationId, startedAt, TimeSpan.FromSeconds(1), MaintenanceAnalysisStatus.Completed, 1, 4, 0, false);

    private static IEnumerable<WindowInfo> Samples() =>
    [new("EXCEL", "Payroll - Q4", false), new("chrome", "News", true)];

    private static void Show(Form form)
    {
        form.Show();
        Application.DoEvents();
    }

    private static void Close(MainForm form)
    {
        if (!form.IsDisposed) form.ExitApplication();
        Application.DoEvents();
    }

    private static void True(bool value, string? message = null)
    {
        if (!value) throw new Exception(message ?? "Expected true");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}, got {actual}");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}");
    }

    private sealed class FakeFileTree : IReadOnlyFileTree
    {
        internal const string Root = @"C:\Users\test\AppData\Local\Temp";
        private readonly Dictionary<string, Node> _nodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _extraEntries = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _failedEnumerations = new(StringComparer.OrdinalIgnoreCase);
        internal List<string> EnumeratedDirectories { get; } = [];
        internal List<string> AttributeReads { get; } = [];
        internal int EnumerationCalls { get; private set; }

        internal static FakeFileTree CreateStandardRoot()
        {
            var tree = new FakeFileTree();
            tree.AddDirectory(Root);
            return tree;
        }

        internal void AddDirectory(string path, bool reparsePoint = false) =>
            _nodes[Path.GetFullPath(path)] = new Node(FileAttributes.Directory | (reparsePoint ? FileAttributes.ReparsePoint : 0), 0, false);

        internal void AddFile(string path, long length) =>
            _nodes[Path.GetFullPath(path)] = new Node(FileAttributes.Normal, length, false);

        internal void AddDeniedFile(string path) =>
            _nodes[Path.GetFullPath(path)] = new Node(FileAttributes.Normal, 0, true);

        internal void FailEnumeration(string path) => _failedEnumerations.Add(Path.GetFullPath(path));

        internal void AddEnumeratorEntry(string directory, string entry)
        {
            var fullDirectory = Path.GetFullPath(directory);
            if (!_extraEntries.TryGetValue(fullDirectory, out var entries)) _extraEntries[fullDirectory] = entries = [];
            entries.Add(entry);
        }

        public string GetFullPath(string path) => Path.GetFullPath(path);
        public bool DirectoryExists(string path) => _nodes.TryGetValue(path, out var node) && (node.Attributes & FileAttributes.Directory) != 0;

        public IEnumerable<string> EnumerateEntries(string directoryPath)
        {
            directoryPath = Path.GetFullPath(directoryPath);
            EnumerationCalls++;
            EnumeratedDirectories.Add(directoryPath);
            if (_failedEnumerations.Contains(directoryPath)) throw new UnauthorizedAccessException("Simulated enumeration denial");
            foreach (var item in _nodes.Keys.Where(path => Parent(path).Equals(directoryPath, StringComparison.OrdinalIgnoreCase))) yield return item;
            if (_extraEntries.TryGetValue(directoryPath, out var extra))
                foreach (var item in extra) yield return item;
        }

        public FileAttributes GetAttributes(string path)
        {
            path = Path.GetFullPath(path);
            AttributeReads.Add(path);
            if (!_nodes.TryGetValue(path, out var node) || node.Denied) throw new UnauthorizedAccessException("Simulated attribute denial");
            return node.Attributes;
        }

        public long GetFileLength(string path)
        {
            path = Path.GetFullPath(path);
            if (!_nodes.TryGetValue(path, out var node) || node.Denied) throw new UnauthorizedAccessException("Simulated file denial");
            return node.Length;
        }

        private static string Parent(string path) => Path.GetDirectoryName(path) ?? string.Empty;
        private sealed record Node(FileAttributes Attributes, long Length, bool Denied);
    }

    private sealed class TestDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "KeepAwake.Tests", Guid.NewGuid().ToString("N"));
        internal TestDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }

    private sealed class MemoryMaintenanceHistoryStore : IMaintenanceHistoryStore
    {
        public List<MaintenanceHistoryEntry> Entries { get; } = [];
        public IReadOnlyList<MaintenanceHistoryEntry> Load() => Entries.OrderByDescending(entry => entry.StartedAt).ToArray();
        public void Add(MaintenanceHistoryEntry entry) => Entries.Add(entry);
    }

    private sealed class FakeMaintenanceExecutor(string operationId, MaintenanceRisk risk) : IMaintenanceOperationExecutor
    {
        public string OperationId => operationId;
        public bool RequiresAdministrator => false;
        public MaintenanceRisk Risk => risk;
        public bool IsApproved => true;
        public bool IsValidated => true;
        public int Calls { get; private set; }

        public Task<MaintenanceOperationExecutionResult> ExecuteAsync(IProgress<MaintenanceExecutionProgress>? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new MaintenanceOperationExecutionResult(OperationId, DateTimeOffset.Now, TimeSpan.Zero,
                MaintenanceOperationState.Completed, 0, 0, []));
        }
    }

    private sealed class FakeTempAnalysisService(MaintenanceAnalysisResult result) : IUserTempAnalysisService
    {
        public string TargetDirectory => @"C:\Users\test\AppData\Local\Temp";
        public Task<MaintenanceAnalysisResult> AnalyzeAsync(IProgress<MaintenanceAnalysisProgress>? progress, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class CancellableTempAnalysisService : IUserTempAnalysisService
    {
        public string TargetDirectory => @"C:\Users\test\AppData\Local\Temp";
        public async Task<MaintenanceAnalysisResult> AnalyzeAsync(IProgress<MaintenanceAnalysisProgress>? progress, CancellationToken cancellationToken)
        {
            var startedAt = DateTimeOffset.Now;
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException)
            {
                return new MaintenanceAnalysisResult(UserTempAnalyzer.OperationId, startedAt, TimeSpan.Zero, MaintenanceAnalysisStatus.Cancelled, 0, 0, 0, 0);
            }
            return new MaintenanceAnalysisResult(UserTempAnalyzer.OperationId, startedAt, TimeSpan.Zero, MaintenanceAnalysisStatus.Completed, 0, 0, 0, 0);
        }
    }

    private sealed class FakeApi(params uint[] results) : IExecutionStateApi
    {
        private readonly Queue<uint> _results = new(results);
        public List<ExecutionFlags> Calls { get; } = [];
        public uint Set(ExecutionFlags flags)
        {
            Calls.Add(flags);
            return _results.Count > 0 ? _results.Dequeue() : 1;
        }
    }

    private sealed class FakePresentationApi(int[]? failOnCalls = null) : IPresentationPowerApi
    {
        private readonly HashSet<int> _failOnCalls = failOnCalls?.ToHashSet() ?? [];
        public List<string> Calls { get; } = [];
        public bool IsDisposed { get; private set; }

        public void Set(PresentationPowerRequestType requestType) => Record($"Set:{requestType}");
        public void Clear(PresentationPowerRequestType requestType) => Record($"Clear:{requestType}");

        public void Dispose() => IsDisposed = true;

        private void Record(string operation)
        {
            Calls.Add(operation);
            if (_failOnCalls.Contains(Calls.Count)) throw new InvalidOperationException($"Fake failure on call {Calls.Count}");
        }
    }
}
