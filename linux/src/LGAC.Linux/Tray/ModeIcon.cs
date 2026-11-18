using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace LGAC.Linux.Tray;

internal static class ModeIcon
{
    private const double DimmedOpacity = 0.45;
    private const double OutlinePixels = 1.1;
    private static readonly IBrush Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Color Outline = Color.FromArgb(235, 24, 24, 24);

    public static WindowIcon Render(string? jobMode, bool dimmed, int size)
    {
        using var bitmap = RenderBitmap(jobMode, dimmed, size);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        stream.Position = 0;
        return new WindowIcon(stream);
    }

    internal static RenderTargetBitmap RenderBitmap(string? jobMode, bool dimmed, int size)
    {
        size = Math.Max(size, 8);
        var solid = RenderSolid(jobMode, size);
        if (!dimmed)
        {
            return solid;
        }

        using (solid)
        {
            var faded = new RenderTargetBitmap(new PixelSize(size, size));
            using var context = faded.CreateDrawingContext(true);
            using (context.PushOpacity(DimmedOpacity))
            {
                var area = new Rect(0, 0, size, size);
                context.DrawImage(solid, area, area);
            }

            return faded;
        }
    }

    private static RenderTargetBitmap RenderSolid(string? jobMode, int size)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        var source = FindGeometry(KeyFor(jobMode));
        if (source is null)
        {
            return bitmap;
        }

        var geometry = new GeometryGroup { FillRule = FillRule.NonZero, Children = [source] };
        var bounds = geometry.Bounds;
        var padding = OutlinePixels + 0.5;
        var available = Math.Max(1, size - 2 * padding);
        var scale = available / Math.Max(bounds.Width, bounds.Height);
        var offsetX = (size - bounds.Width * scale) / 2 - bounds.X * scale;
        var offsetY = (size - bounds.Height * scale) / 2 - bounds.Y * scale;
        var pen = new Pen(new SolidColorBrush(Outline), 2 * OutlinePixels / scale, lineJoin: PenLineJoin.Round);

        using var context = bitmap.CreateDrawingContext(true);
        using (context.PushTransform(new Matrix(scale, 0, 0, scale, offsetX, offsetY)))
        {
            context.DrawGeometry(null, pen, geometry);
            context.DrawGeometry(Fill, null, geometry);
        }

        return bitmap;
    }

    private static string KeyFor(string? jobMode)
    {
        return jobMode switch
        {
            "COOL" => "Icon.ModeCool",
            "HEAT" => "Icon.ModeHeat",
            "AIR_DRY" => "Icon.ModeDry",
            "FAN" => "Icon.ModeFan",
            _ => "Icon.ModeAuto",
        };
    }

    private static Geometry? FindGeometry(string key)
    {
        if (Application.Current is { } application && application.TryFindResource(key, out var value))
        {
            return value as Geometry;
        }

        return null;
    }
}
