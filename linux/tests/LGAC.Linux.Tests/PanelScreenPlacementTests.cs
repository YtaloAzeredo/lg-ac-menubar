using Avalonia;
using LGAC.Linux.Tray;

namespace LGAC.Linux.Tests;

[TestClass]
public class PanelScreenPlacementTests
{
    private static readonly PixelRect Bounds = new(0, 0, 1920, 1080);

    [TestMethod]
    public void ScaleOneAnchorsToTheBottomRightAboveTheBar()
    {
        var workArea = new PixelRect(0, 0, 1920, 1040);
        var layout = PanelScreenPlacement.Compute(Bounds, workArea, 1, 400);

        Assert.AreEqual(new PixelPoint(1920 - 320 - 12, 1040 - 400 - 12), layout.Position);
        Assert.AreEqual(1040 - 24, layout.MaxHeightDips);
    }

    [TestMethod]
    public void ScaleOneAndAHalfConvertsDipsToPixels()
    {
        var bounds = new PixelRect(0, 0, 2880, 1620);
        var workArea = new PixelRect(0, 0, 2880, 1560);
        var layout = PanelScreenPlacement.Compute(bounds, workArea, 1.5, 400);

        Assert.AreEqual(new PixelPoint(2880 - 480 - 18, 1560 - 600 - 18), layout.Position);
        Assert.AreEqual((1560 - 36) / 1.5, layout.MaxHeightDips, 1e-9);
    }

    [TestMethod]
    public void ScaleTwoConvertsDipsToPixels()
    {
        var bounds = new PixelRect(0, 0, 3840, 2160);
        var workArea = new PixelRect(0, 80, 3840, 2080);
        var layout = PanelScreenPlacement.Compute(bounds, workArea, 2, 400);

        Assert.AreEqual(new PixelPoint(3840 - 640 - 24, 80 + 24), layout.Position);
        Assert.AreEqual((2080 - 48) / 2.0, layout.MaxHeightDips, 1e-9);
    }

    [TestMethod]
    public void TallContentIsCappedByTheUsableHeight()
    {
        var workArea = new PixelRect(0, 0, 1920, 600);
        var layout = PanelScreenPlacement.Compute(new PixelRect(0, 0, 1920, 640), workArea, 1, 5000);

        Assert.AreEqual(576, layout.MaxHeightDips);
        Assert.AreEqual(12, layout.Position.Y);
    }

    [TestMethod]
    public void InvalidScalingFallsBackToOne()
    {
        var workArea = new PixelRect(0, 0, 1920, 1040);
        var layout = PanelScreenPlacement.Compute(Bounds, workArea, 0, 400);

        Assert.AreEqual(new PixelPoint(1920 - 320 - 12, 1040 - 400 - 12), layout.Position);
    }
}
