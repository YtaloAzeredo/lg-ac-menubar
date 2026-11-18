using Avalonia.Controls;
using LGAC.Core;
using LGAC.Linux.Tests.Support;
using SkiaSharp;

namespace LGAC.Linux.Tests;

[TestClass]
public class HeadlessSessionTests
{
    [TestMethod]
    public async Task HeadlessSessionRendersAWindow()
    {
        string path = "";
        await HeadlessApp.RunAsync(() =>
        {
            var window = new Window
            {
                Width = 240,
                Height = 120,
                Content = new TextBlock { Text = "LG AC", FontSize = 32 },
            };
            window.Show();
            path = HeadlessApp.Capture(window, "headless-session");
            window.Close();
            return Task.CompletedTask;
        });

        Assert.IsTrue(File.Exists(path));
        using var bitmap = SKBitmap.Decode(path);
        var first = bitmap.GetPixel(0, 0);
        var distinct = false;
        for (var y = 0; y < bitmap.Height && !distinct; y++)
        {
            for (var x = 0; x < bitmap.Width && !distinct; x++)
            {
                distinct = bitmap.GetPixel(x, y) != first;
            }
        }

        Assert.IsTrue(distinct, "A captura tem uma cor só");
    }

    [TestMethod]
    public void InvariantModeFormatsHalfDegree()
    {
        Assert.AreEqual(string.Empty, System.Globalization.CultureInfo.CurrentCulture.Name);
        Assert.AreEqual("23,5°", ACLabels.Temperature(23.5));
    }
}
