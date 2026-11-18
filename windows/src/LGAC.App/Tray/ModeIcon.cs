using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LGAC.App.Tray;

public static class ModeIcon
{
    private const float Grid = 16f;
    private const int DimmedAlpha = 90;

    public static Icon Render(string? jobMode, bool dimmed, bool lightTaskbar, int size)
    {
        size = Math.Max(size, 8);
        var baseColor = lightTaskbar ? Color.FromArgb(32, 32, 32) : Color.FromArgb(245, 245, 245);
        var color = dimmed ? Color.FromArgb(DimmedAlpha, baseColor) : baseColor;

        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);
            graphics.ScaleTransform(size / Grid, size / Grid);

            using var brush = new SolidBrush(color);
            using var pen = new Pen(color, 1.4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            switch (jobMode)
            {
                case "COOL":
                    DrawSnowflake(graphics, pen);
                    break;
                case "HEAT":
                    DrawSun(graphics, pen);
                    break;
                case "AIR_DRY":
                    DrawDrop(graphics, brush);
                    break;
                case "FAN":
                    DrawFan(graphics, brush);
                    break;
                default:
                    DrawThermometer(graphics, brush, pen);
                    break;
            }
        }

        return IconFromBitmap(bitmap);
    }

    private static Icon IconFromBitmap(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static PointF Polar(float radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new PointF(8f + radius * (float)Math.Cos(radians), 8f + radius * (float)Math.Sin(radians));
    }

    private static void DrawSnowflake(Graphics graphics, Pen pen)
    {
        for (var arm = 0; arm < 6; arm++)
        {
            var angle = arm * 60.0;
            var tip = Polar(6.6f, angle);
            graphics.DrawLine(pen, new PointF(8f, 8f), tip);
            var branchBase = Polar(4.4f, angle);
            graphics.DrawLine(pen, branchBase, Polar(4.4f + 2.0f, angle + 38.0));
            graphics.DrawLine(pen, branchBase, Polar(4.4f + 2.0f, angle - 38.0));
        }
    }

    private static void DrawSun(Graphics graphics, Pen pen)
    {
        graphics.DrawEllipse(pen, 8f - 2.9f, 8f - 2.9f, 5.8f, 5.8f);
        for (var ray = 0; ray < 8; ray++)
        {
            var angle = ray * 45.0;
            graphics.DrawLine(pen, Polar(4.9f, angle), Polar(7.1f, angle));
        }
    }

    private static void DrawDrop(Graphics graphics, Brush brush)
    {
        using var path = new GraphicsPath();
        path.AddBezier(new PointF(8f, 1.4f), new PointF(6.6f, 5f), new PointF(3.4f, 7f), new PointF(3.4f, 10f));
        path.AddArc(3.4f, 5.6f, 9.2f, 9.2f, 180f, -180f);
        path.AddBezier(new PointF(12.6f, 10f), new PointF(12.6f, 7f), new PointF(9.4f, 5f), new PointF(8f, 1.4f));
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    private static void DrawFan(Graphics graphics, Brush brush)
    {
        var original = graphics.Transform;
        for (var blade = 0; blade < 3; blade++)
        {
            graphics.Transform = original;
            graphics.TranslateTransform(8f, 8f);
            graphics.RotateTransform(blade * 120f);
            graphics.TranslateTransform(-8f, -8f);
            graphics.FillEllipse(brush, 5.7f, 1.0f, 4.6f, 6.4f);
        }

        graphics.Transform = original;
        graphics.FillEllipse(brush, 8f - 1.5f, 8f - 1.5f, 3f, 3f);
        original.Dispose();
    }

    private static void DrawThermometer(Graphics graphics, Brush brush, Pen pen)
    {
        using var stem = new GraphicsPath();
        stem.AddArc(6.3f, 1.6f, 3.4f, 3.4f, 180f, 180f);
        stem.AddLine(9.7f, 3.3f, 9.7f, 9.4f);
        stem.AddLine(6.3f, 9.4f, 6.3f, 3.3f);
        stem.CloseFigure();
        graphics.DrawPath(pen, stem);
        graphics.FillEllipse(brush, 8f - 3.1f, 14.3f - 6.2f, 6.2f, 6.2f);
        graphics.FillRectangle(brush, 7.3f, 6.0f, 1.4f, 5.0f);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
