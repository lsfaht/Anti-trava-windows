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
            ("empty window filter keeps all rows", EmptyFilterKeepsAll),
            ("window filter matches process and title case-insensitively", WindowFilterMatchesProcessAndTitle),
            ("nonmatching window filter returns no rows", WindowFilterCanReturnNoRows),
            ("WinForms opens inactive and toggles active/inactive", FormOpensAndToggles),
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

    private static void FormFiltersLiveWindows()
    {
        var form = new MainForm(new PowerRequest(new FakeApi()), _ => { }, _ => { });
        try
        {
            Show(form);
            form.FilterInput.Text = Process.GetCurrentProcess().ProcessName;
            form.RefreshStatus();
            var byProcess = form.DisplayedWindows;
            True(byProcess.Count > 0);
            True(byProcess.All(window => window.ProcessName.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase)));

            form.FilterInput.Text = "Windows Keep Awake — diagnóstico";
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
            Equal("Erro", form.StatusText);
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
        form.Close(); Application.DoEvents();
        True(form.IsDisposed);
        True(logs.Any(message => message.Contains("Falha ao liberar estado de energia", StringComparison.Ordinal)));
        True(request.IsEnabled);
        request.Dispose();
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
            form.Close(); Application.DoEvents();
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

    private static void Close(Form form)
    {
        if (!form.IsDisposed) form.Close();
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
}
