using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Tray;

namespace LGAC.Linux.Tests;

[TestClass]
public class ModeIconTests
{
    private static readonly string?[] Modes = ["COOL", "HEAT", "AIR_DRY", "FAN", "AUTO", null];

    private static byte[] Pixels(RenderTargetBitmap bitmap)
    {
        using var copy = new WriteableBitmap(bitmap.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var framebuffer = copy.Lock())
        {
            bitmap.CopyPixels(framebuffer);
            var bytes = new byte[framebuffer.RowBytes * framebuffer.Size.Height];
            System.Runtime.InteropServices.Marshal.Copy(framebuffer.Address, bytes, 0, bytes.Length);
            return bytes;
        }
    }

    private static int VisiblePixels(byte[] bgra)
    {
        var count = 0;
        for (var index = 3; index < bgra.Length; index += 4)
        {
            if (bgra[index] > 16)
            {
                count++;
            }
        }

        return count;
    }

    private static long Alpha(byte[] bgra)
    {
        long total = 0;
        for (var index = 3; index < bgra.Length; index += 4)
        {
            total += bgra[index];
        }

        return total;
    }

    [TestMethod]
    public async Task EveryModeRendersANonEmptyIconAtBothSizes()
    {
        await HeadlessApp.RunAsync(() =>
        {
            foreach (var mode in Modes)
            {
                foreach (var dimmed in new[] { false, true })
                {
                    foreach (var size in new[] { 22, 44 })
                    {
                        Assert.IsNotNull(ModeIcon.Render(mode, dimmed, size), mode);
                        using var bitmap = ModeIcon.RenderBitmap(mode, dimmed, size);
                        Assert.AreEqual(new PixelSize(size, size), bitmap.PixelSize);
                        Assert.IsGreaterThan(size, VisiblePixels(Pixels(bitmap)), $"{mode} {dimmed} {size}");
                    }
                }
            }

            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task DimmedIsLessOpaqueThanNormal()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var normal = ModeIcon.RenderBitmap("COOL", false, 44);
            using var dimmed = ModeIcon.RenderBitmap("COOL", true, 44);
            Assert.IsLessThan(Alpha(Pixels(normal)) * 0.6, Alpha(Pixels(dimmed)));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task DifferentModesDrawDifferentShapes()
    {
        await HeadlessApp.RunAsync(() =>
        {
            var distinct = new[] { "COOL", "HEAT", "AIR_DRY", "FAN", "AUTO" };
            var shapes = distinct.Select(mode =>
            {
                using var bitmap = ModeIcon.RenderBitmap(mode, false, 44);
                return Convert.ToBase64String(Pixels(bitmap));
            }).ToList();
            Assert.HasCount(distinct.Length, shapes.Distinct());
            using var unknown = ModeIcon.RenderBitmap(null, false, 44);
            using var auto = ModeIcon.RenderBitmap("AUTO", false, 44);
            CollectionAssert.AreEqual(Pixels(auto), Pixels(unknown));
            using var other = ModeIcon.RenderBitmap("SOMETHING_ELSE", false, 44);
            CollectionAssert.AreEqual(Pixels(auto), Pixels(other));
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task CapturesEveryModeOnLightAndDarkBackgrounds()
    {
        await HeadlessApp.RunAsync(() =>
        {
            foreach (var background in new[] { ("light", Color.Parse("#F2F2F2")), ("dark", Color.Parse("#2B2B2B")) })
            {
                var rows = new StackPanel { Background = new SolidColorBrush(background.Item2), Spacing = 8, Margin = new Thickness(8) };
                foreach (var dimmed in new[] { false, true })
                {
                    foreach (var size in new[] { 22, 44 })
                    {
                        var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
                        foreach (var mode in Modes)
                        {
                            row.Children.Add(new Image { Width = size, Height = size, Source = ModeIcon.RenderBitmap(mode, dimmed, size) });
                        }

                        rows.Children.Add(row);
                    }
                }

                var window = UiTesting.Host(rows, 330);
                window.SizeToContent = SizeToContent.WidthAndHeight;
                window.UpdateLayout();
                HeadlessApp.Capture(window, "mode-icons-" + background.Item1);
                window.Close();

                var zoom = new StackPanel { Background = new SolidColorBrush(background.Item2), Spacing = 8, Margin = new Thickness(8) };
                foreach (var dimmed in new[] { false, true })
                {
                    var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
                    foreach (var mode in Modes)
                    {
                        var image = new Image { Width = 132, Height = 132, Source = ModeIcon.RenderBitmap(mode, dimmed, 44) };
                        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
                        row.Children.Add(image);
                    }

                    zoom.Children.Add(row);
                }

                var zoomWindow = UiTesting.Host(zoom, 900);
                zoomWindow.SizeToContent = SizeToContent.WidthAndHeight;
                zoomWindow.UpdateLayout();
                HeadlessApp.Capture(zoomWindow, "mode-icons-zoom-" + background.Item1);
                zoomWindow.Close();
            }

            return Task.CompletedTask;
        });
    }
}
