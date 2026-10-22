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

internal sealed class WindowsPresentationPowerApi : IPresentationPowerApi
{
    private const uint PowerRequestContextVersion = 0;
    private const uint PowerRequestContextSimpleString = 0x1;
    private static readonly IntPtr InvalidHandleValue = new(-1);
    private IntPtr _requestHandle;
    private IntPtr _reasonString;

    [StructLayout(LayoutKind.Sequential)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        public IntPtr SimpleReasonString;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr powerRequest, PresentationPowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr powerRequest, PresentationPowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    public WindowsPresentationPowerApi()
    {
        _reasonString = Marshal.StringToHGlobalUni("Windows Keep Awake: modo de apresentação ativado pelo usuário");
        try
        {
            var context = new ReasonContext
            {
                Version = PowerRequestContextVersion,
                Flags = PowerRequestContextSimpleString,
                SimpleReasonString = _reasonString
            };
            _requestHandle = PowerCreateRequest(ref context);
            if (_requestHandle == IntPtr.Zero || _requestHandle == InvalidHandleValue)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"PowerCreateRequest falhou (Win32 {errorCode}).");
            }
        }
        catch
        {
            Marshal.FreeHGlobal(_reasonString);
            _reasonString = IntPtr.Zero;
            throw;
        }
    }

    public void Set(PresentationPowerRequestType requestType)
    {
        EnsureOpen();
        if (PowerSetRequest(_requestHandle, requestType)) return;
        var errorCode = Marshal.GetLastWin32Error();
        throw new Win32Exception(errorCode, $"PowerSetRequest({requestType}) falhou (Win32 {errorCode}).");
    }

    public void Clear(PresentationPowerRequestType requestType)
    {
        EnsureOpen();
        if (PowerClearRequest(_requestHandle, requestType)) return;
        var errorCode = Marshal.GetLastWin32Error();
        throw new Win32Exception(errorCode, $"PowerClearRequest({requestType}) falhou (Win32 {errorCode}).");
    }

    public void Dispose()
    {
        if (_requestHandle == IntPtr.Zero || _requestHandle == InvalidHandleValue) return;
        var handle = _requestHandle;
        if (!CloseHandle(handle))
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new Win32Exception(errorCode, $"CloseHandle da solicitação de apresentação falhou (Win32 {errorCode}).");
        }
        _requestHandle = IntPtr.Zero;
        if (_reasonString != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_reasonString);
            _reasonString = IntPtr.Zero;
        }
    }

    private void EnsureOpen()
    {
        if (_requestHandle == IntPtr.Zero || _requestHandle == InvalidHandleValue)
            throw new ObjectDisposedException(nameof(WindowsPresentationPowerApi));
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
