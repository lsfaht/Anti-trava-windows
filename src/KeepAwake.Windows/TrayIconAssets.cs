using System.Drawing;

namespace KeepAwake.Windows;

internal static class TrayIconAssets
{
    private const string ResourceName = "KeepAwake.Windows.Assets.WindowsKeepAwake.ico";

    internal static Icon Load()
    {
        using var stream = typeof(TrayIconAssets).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"O recurso de ícone '{ResourceName}' não foi incorporado à aplicação.");
        using var source = new Icon(stream);
        return (Icon)source.Clone();
    }
}
