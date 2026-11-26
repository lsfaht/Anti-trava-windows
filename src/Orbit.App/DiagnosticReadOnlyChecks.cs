using Microsoft.Win32;

namespace Orbit.App;

internal static class DiagnosticReadOnlyChecks
{
    public static IEnumerable<string> Collect(bool sessionAvailable, bool powerAvailable)
    {
        var results = new List<string>
        {
            "Windows: " + Environment.OSVersion.Version,
            "Eventos WTS desta sessão: " + (sessionAvailable ? "registro disponível" : "indisponíveis; consultar evento de falha"),
            "Notificações de suspensão/retomada: " + (powerAvailable ? "registro disponível" : "registro indisponível; consultar evento de falha"),
            "powercfg /requests: não executado automaticamente; pode exigir elevação. Sem confirmação independente dos pedidos.",
            "gpresult e log Security: não consultados. Origem GPO/MDM e causa do bloqueio não determinadas."
        };
        Read(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "InactivityTimeoutSecs");
        foreach (var path in new[] { @"Control Panel\Desktop", @"Software\Policies\Microsoft\Windows\Control Panel\Desktop" })
            foreach (var name in new[] { "ScreenSaveActive", "ScreenSaveTimeOut", "ScreenSaverIsSecure" }) Read(Registry.CurrentUser, path, name);
        return results;

        void Read(RegistryKey root, string path, string name)
        {
            var label = root.Name + "\\" + path + "\\" + name;
            try
            {
                using var key = root.OpenSubKey(path, writable: false);
                var value = key?.GetValue(name)?.ToString();
                results.Add(label + ": " + (value is null ? "ausente" : long.TryParse(value, out var number) ? number.ToString() : "formato não numérico; omitido"));
            }
            catch (UnauthorizedAccessException) { results.Add(label + ": acesso negado"); }
            catch (System.Security.SecurityException) { results.Add(label + ": acesso negado"); }
            catch (Exception ex) { results.Add(label + ": indisponível (" + ex.GetType().Name + ")"); }
        }
    }
}
