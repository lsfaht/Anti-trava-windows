using Microsoft.Win32;

namespace KeepAwake.Windows;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowsKeepAwake";

    public static bool IsEnabled
    {
        get
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false); return key?.GetValue(ValueName) is string; }
            catch { return false; }
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Não foi possível abrir a configuração de inicialização do usuário.");
        if (!enabled) { key.DeleteValue(ValueName, throwOnMissingValue: false); return; }
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Não foi possível localizar o executável.");
        key.SetValue(ValueName, $"\"{executable}\" --minimized", RegistryValueKind.String);
    }
}
