using System.Runtime.InteropServices;
using System.Text;
using System.ComponentModel;
using KeepAwake.Core;

namespace KeepAwake.Windows;

internal sealed class WindowsExecutionStateApi : IExecutionStateApi
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);
    public uint Set(ExecutionFlags flags)
    {
        var previousState = SetThreadExecutionState((uint)flags);
        if (previousState == 0)
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode, $"SetThreadExecutionState falhou (Win32 {errorCode}).");
        }
        return previousState;
    }
}

internal static class WindowEnumerator
{
    private delegate bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    public static IReadOnlyList<WindowInfo> GetVisibleWindows()
    {
        var foreground = GetForegroundWindow();
        var result = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var title = new StringBuilder(512);
            GetWindowText(hwnd, title, title.Capacity);
            if (title.Length == 0) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            string processName;
            try { processName = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
            catch { processName = "(indisponível)"; }
            result.Add(new WindowInfo(processName, title.ToString(), hwnd == foreground));
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
