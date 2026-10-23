using System.ComponentModel;
using System.Runtime.InteropServices;

namespace KeepAwake.Windows;

internal enum PowerSourceKind
{
    Unavailable,
    External,
    Battery,
    Unknown
}

internal sealed record PowerSupplySnapshot(
    PowerSourceKind Source,
    bool? HasBattery,
    byte? BatteryPercent,
    bool IsCharging,
    string? Error = null)
{
    internal static PowerSupplySnapshot Unavailable(string? error = null) =>
        new(PowerSourceKind.Unavailable, null, null, false, error);

    internal static PowerSupplySnapshot FromNativeStatus(bool succeeded, byte acLineStatus, byte batteryFlag, byte batteryPercent, string? error = null)
    {
        if (!succeeded) return Unavailable(error);

        var hasBattery = batteryFlag == 0x80 ? false : batteryFlag == 0xFF ? (bool?)null : true;
        byte? percent = batteryPercent <= 100 && hasBattery != false ? batteryPercent : null;
        var source = acLineStatus switch
        {
            0 => PowerSourceKind.Battery,
            1 => PowerSourceKind.External,
            _ => PowerSourceKind.Unknown
        };
        if (source == PowerSourceKind.Battery) hasBattery = true;

        return new PowerSupplySnapshot(source, hasBattery, percent, (batteryFlag & 0x08) != 0, error);
    }

    internal string Describe()
    {
        if (Source == PowerSourceKind.Unavailable) return "Estado de energia indisponível";
        var percentage = BatteryPercent is { } value ? $" · bateria {value}%" : "";
        var charging = IsCharging ? " · carregando" : "";
        return Source switch
        {
            PowerSourceKind.External when HasBattery == false => "Alimentação externa · sem bateria detectada",
            PowerSourceKind.External => "Conectado à tomada" + percentage + charging,
            PowerSourceKind.Battery when BatteryPercent is { } batteryValue => $"Na bateria — {batteryValue}%" + charging,
            PowerSourceKind.Battery => "Na bateria · percentual indisponível" + charging,
            _ when HasBattery == false => "Alimentação desconhecida · sem bateria detectada",
            _ => "Alimentação desconhecida" + percentage + charging
        };
    }
}

internal static class WindowsPowerStatusReader
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    internal static PowerSupplySnapshot Read()
    {
        if (GetSystemPowerStatus(out var status))
            return PowerSupplySnapshot.FromNativeStatus(true, status.ACLineStatus, status.BatteryFlag, status.BatteryLifePercent);

        var error = Marshal.GetLastWin32Error();
        return PowerSupplySnapshot.Unavailable(new Win32Exception(error).Message);
    }
}

internal sealed class PowerStatusIndicator : UserControl
{
    private readonly Label _power = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label _keepAwake = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolTip _toolTip = new() { ShowAlways = true };
    private string _powerDescription = "Estado de energia indisponível";
    private string _keepAwakeDescription = "Keep Awake: Inativo";
    private bool _compact;

    internal PowerSupplySnapshot Snapshot { get; private set; } = PowerSupplySnapshot.Unavailable();
    internal string PowerDescription => _powerDescription;
    internal string KeepAwakeDescription => _keepAwakeDescription;
    internal bool IsCompact => _compact;
    internal Label PowerLabel => _power;
    internal Label KeepAwakeLabel => _keepAwake;

    internal PowerStatusIndicator()
    {
        BackColor = AppTheme.Card;
        ForeColor = AppTheme.Text;
        Padding = new Padding(2);
        MinimumSize = new Size(0, 64);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AppTheme.Card,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        _power.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        _keepAwake.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        _power.ForeColor = AppTheme.Muted;
        _keepAwake.ForeColor = AppTheme.Muted;
        layout.Controls.Add(_power, 0, 0);
        layout.Controls.Add(_keepAwake, 0, 1);
        Controls.Add(layout);
        SetSnapshot(Snapshot);
        SetKeepAwakeActive(false);
    }

    internal void SetSnapshot(PowerSupplySnapshot snapshot)
    {
        Snapshot = snapshot;
        _powerDescription = snapshot.Describe();
        _power.Text = "⚡  " + _powerDescription;
        _power.AccessibleName = _powerDescription;
        _power.ForeColor = snapshot.Source == PowerSourceKind.Unavailable ? AppTheme.Warning : AppTheme.Text;
        if (!string.IsNullOrWhiteSpace(snapshot.Error)) _powerDescription += "\n" + snapshot.Error;
        UpdateDisplay();
    }

    internal void SetKeepAwakeActive(bool active)
    {
        _keepAwakeDescription = "Keep Awake: " + (active ? "Ativo" : "Inativo");
        _keepAwake.ForeColor = active ? AppTheme.GreenLight : AppTheme.Muted;
        _keepAwake.AccessibleName = _keepAwakeDescription;
        UpdateDisplay();
    }

    internal void SetCompact(bool compact)
    {
        if (_compact == compact) return;
        _compact = compact;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        _power.Text = _compact ? "⚡" : "⚡  " + _powerDescription.Split('\n')[0];
        _keepAwake.Text = _compact ? "◉" : "◉  " + _keepAwakeDescription;
        _toolTip.SetToolTip(_power, _powerDescription);
        _toolTip.SetToolTip(_keepAwake, _keepAwakeDescription);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _toolTip.Dispose();
        base.Dispose(disposing);
    }
}
