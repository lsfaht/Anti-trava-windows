using System.Diagnostics;
using KeepAwake.Core;
using KeepAwake.Windows;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
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
            ("closing the window hides it without releasing power requests", ClosingWindowKeepsBackgroundRequests),
            ("tray maintenance commands stay synchronized with the form", TrayMaintenanceCommandsStaySynchronized),
            ("tray presentation command tracks active state", TrayPresentationCommandTracksState),
            ("tray exit releases requests and disposes the tray icon", TrayExitReleasesAndDisposes),
            ("initial options and automatic presentation activation", InitialOptionsAndAutomaticPresentation),
            ("timer expiration releases active requests", TimerExpirationReleasesActiveRequests),
            ("WinForms process/title/no-match/clear filters work", FormFiltersLiveWindows),
            ("WinForms reports and logs API failures", FormReportsApiFailure),
            ("closing a form after release failure logs the error and allows retry", FormCloseLogsReleaseFailure),
            ("closing an active form releases the real Windows request", RealWindowsRequestIsReleasedOnClose)
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
