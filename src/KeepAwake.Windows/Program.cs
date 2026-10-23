namespace KeepAwake.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var mutexName = $@"Local\WindowsKeepAwake-{Environment.UserName}";
        var activationEventName = mutexName + "-Activate";
        using var instanceMutex = new Mutex(true, mutexName, out var createdNew);
        if (!createdNew)
        {
            if (!InstanceActivationSignal.TrySignalExisting(activationEventName))
            {
                ApplicationConfiguration.Initialize();
                MessageBox.Show(
                    "O Windows Keep Awake já está em execução, mas não foi possível solicitar a restauração da janela. " +
                    "Use 'Abrir janela' no ícone da bandeja. Se o ícone não responder, selecione 'Sair' e inicie novamente.",
                    "Windows Keep Awake já está aberto",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        try
        {
            var form = new MainForm();
            _ = form.Handle;
            using var activationSignal = new InstanceActivationSignal(activationEventName, () =>
            {
                if (form.IsDisposed || !form.IsHandleCreated) return;
                try { form.BeginInvoke((Action)form.HandleTrayDoubleClick); }
                catch (InvalidOperationException) { }
            });
            if (args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
                form.Shown += (_, _) => form.HideToTray();
            Application.Run(form);
        }
        finally { instanceMutex.ReleaseMutex(); }
    }
}
