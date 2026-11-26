using System.Text.Json;
using KeepAwake.Core;
using Orbit.Application;

static class DiagnosticTests
{
    public static IEnumerable<(string Name, Action Test)> Cases => new (string, Action)[]
    {
        ("detects incomplete previous run without claiming crash", Interrupted),
        ("normal exit persists final released state", NormalExit),
        ("fatal exception cannot become a clean exit", FatalExit),
        ("storage failure retains diagnostics in memory", StorageFailure),
        ("export omits exception messages and source paths", Privacy),
        ("event log rotation bounds retention", Rotation),
        ("corrupt marker does not prevent startup", CorruptMarker),
        ("hide and restore preserve active requests until explicit exit", TrayLifecycle),
        ("lock snapshot preserves requested state without claiming cause", LockEvent),
        ("resume marks requests uncertain without reactivation", ResumeEvent),
        ("partial presentation failure reports error and retained request", PartialFailure),
        ("cleanup failure remains distinguishable from clean exit", CleanupFailure),
        ("repeated activation and disposal do not leak request counts", RepeatedCalls)
    };
    private static void Check(bool value) { if (!value) throw new Exception("Diagnostic assertion failed"); }
    private static RequestSnapshot Empty => new(false, false, false, "unknown", "unknown", "visible");
    private static KeepAwakeViewModel Model(FakePresentationApi? api = null) => new(new FakeExecutionApi(), () => new PresentationModeRequest(api ?? new FakePresentationApi()));
    private static RunState Marker(string directory) => JsonSerializer.Deserialize<RunState>(File.ReadAllText(Path.Combine(directory, "run-state.json")))!;
    private static void Interrupted()
    {
        using var files = new Files(); var first = new DiagnosticJournal(files.Path, "test", 1); first.Heartbeat(Empty);
        var second = new DiagnosticJournal(files.Path, "test", 2);
        Check(second.PreviousRunNotice.Contains("sem encerramento normal confirmado"));
        Check(second.PreviousRunNotice.Contains("Não comprova crash"));
    }
    private static void NormalExit()
    {
        using var files = new Files(); var journal = new DiagnosticJournal(files.Path, "test", 1); using var vm = Model();
        var life = new OrbitLifetime(vm, journal); vm.TogglePresentation(); life.RequestExit(); life.Finish();
        Check(Marker(files.Path).Outcome == "normal"); Check(!life.Snapshot.PresentationDisplayRequested);
        var next = new DiagnosticJournal(files.Path, "test", 2); Check(next.PreviousRunNotice.Contains("normalmente"));
    }
    private static void FatalExit()
    {
        using var files = new Files(); var journal = new DiagnosticJournal(files.Path, "test", 1);
        journal.RecordException("fatal", new InvalidOperationException(), fatal: true); journal.Complete(Empty, true);
        Check(Marker(files.Path).Outcome == "faulted");
    }
    private static void StorageFailure()
    {
        using var files = new Files(); var blocked = System.IO.Path.Combine(files.Path, "file"); File.WriteAllText(blocked, "fixture");
        var journal = new DiagnosticJournal(blocked, "test", 1); journal.Record("memory_evidence", Empty);
        Check(journal.LastError is not null); var report = journal.Export(files.Path, Empty, ["consulta não executada"]);
        Check(File.ReadAllText(report).Contains("memory_evidence"));
    }
    private static void Privacy()
    {
        using var files = new Files(); var journal = new DiagnosticJournal(files.Path, "test", 1);
        journal.RecordException("error", new IOException("private-document.txt secret-user"));
        var text = File.ReadAllText(journal.Export(files.Path, Empty, ["consulta indisponível"]));
        Check(!text.Contains("private-document") && !text.Contains("secret-user"));
        Check(text.Contains("IOException") && text.Contains("HRESULT"));
        Check(text.Contains("consulta indisponível") && text.Contains("efeito no Windows não verificado"));
    }
    private static void Rotation()
    {
        using var files = new Files(); var journal = new DiagnosticJournal(files.Path, "test", 1, 2048);
        for (var i = 0; i < 70; i++) journal.Record("rotation", Empty);
        Check(File.Exists(journal.LogPath + ".previous"));
        Check(new FileInfo(journal.LogPath).Length < 4096 && new FileInfo(journal.LogPath + ".previous").Length < 4096);
    }
    private static void CorruptMarker()
    {
        using var files = new Files(); File.WriteAllText(System.IO.Path.Combine(files.Path, "run-state.json"), "{");
        var journal = new DiagnosticJournal(files.Path, "test", 1);
        Check(journal.PreviousRunNotice.Contains("indisponível")); Check(Marker(files.Path).Outcome == "running");
    }
    private static void TrayLifecycle()
    {
        using var files = new Files(); var journal = new DiagnosticJournal(files.Path, "test", 1); var api = new FakePresentationApi(); using var vm = Model(api);
        var life = new OrbitLifetime(vm, journal); vm.TogglePresentation();
        life.Hide(); life.Hide(); Check(!life.ExitRequested && !life.IsClosed && vm.IsPresentationActive);
        life.Restore(); Check(life.Visibility == "visible" && api.Calls.Count == 2);
        life.RequestExit(); life.Finish(); life.Finish(); life.Restore();
        Check(life.IsClosed && life.Visibility == "closed" && api.Calls.Count == 5);
    }
    private static void LockEvent()
    {
        using var files = new Files(); var journal = new DiagnosticJournal(files.Path, "test", 1); using var vm = Model();
        var life = new OrbitLifetime(vm, journal); vm.TogglePresentation(); life.Observe(ObservedSystemEvent.Locked);
        Check(life.Snapshot.Session == "locked" && life.Snapshot.PresentationDisplayRequested);
        var entry = File.ReadLines(journal.LogPath).Select(line => JsonSerializer.Deserialize<DiagnosticEntry>(line)!).Last();
        Check(entry.Event == "system_Locked" && entry.Requests!.PresentationDisplayRequested);
        life.Observe(ObservedSystemEvent.Unlocked); Check(life.Session == "unlocked");
    }
    private static void ResumeEvent()
    {
        using var files = new Files(); var journal = new DiagnosticJournal(files.Path, "test", 1); var api = new FakePresentationApi(); using var vm = Model(api);
        var life = new OrbitLifetime(vm, journal); vm.TogglePresentation(); life.Observe(ObservedSystemEvent.Suspend); life.Observe(ObservedSystemEvent.Resume);
        Check(vm.PresentationStatus.Contains("não revalidados") && api.Calls.Count == 2);
        vm.TogglePresentation(); vm.TogglePresentation(); Check(vm.PresentationStatus.Contains("aceitas"));
        Check(!vm.VerificationNotice.Contains("há pedidos não revalidados"));
    }
    private static void PartialFailure()
    {
        using var vm = new KeepAwakeViewModel(new FakeExecutionApi(), () => new PresentationModeRequest(new FailingPresentation()));
        var notified = false; vm.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(vm.PresentationLabel);
        Check(!vm.TogglePresentation()); Check(vm.IsPresentationSystemRequested && !vm.IsPresentationDisplayRequested);
        Check(vm.PresentationStatus.Contains("parciais") && notified);
    }
    private static void CleanupFailure()
    {
        using var files = new Files(); var journal = new DiagnosticJournal(files.Path, "test", 1);
        var vm = new KeepAwakeViewModel(new FakeExecutionApi { FailOnCall = 2 }, () => new PresentationModeRequest(new FakePresentationApi()));
        var life = new OrbitLifetime(vm, journal); vm.TogglePower(); life.RequestExit(); life.Finish();
        Check(!vm.ReleaseSucceeded && Marker(files.Path).Outcome == "cleanup_error");
    }
    private static void RepeatedCalls()
    {
        var api = new FakePresentationApi(); var request = new PresentationModeRequest(api);
        request.Enable(); request.Enable(); request.Disable(); request.Disable(); request.Dispose(); request.Dispose();
        Check(api.Calls.SequenceEqual(new[] { "Set:SystemRequired", "Set:DisplayRequired", "Clear:DisplayRequired", "Clear:SystemRequired", "Dispose" }));
    }
    private sealed class FailingPresentation : IPresentationPowerApi
    {
        public void Set(PresentationPowerRequestType type) { if (type == PresentationPowerRequestType.DisplayRequired) throw new InvalidOperationException(); }
        public void Clear(PresentationPowerRequestType type) => throw new InvalidOperationException();
        public void Dispose() { }
    }
    private sealed class Files : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OrbitTests", Guid.NewGuid().ToString("N"));
        public Files() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true); // Only this fixture's newly created directory.
    }
}
