using System.Drawing;
using System.Windows;
using LGAC.App.Tray;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;
using Point = System.Drawing.Point;

namespace LGAC.App.Tests;

[TestClass]
public sealed class PanelPlacementTests
{
    private static readonly Size Panel = new(360, 480);

    [TestMethod]
    public void TaskbarAtBottomPlacesPanelAboveIcon()
    {
        var workArea = new Rect(0, 0, 1920, 1032);
        var icon = new Rect(1700, 1040, 24, 24);

        var result = PanelPlacement.Compute(workArea, icon, Panel);

        Assert.AreEqual(workArea.Bottom - Panel.Height - 12, result.Top);
        Assert.AreEqual(1712 - Panel.Width / 2, result.Left);
        AssertContained(workArea, result);
    }

    [TestMethod]
    public void TaskbarAtTopPlacesPanelBelowIcon()
    {
        var workArea = new Rect(0, 40, 1920, 1040);
        var icon = new Rect(1700, 8, 24, 24);

        var result = PanelPlacement.Compute(workArea, icon, Panel);

        Assert.AreEqual(workArea.Top + 12, result.Top);
        AssertContained(workArea, result);
    }

    [TestMethod]
    public void TaskbarAtLeftPlacesPanelBesideIcon()
    {
        var workArea = new Rect(48, 0, 1872, 1080);
        var icon = new Rect(12, 1000, 24, 24);

        var result = PanelPlacement.Compute(workArea, icon, Panel);

        Assert.AreEqual(workArea.Left + 12, result.Left);
        AssertContained(workArea, result);
    }

    [TestMethod]
    public void TaskbarAtRightPlacesPanelBesideIcon()
    {
        var workArea = new Rect(0, 0, 1872, 1080);
        var icon = new Rect(1884, 1000, 24, 24);

        var result = PanelPlacement.Compute(workArea, icon, Panel);

        Assert.AreEqual(workArea.Right - Panel.Width - 12, result.Left);
        AssertContained(workArea, result);
    }

    [TestMethod]
    public void IconNearCornerKeepsPanelInsideWorkArea()
    {
        var workArea = new Rect(0, 0, 1920, 1032);
        var icon = new Rect(1890, 1040, 24, 24);

        var result = PanelPlacement.Compute(workArea, icon, Panel);

        Assert.AreEqual(workArea.Right - Panel.Width - 12, result.Left);
        AssertContained(workArea, result);
    }

    [TestMethod]
    public void IconNearLeftCornerKeepsPanelInsideWorkArea()
    {
        var workArea = new Rect(0, 0, 1920, 1032);
        var icon = new Rect(0, 1040, 24, 24);

        var result = PanelPlacement.Compute(workArea, icon, Panel);

        Assert.AreEqual(workArea.Left + 12, result.Left);
        AssertContained(workArea, result);
    }

    [TestMethod]
    public void SecondaryMonitorWithNegativeOriginStaysInsideItsWorkArea()
    {
        var workArea = new Rect(-1920, -200, 1920, 1032);
        var icon = new Rect(-300, 840, 24, 24);

        var result = PanelPlacement.Compute(workArea, icon, Panel);

        Assert.AreEqual(workArea.Bottom - Panel.Height - 12, result.Top);
        AssertContained(workArea, result);
    }

    [TestMethod]
    public void PanelLargerThanWorkAreaIsShrunkInside()
    {
        var workArea = new Rect(0, 0, 911, 464);
        var icon = new Rect(700, 470, 24, 24);

        var result = PanelPlacement.Compute(workArea, icon, Panel);

        Assert.AreEqual(440, result.Height);
        Assert.AreEqual(360, result.Width);
        AssertContained(workArea, result);
    }

    [TestMethod]
    public void FromPixelsConvertsToDipsWithNegativeOrigin()
    {
        var bounds = new Rectangle(-2880, 0, 2880, 1620);
        var working = new Rectangle(-2880, 0, 2880, 1560);

        var result = PanelPlacement.FromPixels(working, bounds, new Point(-300, 1590), 1.5);

        Assert.AreEqual(1.5, result.Scale);
        Assert.AreEqual(-1920, result.WorkArea.Left, 0.001);
        Assert.AreEqual(1920, result.WorkArea.Width, 0.001);
        Assert.AreEqual(1040, result.WorkArea.Height, 0.001);
        Assert.AreEqual(-200, result.Anchor.Left, 0.001);
        Assert.AreEqual(1060, result.Anchor.Top, 0.001);
    }

    [TestMethod]
    public void CursorInsideWorkAreaAnchorsToTaskbarEdge()
    {
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var working = new Rectangle(0, 0, 1920, 1032);

        var result = PanelPlacement.FromPixels(working, bounds, new Point(1800, 1000), 1);

        Assert.AreEqual(1032, result.Anchor.Top, 0.001);
        Assert.AreEqual(1800, result.Anchor.Left, 0.001);
        var panel = PanelPlacement.Compute(result.WorkArea, result.Anchor, Panel);
        Assert.AreEqual(result.WorkArea.Bottom - Panel.Height - 12, panel.Top, 0.001);
    }

    [TestMethod]
    public void CursorInsideWorkAreaWithTaskbarOnLeftAnchorsToLeftEdge()
    {
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var working = new Rectangle(48, 0, 1872, 1080);

        var result = PanelPlacement.FromPixels(working, bounds, new Point(100, 500), 1);

        Assert.AreEqual(47, result.Anchor.Left, 0.001);
        var panel = PanelPlacement.Compute(result.WorkArea, result.Anchor, Panel);
        Assert.AreEqual(result.WorkArea.Left + 12, panel.Left, 0.001);
    }

    [TestMethod]
    public void CursorInsideWorkAreaWithHiddenTaskbarAssumesBottom()
    {
        var bounds = new Rectangle(0, 0, 1920, 1080);

        var result = PanelPlacement.FromPixels(bounds, bounds, new Point(1800, 1000), 1);

        Assert.AreEqual(1080, result.Anchor.Top, 0.001);
    }

    private static void AssertContained(Rect workArea, Rect panel)
    {
        Assert.IsGreaterThanOrEqualTo(workArea.Left, panel.Left);
        Assert.IsGreaterThanOrEqualTo(workArea.Top, panel.Top);
        Assert.IsLessThanOrEqualTo(workArea.Right, panel.Right);
        Assert.IsLessThanOrEqualTo(workArea.Bottom, panel.Bottom);
    }
}
