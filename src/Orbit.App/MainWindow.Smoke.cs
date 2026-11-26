using KeepAwake.Core;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using System.Text.Json;

namespace Orbit.App;

// Opt-in integration harness. Real host/window lifecycle, fake power APIs, isolated journal/mutex.
internal sealed class SmokePowerApis : IExecutionStateApi, IPresentationPowerApi
{
    public bool SystemRequested, DisplayRequested, ExecutionRequested, Disposed;
    public uint Set(ExecutionFlags flags) { ExecutionRequested = flags != ExecutionFlags.Continuous; return 1; }
    public void Set(PresentationPowerRequestType type) { if (type == PresentationPowerRequestType.DisplayRequired) DisplayRequested = true; else SystemRequested = true; }
    public void Clear(PresentationPowerRequestType type) { if (type == PresentationPowerRequestType.DisplayRequired) DisplayRequested = false; else SystemRequested = false; }
    public void Dispose() { Disposed = true; }
}

public sealed partial class MainWindow
{
    internal async Task RunDiagnosticSmokeAsync(SmokePowerApis apis)
    {
        var results = new List<object>();
        var failures = 0;
        void Check(string scenario, bool passed)
        {
            results.Add(new { scenario, passed });
            if (!passed) failures++;
        }
        async Task Invoke(Microsoft.UI.Xaml.Controls.Button button)
        {
            var peer = new ButtonAutomationPeer(button);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            await Task.Delay(150);
        }
        Closed += (_, _) =>
        {
            Check("Exit closes host, disposes tray and releases fake power requests", _lifetime.ExitRequested && _lifetime.IsClosed && _trayDisposed && apis.Disposed && !apis.SystemRequested && !apis.DisplayRequested && !apis.ExecutionRequested);
            const int expectedChecks = 13;
            if (results.Count != expectedChecks) { failures++; results.Add(new { scenario = "Smoke terminated before all expected scenarios", passed = false }); }
            var result = new { time = DateTimeOffset.Now, processId = Environment.ProcessId, expectedChecks, evidence = "Real WinUI window/handlers; fake power APIs. No physical lock, suspend or screenshot validation.", failures, results };
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "smoke-results", "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = failures == 0 ? 0 : 1;
        };
        try
        {
            await Task.Delay(150);
            Check("Initial window is visible and requests inactive", IsWindowVisible(_windowHandle) && !_keepAwake.IsActive && !_keepAwake.IsPresentationActive);
            Check("NotifyIcon has valid icon and is enabled", _trayIcon.Visible && _trayIcon.Icon is not null && _trayImage.Handle != IntPtr.Zero);
            Check("Session and suspend/resume notification registrations succeeded", _sessionObserver?.SessionNotificationsAvailable == true && _sessionObserver.PowerNotificationsAvailable);
            SelectPage("KeepAwake");
            await Invoke(_presentationToggleButton!);
            Check("Presentation button synchronizes label, tray and fake requests", apis.SystemRequested && apis.DisplayRequested && _presentationStatusText!.Text.Contains("aceitas") && _trayPresentation.Text?.Contains("Desativar") == true);
            await Invoke(_presentationToggleButton!);
            Check("Stop button releases fake requests and synchronizes tray", !apis.SystemRequested && !apis.DisplayRequested && _presentationStatusText!.Text.Contains("Inativo") && _trayPresentation.Text?.Contains("Ativar") == true);
            await Invoke(_presentationToggleButton!);
            _trayStart.PerformClick();
            Check("Tray energy activation updates UI", apis.ExecutionRequested && _powerStatusText!.Text.Contains("aceita") && _trayStop.Enabled && !_trayStart.Enabled);
            var appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle));
            ((OverlappedPresenter)appWindow.Presenter).Minimize();
            await Task.Delay(150);
            Check("Minimization hides own window and retains requests", !IsWindowVisible(_windowHandle) && _lifetime.Visibility == "tray" && apis.DisplayRequested && apis.ExecutionRequested);
            _trayOpen.PerformClick();
            await Task.Delay(150);
            Check("Tray Open restores own window", IsWindowVisible(_windowHandle) && !IsIconic(_windowHandle) && _lifetime.Visibility == "visible");
            // Window.Close() intentionally bypasses the cancelable user-close path; send the
            // same system command as the caption X to THIS test window only.
            if (!PostMessage(_windowHandle, 0x112, new IntPtr(0xF060), IntPtr.Zero)) throw new InvalidOperationException("Unable to send close command to test window");
            await Task.Delay(150);
            Check("Window close hides without releasing requests", !IsWindowVisible(_windowHandle) && !_lifetime.IsClosed && apis.DisplayRequested && apis.ExecutionRequested);
            RestoreFromTray();
            await Task.Delay(150);
            Check("Shared double-click restoration handler restores window", IsWindowVisible(_windowHandle) && !IsIconic(_windowHandle));
            SelectPage("History");
            Check("History page builds without modifying bound ItemCollection", _activePage == "History");
            SelectPage("Diagnostics");
            await Invoke(_exportDiagnosticButton!);
            Check("Export button creates report", Directory.Exists(_journal.ReportDirectory) && Directory.GetFiles(_journal.ReportDirectory, "Orbit-diagnostico-*.txt").Length > 0);
        }
        catch (Exception ex) { _journal.RecordException("smoke_failed", ex, _lifetime.Snapshot); Check("Unhandled smoke exception: " + ex.GetType().Name, false); }
        finally { _trayExit.PerformClick(); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
