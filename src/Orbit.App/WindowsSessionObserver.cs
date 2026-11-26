using System.ComponentModel;
using System.Runtime.InteropServices;
using Orbit.Application;

namespace Orbit.App;

// Hidden top-level window: receives broadcasts even while the main window is in the tray.
internal sealed class WindowsSessionObserver : System.Windows.Forms.NativeWindow, IDisposable
{
    private readonly Action<ObservedSystemEvent> _observe;
    private readonly Action<string, Exception> _error;
    private bool _registered;
    private IntPtr _powerRegistration;
    public WindowsSessionObserver(Action<ObservedSystemEvent> observe, Action<string, Exception> error)
    {
        _observe = observe; _error = error;
        CreateHandle(new System.Windows.Forms.CreateParams { Caption = "Orbit event observer" });
        _registered = WTSRegisterSessionNotification(Handle, 0); // NOTIFY_FOR_THIS_SESSION
        if (!_registered) _error("session_registration_failed", new Win32Exception(Marshal.GetLastWin32Error()));
        _powerRegistration = RegisterSuspendResumeNotification(Handle, 0);
        if (_powerRegistration == IntPtr.Zero) _error("power_registration_failed", new Win32Exception(Marshal.GetLastWin32Error()));
    }
    public bool SessionNotificationsAvailable => _registered;
    public bool PowerNotificationsAvailable => _powerRegistration != IntPtr.Zero;
    protected override void WndProc(ref System.Windows.Forms.Message message)
    {
        try
        {
            ObservedSystemEvent? observed = message.Msg switch
            {
                0x2B1 => message.WParam.ToInt32() switch
                {
                    7 => ObservedSystemEvent.Locked, 8 => ObservedSystemEvent.Unlocked,
                    2 or 4 => ObservedSystemEvent.Disconnected, 1 or 3 => ObservedSystemEvent.Connected,
                    _ => null
                },
                0x218 => message.WParam.ToInt32() switch
                {
                    4 => ObservedSystemEvent.Suspend, 7 or 18 => ObservedSystemEvent.Resume,
                    10 => ObservedSystemEvent.PowerChanged, _ => null
                },
                0x16 when message.WParam != IntPtr.Zero => ObservedSystemEvent.SessionEnding,
                _ => null
            };
            if (observed is not null) _observe(observed.Value);
            if (message.Msg is 0x11 or 0x218) { message.Result = new IntPtr(1); return; }
        }
        catch (Exception ex) { _error("session_observer_error", ex); }
        base.WndProc(ref message);
    }
    public void Dispose()
    {
        if (_registered && !WTSUnRegisterSessionNotification(Handle))
            _error("session_unregistration_failed", new Win32Exception(Marshal.GetLastWin32Error()));
        _registered = false;
        if (_powerRegistration != IntPtr.Zero && !UnregisterSuspendResumeNotification(_powerRegistration))
            _error("power_unregistration_failed", new Win32Exception(Marshal.GetLastWin32Error()));
        _powerRegistration = IntPtr.Zero;
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
    [DllImport("wtsapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(IntPtr window, uint flags);
    [DllImport("wtsapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr RegisterSuspendResumeNotification(IntPtr recipient, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterSuspendResumeNotification(IntPtr handle);
}
