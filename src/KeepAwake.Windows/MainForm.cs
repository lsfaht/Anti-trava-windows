using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using KeepAwake.Core;

namespace KeepAwake.Windows;

internal sealed class MainForm : Form
{
    private readonly PowerRequest _request;
    private readonly Action<string>? _logSink;
    private readonly Action<string>? _errorPresenter;
    private readonly Label _status = new() { AutoSize = true, Text = "Inativa" };
    private readonly Label _session = new() { AutoSize = true };
    private readonly Button _toggle = new() { Text = "Iniciar manutenção" };
    private readonly TextBox _filters = new() { Width = 420, PlaceholderText = "Ex.: chrome; excel (processo ou parte do título)" };
    private readonly CheckBox _display = new() { Text = "Manter também a tela ligada", Checked = true, AutoSize = true };
    private readonly ListView _windows = new() { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill };
    private readonly TextBox _report = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private readonly string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsKeepAwake", "diagnostics.log");

    public MainForm() : this(null, null, null) { }

    internal MainForm(PowerRequest? request, Action<string>? logSink, Action<string>? errorPresenter)
    {
        _request = request ?? new PowerRequest(new WindowsExecutionStateApi());
        _logSink = logSink;
        _errorPresenter = errorPresenter;
        Text = "Windows Keep Awake — diagnóstico";
        Width = 820; Height = 640; MinimumSize = new Size(700, 500);
        _windows.Columns.Add("Processo", 170); _windows.Columns.Add("Título da janela (somente em memória)", 450); _windows.Columns.Add("Estado", 90);
        _toggle.Click += (_, _) => ToggleRequest();
        _timer.Tick += (_, _) => RefreshStatus();
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(10), AutoSize = false };
        top.Controls.AddRange([_toggle, _status, _display, new Label { Text = "Filtro opcional (; separa itens):", AutoSize = true, Padding = new Padding(5) }, _filters]);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var activity = new TabPage("Janelas e sessão");
        var activityLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        activityLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); activityLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); activityLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        activityLayout.Controls.Add(_session, 0, 0); activityLayout.Controls.Add(_windows, 0, 1);
        activityLayout.Controls.Add(new Label { Text = "A manutenção de energia não impede protetor de tela ou bloqueio por política.", AutoSize = true, Padding = new Padding(4) }, 0, 2);
        activity.Controls.Add(activityLayout);
        var diagnostics = new TabPage("Diagnóstico");
        var diagnose = new Button { Text = "Atualizar relatório", Dock = DockStyle.Top, Height = 34 };
        diagnose.Click += (_, _) => _report.Text = BuildReport();
        diagnostics.Controls.Add(_report); diagnostics.Controls.Add(diagnose);
        tabs.TabPages.Add(activity); tabs.TabPages.Add(diagnostics);
        Controls.Add(tabs); Controls.Add(top);
        FormClosing += (_, _) => { _timer.Stop(); try { _request.Dispose(); } catch (Exception ex) { Log("Falha ao liberar estado de energia: " + ex.Message); } };
        Shown += (_, _) => { _timer.Start(); RefreshStatus(); _report.Text = BuildReport(); };
    }

    internal string StatusText => _status.Text;
    internal string ToggleText => _toggle.Text;
    internal Button ToggleButton => _toggle;
    internal TextBox FilterInput => _filters;
    internal IReadOnlyList<WindowInfo> DisplayedWindows => _windows.Items.Cast<ListViewItem>()
        .Select(item => new WindowInfo(item.Text, item.SubItems[1].Text, item.SubItems[2].Text == "Ativa"))
        .ToArray();

    private void ToggleRequest()
    {
        try
        {
            if (_request.IsEnabled) { _request.Disable(); _status.Text = "Inativa"; _toggle.Text = "Iniciar manutenção"; Log("Solicitação de energia liberada pelo usuário."); }
            else { _request.Enable(_display.Checked); _status.Text = "Ativa"; _toggle.Text = "Interromper manutenção"; Log("Solicitação de energia ativada pelo usuário."); }
        }
        catch (Exception ex)
        {
            _status.Text = "Erro";
            Log("Erro de SetThreadExecutionState: " + ex.Message);
            if (_errorPresenter is null)
                MessageBox.Show(this, ex.Message, "Falha ao alterar solicitação", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else
                _errorPresenter(ex.Message);
        }
    }

    internal void RefreshStatus()
    {
        try
        {
            var all = WindowEnumerator.GetVisibleWindows();
            var matches = WindowFilter.Apply(all, _filters.Text);
            _windows.BeginUpdate(); _windows.Items.Clear();
            foreach (var w in matches)
                _windows.Items.Add(new ListViewItem([w.ProcessName, w.Title, w.IsForeground ? "Ativa" : "Visível"]));
            _windows.EndUpdate();
            var foreground = all.FirstOrDefault(w => w.IsForeground);
            var idle = SessionState.GetIdleTime();
            _session.Text = $"Desktop: {SessionState.GetDesktopState()} | Última entrada: {(idle.HasValue ? $"{idle.Value.TotalSeconds:F0} s atrás" : "indisponível")} | Janelas visíveis: {matches.Count}/{all.Count}" + (foreground is null ? "" : $" | Ativa: {foreground.ProcessName}");
        }
        catch (Exception ex) { _session.Text = "Estado da sessão indisponível: " + ex.Message; }
    }

    private string BuildReport()
    {
        var b = new StringBuilder();
        b.AppendLine("Windows Keep Awake — relatório de diagnóstico (somente leitura)");
        b.AppendLine("Coletado em: " + DateTimeOffset.Now.ToString("O"));
        b.AppendLine("\nConfirmado pela aplicação:");
        b.AppendLine("• SetThreadExecutionState solicita ES_SYSTEM_REQUIRED e, opcionalmente, ES_DISPLAY_REQUIRED enquanto ativada.");
        b.AppendLine("• Esta API não simula presença, não impede protetor de tela e não substitui políticas de bloqueio.");
        b.AppendLine("• A enumeração de janelas consulta apenas visibilidade, processo e título; não lê conteúdo de janelas.");
        b.AppendLine("\nValores consultados (ausente ou inacessível será indicado):");
        ReadRegistry(b, Registry.CurrentUser, @"Control Panel\Desktop", "ScreenSaveActive", "ScreenSaveTimeOut", "ScreenSaverIsSecure");
        ReadRegistry(b, Registry.CurrentUser, @"Software\Policies\Microsoft\Windows\Control Panel\Desktop", "ScreenSaveActive", "ScreenSaveTimeOut", "ScreenSaverIsSecure");
        ReadRegistry(b, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "InactivityTimeoutSecs");
        ReadRegistry(b, Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Control Panel\Desktop", "ScreenSaveActive", "ScreenSaveTimeOut", "ScreenSaverIsSecure");
        b.AppendLine("\nInterpretação e limites:");
        b.AppendLine("• Suspensão é transição de energia do sistema; desligar o monitor mantém o Windows em execução.");
        b.AppendLine("• Protetor de tela é uma preferência visual; a opção segura pode bloquear ao retornar.");
        b.AppendLine("• InactivityTimeoutSecs define um limite de inatividade para bloqueio de segurança quando aplicado; sua presença e valor, isoladamente, não provam a causa do bloqueio observado.");
        b.AppendLine("• Um bloqueio em cerca de 2 minutos pode vir de política local/domínio, protetor de tela ou ferramenta de gerenciamento corporativo. Este programa não consegue identificar qual mecanismo venceu sem evidência adicional.");
        b.AppendLine("• gpresult /scope computer /z retornou Acesso negado, conforme informado. Este relatório não tenta elevar privilégios nem ler logs de segurança protegidos.");
        b.AppendLine("• Eventos relevantes podem estar nos logs System, Security ou operacionais de gerenciamento; a leitura pode exigir autorização administrativa. Nenhum evento foi confirmado por este relatório.");
        b.AppendLine("\nSolicite ao suporte/administrador autorizado verificar a política efetiva de bloqueio interativo (incluindo tempo limite de máquina), protetor de tela seguro e ferramentas de gerenciamento aplicadas. Não altere políticas corporativas localmente.");
        b.AppendLine("\nLog local: " + _logPath);
        Log("Relatório somente leitura atualizado.");
        return b.ToString();
    }

    private static void ReadRegistry(StringBuilder b, RegistryKey root, string path, params string[] names)
    {
        b.AppendLine($"[{root.Name}\\{path}]");
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            foreach (var name in names)
            {
                if (key is null) { b.AppendLine($"  {name}: chave ausente ou sem acesso"); continue; }
                try { b.AppendLine($"  {name}: {key.GetValue(name, "<não definido>")}"); }
                catch (Exception ex) { b.AppendLine($"  {name}: inacessível ({ex.GetType().Name})"); }
            }
        }
        catch (Exception ex) { b.AppendLine("  inacessível (" + ex.GetType().Name + ")"); }
    }

    private void Log(string message)
    {
        if (_logSink is not null) { _logSink(message); return; }
        try { Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!); File.AppendAllText(_logPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); } catch { }
    }
}

internal static class SessionState
{
    [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder info, uint length, out uint needed);

    public static TimeSpan? GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return null;
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    public static string GetDesktopState()
    {
        const uint desktopReadObjects = 0x0001;
        const int uoiName = 2;
        var handle = OpenInputDesktop(0, false, desktopReadObjects);
        if (handle == IntPtr.Zero) return "indisponível (" + Marshal.GetLastWin32Error() + ")";
        try
        {
            var name = new StringBuilder(128);
            return GetUserObjectInformation(handle, uoiName, name, (uint)(name.Capacity * 2), out _)
                ? name.ToString().Equals("Winlogon", StringComparison.OrdinalIgnoreCase) ? "tela segura/bloqueio provável" : "desktop interativo"
                : "indisponível";
        }
        finally { CloseDesktop(handle); }
    }
}
