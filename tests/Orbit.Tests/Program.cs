using KeepAwake.Core;
using Orbit.Application;

var checks = new List<(string Name, Action Test)>
{
    ("starts inactive", StartsInactive),
    ("filters visible windows by process or title", FiltersWindows),
    ("filter with no matches returns no windows", WindowFilterCanReturnNoResults),
    ("empty window filter restores all windows", ClearingWindowFilterRestoresAll),
    ("activates then releases execution state", ActivatesAndReleases),
    ("reports API failure without claiming active", ReportsFailure),
    ("keeps active state and reports release failure", ReportsReleaseFailure),
    ("disposal releases active energy request", DisposeReleases),
    ("presentation request is released on disposal", PresentationDisposeReleases)
};
checks.AddRange(DiagnosticTests.Cases);
var failed = 0;
foreach (var (name, test) in checks)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); }
}
Console.WriteLine($"{checks.Count - failed}/{checks.Count} checks passed");
return failed == 0 ? 0 : 1;

static void StartsInactive()
{
    using var vm = new KeepAwakeViewModel(new FakeExecutionApi(), () => new PresentationModeRequest(new FakePresentationApi()));
    Equal("Inativa", vm.Status); True(!vm.IsActive); Equal("Inativo", vm.PresentationStatus);
}
static void FiltersWindows()
{
    var windows = new[] { new WindowInfo("chrome", "Gmail", false), new WindowInfo("EXCEL", "Budget 2026", true) };
    Equal(1, WindowFilter.Apply(windows, "chrome").Count); Equal(1, WindowFilter.Apply(windows, "budget").Count);
}
static void WindowFilterCanReturnNoResults()
{
    var windows = new[] { new WindowInfo("chrome", "Gmail", false) }; Equal(0, WindowFilter.Apply(windows, "no-match").Count);
}
static void ClearingWindowFilterRestoresAll()
{
    var windows = new[] { new WindowInfo("chrome", "Gmail", false), new WindowInfo("EXCEL", "Budget", true) };
    Equal(2, WindowFilter.Apply(windows, "").Count);
}
static void ActivatesAndReleases()
{
    var api = new FakeExecutionApi(); using var vm = new KeepAwakeViewModel(api, () => new PresentationModeRequest(new FakePresentationApi()));
    True(vm.TogglePower()); True(vm.IsActive); True((api.Calls[0] & ExecutionFlags.SystemRequired) != 0); True((api.Calls[0] & ExecutionFlags.DisplayRequired) != 0);
    True(vm.TogglePower()); True(!vm.IsActive); Equal(ExecutionFlags.Continuous, api.Calls[1]);
}
static void ReportsFailure()
{
    var api = new FakeExecutionApi { FailNext = true }; using var vm = new KeepAwakeViewModel(api, () => new PresentationModeRequest(new FakePresentationApi()));
    True(!vm.TogglePower()); True(!vm.IsActive); True(vm.Status.StartsWith("Erro", StringComparison.Ordinal)); True(vm.Message.Contains("falhou", StringComparison.OrdinalIgnoreCase));
}
static void DisposeReleases()
{
    var api = new FakeExecutionApi(); var vm = new KeepAwakeViewModel(api, () => new PresentationModeRequest(new FakePresentationApi()));
    vm.TogglePower(); vm.Dispose(); Equal(2, api.Calls.Count); Equal(ExecutionFlags.Continuous, api.Calls[^1]);
}
static void ReportsReleaseFailure()
{
    var api = new FakeExecutionApi { FailOnCall = 2 }; var vm = new KeepAwakeViewModel(api, () => new PresentationModeRequest(new FakePresentationApi()));
    True(vm.TogglePower()); vm.Dispose(); True(api.Calls.Count == 2); True(vm.Message.Contains("liberar", StringComparison.OrdinalIgnoreCase));
}
static void PresentationDisposeReleases()
{
    var api = new FakePresentationApi(); var vm = new KeepAwakeViewModel(new FakeExecutionApi(), () => new PresentationModeRequest(api));
    True(vm.TogglePresentation()); vm.Dispose(); Equal(new[] { "Set:SystemRequired", "Set:DisplayRequired", "Clear:DisplayRequired", "Clear:SystemRequired", "Dispose" }, api.Calls.ToArray());
}
static void True(bool value) { if (!value) throw new InvalidOperationException("assertion failed"); }
static void Equal<T>(T expected, T actual) { if (expected is Array ea && actual is Array aa) { if (!ea.Cast<object>().SequenceEqual(aa.Cast<object>())) throw new InvalidOperationException($"expected {string.Join(",", ea.Cast<object>())}; got {string.Join(",", aa.Cast<object>())}"); return; } if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"expected {expected}; got {actual}"); }

sealed class FakeExecutionApi : IExecutionStateApi
{
    public List<ExecutionFlags> Calls { get; } = [];
    public bool FailNext { get; set; }
    public int FailOnCall { get; set; }
    public uint Set(ExecutionFlags flags) { Calls.Add(flags); if (FailNext) { FailNext = false; return 0; } if (FailOnCall == Calls.Count) return 0; return 1; }
}
sealed class FakePresentationApi : IPresentationPowerApi
{
    public List<string> Calls { get; } = [];
    public void Set(PresentationPowerRequestType type) => Calls.Add("Set:" + type);
    public void Clear(PresentationPowerRequestType type) => Calls.Add("Clear:" + type);
    public void Dispose() => Calls.Add("Dispose");
}
