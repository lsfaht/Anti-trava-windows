using Microsoft.UI.Xaml;
using Orbit.Application;

namespace Orbit.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationWait;
    private bool _isPrimary;
    internal static DiagnosticJournal? Journal { get; private set; }
    internal static RequestSnapshot? LatestSnapshot;
    private readonly bool _smoke = Environment.GetCommandLineArgs().Contains("--diagnostic-smoke");
    public App()
    {
        UnhandledException += (_, args) =>
        {
            Journal?.RecordException("winui_unhandled", args.Exception, LatestSnapshot, fatal: true);
        };
        var name = $@"Local\Orbit-{Environment.UserName}";
        if (_smoke) name += "-diagnostic-smoke";
        _instanceMutex = new Mutex(true, name, out _isPrimary);
        if (!_isPrimary) return;
        Journal = new DiagnosticJournal(_smoke ? Path.Combine(AppContext.BaseDirectory, "smoke-results") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Orbit"),
            (typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown") + (_smoke ? " [TESTE: APIs de energia simuladas]" : ""), Environment.ProcessId);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Journal.RecordException("runtime_unhandled", e.ExceptionObject as Exception ?? new Exception(), LatestSnapshot, fatal: true);
        TaskScheduler.UnobservedTaskException += (_, e) => Journal.RecordException("task_unobserved", e.Exception, LatestSnapshot);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Journal.Record("process_exit_observed", LatestSnapshot);
        try { InitializeComponent(); }
        catch (Exception ex) { Journal.RecordException("xaml_initialization_failed", ex, fatal: true); throw; }
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name + "-Activate");
        _activationWait = ThreadPool.RegisterWaitForSingleObject(_activationEvent, (_, timedOut) =>
        {
            if (!timedOut) _window?.DispatcherQueue.TryEnqueue(() => (_window as MainWindow)?.RestoreFromTray());
        }, null, Timeout.Infinite, executeOnlyOnce: false);
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!_isPrimary) { if (_smoke) Environment.ExitCode = 2; else SignalPrimary(); Exit(); return; }
        var smokeApis = _smoke ? new SmokePowerApis() : null;
        try { _window = new MainWindow(Journal!, smokeApis, smokeApis is null ? null : () => new KeepAwake.Core.PresentationModeRequest(smokeApis)); }
        catch (Exception ex) { Journal?.RecordException("window_initialization_failed", ex, LatestSnapshot, fatal: true); Exit(); return; }
        _window.Closed += (_, _) =>
        {
            _activationWait?.Unregister(null); _activationEvent?.Dispose();
            _instanceMutex?.ReleaseMutex(); _instanceMutex?.Dispose();
        };
        _window.Activate();
        if (_smoke) _window.DispatcherQueue.TryEnqueue(async () => await ((MainWindow)_window).RunDiagnosticSmokeAsync(smokeApis!));
    }

    private static void SignalPrimary()
    {
        try { using var signal = EventWaitHandle.OpenExisting($@"Local\Orbit-{Environment.UserName}-Activate"); signal.Set(); }
        catch (WaitHandleCannotBeOpenedException) { }
        catch (UnauthorizedAccessException) { }
    }
}
