using Avalonia;
using LGAC.Linux.Tray;

namespace LGAC.Linux.Tests;

[TestClass]
public sealed class PanelPlacementTests
{
    private static readonly PixelSize Panel = new(300, 200);

    [TestMethod]
    public void BarOnTopPlacesTopRight()
    {
        var screen = new PixelRect(0, 0, 1920, 1080);
        var work = new PixelRect(0, 32, 1920, 1048);

        var result = PanelPlacement.Compute(screen, work, Panel, 12);

        Assert.AreEqual(new PixelRect(1920 - 300 - 12, 32 + 12, 300, 200), result);
    }

    [TestMethod]
    public void BarOnBottomPlacesBottomRight()
    {
        var screen = new PixelRect(0, 0, 1920, 1080);
        var work = new PixelRect(0, 0, 1920, 1040);

        var result = PanelPlacement.Compute(screen, work, Panel, 12);

        Assert.AreEqual(new PixelRect(1920 - 300 - 12, 1040 - 200 - 12, 300, 200), result);
    }

    [TestMethod]
    public void BarOnLeftPlacesLeftSide()
    {
        var screen = new PixelRect(0, 0, 1920, 1080);
        var work = new PixelRect(48, 0, 1872, 1080);

        var result = PanelPlacement.Compute(screen, work, Panel, 12);

        Assert.AreEqual(new PixelRect(48 + 12, 1080 - 200 - 12, 300, 200), result);
    }

    [TestMethod]
    public void BarOnTopWithDockOnLeftPlacesTopRight()
    {
        var screen = new PixelRect(0, 0, 1920, 1080);
        var work = new PixelRect(70, 32, 1850, 1048);

        var result = PanelPlacement.Compute(screen, work, new PixelSize(320, 400), 12);

        Assert.AreEqual(new PixelPoint(1588, 44), result.Position);
    }

    [TestMethod]
    public void BarsOnTopAndBottomPlaceTopRight()
    {
        var screen = new PixelRect(0, 0, 1920, 1080);
        var work = new PixelRect(0, 32, 1920, 1000);

        var result = PanelPlacement.Compute(screen, work, new PixelSize(320, 400), 12);

        Assert.AreEqual(new PixelPoint(1920 - 320 - 12, 32 + 12), result.Position);
    }

    [TestMethod]
    public void BarOnlyOnLeftKeepsBottomLeftCorner()
    {
        var screen = new PixelRect(0, 0, 1920, 1080);
        var work = new PixelRect(70, 0, 1850, 1080);

        var result = PanelPlacement.Compute(screen, work, new PixelSize(320, 400), 12);

        Assert.AreEqual(new PixelPoint(70 + 12, 1080 - 400 - 12), result.Position);
    }

    [TestMethod]
    public void NegativeOriginMonitor()
    {
        var screen = new PixelRect(-1920, -200, 1920, 1080);
        var work = new PixelRect(-1920, -168, 1920, 1048);

        var result = PanelPlacement.Compute(screen, work, Panel, 12);

        Assert.AreEqual(new PixelRect(-300 - 12, -168 + 12, 300, 200), result);
    }

    [TestMethod]
    public void OversizedPanelIsShrunkInsideWorkArea()
    {
        var screen = new PixelRect(0, 0, 400, 300);
        var work = new PixelRect(0, 0, 400, 300);

        var result = PanelPlacement.Compute(screen, work, new PixelSize(900, 700), 12);

        Assert.AreEqual(new PixelRect(12, 12, 376, 276), result);
        Assert.IsTrue(work.Contains(result.TopLeft) && work.Contains(result.BottomRight - new PixelPoint(1, 1)));
    }

    [TestMethod]
    public void TinyWorkAreaNeverEscapes()
    {
        var work = new PixelRect(10, 10, 20, 20);

        var result = PanelPlacement.Compute(work, work, Panel, 12);

        Assert.IsTrue(result.X >= work.X && result.Y >= work.Y);
        Assert.IsTrue(result.Right <= work.Right && result.Bottom <= work.Bottom);
        Assert.IsTrue(result.Width >= 1 && result.Height >= 1);
    }
}
