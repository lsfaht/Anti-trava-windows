namespace KeepAwake.Core;

[Flags]
public enum ExecutionFlags : uint
{
    Continuous = 0x80000000,
    SystemRequired = 0x00000001,
    DisplayRequired = 0x00000002
}

public interface IExecutionStateApi
{
    uint Set(ExecutionFlags flags);
}

public sealed class PowerRequest(IExecutionStateApi api) : IDisposable
{
    private bool _enabled;
    private bool _disposed;
    private bool _displayRequired;
    public bool IsEnabled => _enabled;

    public void Enable(bool keepDisplayOn)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_enabled && _displayRequired == keepDisplayOn) return;
        var flags = ExecutionFlags.Continuous | ExecutionFlags.SystemRequired;
        if (keepDisplayOn) flags |= ExecutionFlags.DisplayRequired;
        if (api.Set(flags) == 0) throw new InvalidOperationException("SetThreadExecutionState falhou.");
        _enabled = true;
        _displayRequired = keepDisplayOn;
    }

    public void Disable()
    {
        if (!_enabled) return;
        if (api.Set(ExecutionFlags.Continuous) == 0)
            throw new InvalidOperationException("Não foi possível liberar a solicitação de energia.");
        _enabled = false;
        _displayRequired = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Disable();
        _disposed = true;
    }
}

public sealed record WindowInfo(string ProcessName, string Title, bool IsForeground);

public static class WindowFilter
{
    public static IReadOnlyList<WindowInfo> Apply(IEnumerable<WindowInfo> windows, string? configuredFilters)
    {
        var filters = (configuredFilters ?? string.Empty).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (filters.Length == 0) return windows.ToArray();
        return windows.Where(w => filters.Any(f => w.ProcessName.Contains(f, StringComparison.OrdinalIgnoreCase)
            || w.Title.Contains(f, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
}
