using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using KeepAwake.Core;

namespace Orbit.App;

internal sealed class WindowsExecutionStateApi : IExecutionStateApi
{
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint SetThreadExecutionState(uint flags);
    public uint Set(ExecutionFlags flags)
    {
        var previous = SetThreadExecutionState((uint)flags);
        if (previous == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "SetThreadExecutionState falhou.");
        return previous;
    }
}

internal sealed class WindowsPresentationPowerApi : IPresentationPowerApi
{
    private const uint ContextVersion = 0, ContextSimpleString = 1;
    private static readonly IntPtr InvalidHandle = new(-1);
    private IntPtr _handle, _reason;
    [StructLayout(LayoutKind.Sequential)] private struct ReasonContext { public uint Version, Flags; public IntPtr SimpleReasonString; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr PowerCreateRequest(ref ReasonContext context);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PowerSetRequest(IntPtr handle, PresentationPowerRequestType type);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PowerClearRequest(IntPtr handle, PresentationPowerRequestType type);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);

    public WindowsPresentationPowerApi()
    {
        _reason = Marshal.StringToHGlobalUni("Orbit: modo de apresentação ativado pelo usuário");
        try
        {
            var context = new ReasonContext { Version = ContextVersion, Flags = ContextSimpleString, SimpleReasonString = _reason };
            _handle = PowerCreateRequest(ref context);
            if (_handle == IntPtr.Zero || _handle == InvalidHandle) throw new Win32Exception(Marshal.GetLastWin32Error(), "PowerCreateRequest falhou.");
        }
        catch { Marshal.FreeHGlobal(_reason); _reason = IntPtr.Zero; throw; }
    }
    public void Set(PresentationPowerRequestType type) { EnsureOpen(); if (!PowerSetRequest(_handle, type)) throw new Win32Exception(Marshal.GetLastWin32Error(), $"PowerSetRequest({type}) falhou."); }
    public void Clear(PresentationPowerRequestType type) { EnsureOpen(); if (!PowerClearRequest(_handle, type)) throw new Win32Exception(Marshal.GetLastWin32Error(), $"PowerClearRequest({type}) falhou."); }
    public void Dispose()
    {
        if (_handle != IntPtr.Zero && _handle != InvalidHandle)
        {
            if (!CloseHandle(_handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "CloseHandle da solicitação de energia falhou.");
            _handle = IntPtr.Zero;
        }
        if (_reason != IntPtr.Zero) { Marshal.FreeHGlobal(_reason); _reason = IntPtr.Zero; }
    }
    private void EnsureOpen() { if (_handle == IntPtr.Zero || _handle == InvalidHandle) throw new ObjectDisposedException(nameof(WindowsPresentationPowerApi)); }
}

internal sealed class OrbitReadOnlyFileTree : IReadOnlyFileTree
{
    public string GetFullPath(string path) => Path.GetFullPath(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateEntries(string directoryPath) => Directory.EnumerateFileSystemEntries(directoryPath);
    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
    public long GetFileLength(string path) => new FileInfo(path).Length;
}

internal sealed class OrbitUserTempAnalysisService : IUserTempAnalysisService
{
    private readonly UserTempAnalyzer _analyzer = new(new OrbitReadOnlyFileTree());
    public string TargetDirectory { get; } = Path.GetFullPath(Path.GetTempPath());
    public Task<MaintenanceAnalysisResult> AnalyzeAsync(IProgress<MaintenanceAnalysisProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => _analyzer.Analyze(TargetDirectory, progress, cancellationToken));
}

internal sealed class OrbitHistoryStore : IMaintenanceHistoryStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsKeepAwake", "maintenance-history.json");
    private readonly object _gate = new();
    public IReadOnlyList<MaintenanceHistoryEntry> Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return [];
            var bytes = File.ReadAllBytes(_path);
            if (bytes.Length > 1024 * 1024) throw new InvalidDataException("O histórico excede 1 MiB.");
            return (JsonSerializer.Deserialize<List<MaintenanceHistoryEntry>>(bytes, Options) ?? [])
                .Where(entry => entry.StartedAt >= DateTimeOffset.UtcNow.AddDays(-30)).OrderByDescending(entry => entry.StartedAt).Take(500).ToArray();
        }
    }
    public void Add(MaintenanceHistoryEntry entry)
    {
        lock (_gate)
        {
            var records = Load().Append(entry).OrderByDescending(item => item.StartedAt).Take(500).ToList();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(records, Options);
            while (bytes.Length > 1024 * 1024 && records.Count > 0) { records.RemoveAt(records.Count - 1); bytes = JsonSerializer.SerializeToUtf8Bytes(records, Options); }
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporaryPath = _path + ".orbit.tmp";
            File.WriteAllBytes(temporaryPath, bytes);
            File.Move(temporaryPath, _path, true);
        }
    }
}

internal sealed record VisibleWindow(string Process, string Title, bool IsForeground);

internal static class OrbitWindowDiagnostics
{
    private delegate bool EnumCallback(IntPtr window, IntPtr state);
    [StructLayout(LayoutKind.Sequential)] private struct LastInputInfo { public uint Size, Time; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo info);
    [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();

    public static IReadOnlyList<VisibleWindow> GetWindows()
    {
        var foreground = GetForegroundWindow(); var windows = new List<VisibleWindow>();
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            var title = new StringBuilder(512); GetWindowText(window, title, title.Capacity);
            if (title.Length == 0) return true;
            GetWindowThreadProcessId(window, out var pid);
            string process; try { process = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch { process = "(indisponível)"; }
            windows.Add(new VisibleWindow(process, title.ToString(), window == foreground)); return true;
        }, IntPtr.Zero);
        return windows;
    }
    public static TimeSpan? GetIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return null;
        return TimeSpan.FromMilliseconds(Math.Max(0, (long)GetTickCount64() - info.Time));
    }
}
