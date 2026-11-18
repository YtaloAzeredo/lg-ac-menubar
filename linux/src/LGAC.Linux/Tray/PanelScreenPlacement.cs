using Avalonia;

namespace LGAC.Linux.Tray;

internal sealed record ScreenInfo(PixelRect Bounds, PixelRect WorkArea, double Scaling);

internal readonly record struct PanelLayout(PixelPoint Position, double MaxHeightDips);

internal static class PanelScreenPlacement
{
    public const double PanelWidthDips = 320;
    public const double MarginDips = 12;

    public static PanelLayout Compute(PixelRect screenBounds, PixelRect workArea, double scaling, double desiredHeightDips)
    {
        var scale = scaling > 0 && double.IsFinite(scaling) ? scaling : 1;
        var margin = (int)Math.Round(MarginDips * scale);
        var maxHeightDips = Math.Max(1, (workArea.Height - 2 * margin) / scale);
        var heightDips = Math.Clamp(desiredHeightDips, 1, maxHeightDips);
        var size = new PixelSize(
            (int)Math.Round(PanelWidthDips * scale),
            (int)Math.Round(heightDips * scale));
        var rect = PanelPlacement.Compute(screenBounds, workArea, size, margin);
        return new PanelLayout(rect.Position, maxHeightDips);
    }
}
