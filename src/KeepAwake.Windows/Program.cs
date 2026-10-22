namespace KeepAwake.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var instanceMutex = new Mutex(true, $@"Local\WindowsKeepAwake-{Environment.UserName}", out var createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();
        try
        {
            var form = new MainForm();
            if (args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
                form.Shown += (_, _) => form.HideToTray();
            Application.Run(form);
        }
        finally { instanceMutex.ReleaseMutex(); }
    }
}
