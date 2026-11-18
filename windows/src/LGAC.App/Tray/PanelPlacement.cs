using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace LGAC.App.Tray;

public readonly record struct PanelAnchor(Rect WorkArea, Rect Anchor, double Scale);

public static class PanelPlacement
{
    private const int MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;
    private const double DefaultDpi = 96.0;

    private enum Edge
    {
        Left,
        Right,
        Top,
        Bottom,
    }

    public static Rect Compute(Rect workArea, Rect trayIcon, Size panel, double margin = 12)
    {
        var width = Math.Min(panel.Width, Math.Max(0, workArea.Width - 2 * margin));
        var height = Math.Min(panel.Height, Math.Max(0, workArea.Height - 2 * margin));
        var centerX = trayIcon.Left + trayIcon.Width / 2;
        var centerY = trayIcon.Top + trayIcon.Height / 2;
        double x;
        double y;
        switch (NearestEdge(workArea, centerX, centerY))
        {
            case Edge.Bottom:
                x = AlongEdge(centerX, width, workArea.Left, workArea.Right, margin);
                y = workArea.Bottom - height - margin;
                break;
            case Edge.Top:
                x = AlongEdge(centerX, width, workArea.Left, workArea.Right, margin);
                y = workArea.Top + margin;
                break;
            case Edge.Left:
                x = workArea.Left + margin;
                y = AlongEdge(centerY, height, workArea.Top, workArea.Bottom, margin);
                break;
            default:
                x = workArea.Right - width - margin;
                y = AlongEdge(centerY, height, workArea.Top, workArea.Bottom, margin);
                break;
        }

        return new Rect(x, y, width, height);
    }

    private static Edge NearestEdge(Rect workArea, double centerX, double centerY)
    {
        var beyond = new[]
        {
            (Edge.Left, workArea.Left - centerX),
            (Edge.Right, centerX - workArea.Right),
            (Edge.Top, workArea.Top - centerY),
            (Edge.Bottom, centerY - workArea.Bottom),
        };
        var outside = beyond.MaxBy(entry => entry.Item2);
        if (outside.Item2 > 0)
        {
            return outside.Item1;
        }

        var inside = new[]
        {
            (Edge.Left, centerX - workArea.Left),
            (Edge.Right, workArea.Right - centerX),
            (Edge.Top, centerY - workArea.Top),
            (Edge.Bottom, workArea.Bottom - centerY),
        };
        return inside.MinBy(entry => entry.Item2).Item1;
    }

    public static PanelAnchor AnchorFromCursor()
    {
        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        return FromPixels(screen.WorkingArea, screen.Bounds, cursor, ScaleAt(cursor));
    }

    public static PanelAnchor FromPixels(Rectangle workingArea, Rectangle bounds, Point cursor, double scale)
    {
        var anchor = workingArea.Contains(cursor)
            ? TaskbarEdgeAnchor(workingArea, bounds, cursor)
            : new Rectangle(cursor.X, cursor.Y, 1, 1);

        return new PanelAnchor(ToDips(workingArea, scale), ToDips(anchor, scale), scale);
    }

    private static Rectangle TaskbarEdgeAnchor(Rectangle workingArea, Rectangle bounds, Point cursor)
    {
        if (workingArea.Top > bounds.Top)
        {
            return new Rectangle(cursor.X, workingArea.Top - 1, 1, 1);
        }

        if (workingArea.Left > bounds.Left)
        {
            return new Rectangle(workingArea.Left - 1, cursor.Y, 1, 1);
        }

        if (workingArea.Right < bounds.Right)
        {
            return new Rectangle(workingArea.Right, cursor.Y, 1, 1);
        }

        return new Rectangle(cursor.X, workingArea.Bottom, 1, 1);
    }

    private static Rect ToDips(Rectangle pixels, double scale)
    {
        return new Rect(pixels.Left / scale, pixels.Top / scale, pixels.Width / scale, pixels.Height / scale);
    }

    private static double AlongEdge(double center, double length, double start, double end, double margin)
    {
        var low = start + margin;
        var high = end - length - margin;
        var value = center - length / 2;
        return Math.Max(low, Math.Min(value, high));
    }

    private static double ScaleAt(Point point)
    {
        try
        {
            var monitor = MonitorFromPoint(point, MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero
                && GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) == 0
                && dpiX > 0)
            {
                return dpiX / DefaultDpi;
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        return 1.0;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, int flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
