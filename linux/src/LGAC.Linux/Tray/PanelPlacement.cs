using Avalonia;

namespace LGAC.Linux.Tray;

public static class PanelPlacement
{
    public const int DefaultMargin = 12;

    public static PixelRect Compute(PixelRect screenBounds, PixelRect workArea, PixelSize panel, int margin = DefaultMargin)
    {
        var width = Math.Max(1, Math.Min(panel.Width, workArea.Width - 2 * margin));
        var height = Math.Max(1, Math.Min(panel.Height, workArea.Height - 2 * margin));
        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);

        var barOnTop = workArea.Y > screenBounds.Y;
        var barOnBottom = workArea.Bottom < screenBounds.Bottom;
        var barOnLeft = !barOnTop && !barOnBottom && workArea.X > screenBounds.X;

        var x = barOnLeft ? workArea.X + margin : workArea.Right - width - margin;
        var y = barOnTop ? workArea.Y + margin : workArea.Bottom - height - margin;

        x = Math.Clamp(x, workArea.X, workArea.Right - width);
        y = Math.Clamp(y, workArea.Y, workArea.Bottom - height);
        return new PixelRect(x, y, width, height);
    }
}
