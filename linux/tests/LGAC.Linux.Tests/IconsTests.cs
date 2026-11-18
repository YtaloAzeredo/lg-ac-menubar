using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using LGAC.Linux.Tests.Support;

namespace LGAC.Linux.Tests;

[TestClass]
public class IconsTests
{
    private static readonly string[] Keys =
    [
        "Icon.Power", "Icon.Minus", "Icon.Plus", "Icon.Back", "Icon.Refresh", "Icon.Settings",
        "Icon.SwingVertical", "Icon.SwingHorizontal", "Icon.Light", "Icon.Timer",
        "Icon.ModeCool", "Icon.ModeHeat", "Icon.ModeDry", "Icon.ModeFan", "Icon.ModeAuto",
    ];

    [TestMethod]
    public async Task EveryIconIsAGeometryWithBounds()
    {
        await HeadlessApp.RunAsync(() =>
        {
            foreach (var key in Keys)
            {
                Assert.IsTrue(Application.Current!.TryFindResource(key, out var value), key);
                var geometry = value as Geometry;
                Assert.IsNotNull(geometry, key);
                Assert.IsGreaterThan(8, geometry.Bounds.Width, key);
                Assert.IsGreaterThan(1, geometry.Bounds.Height, key);
            }

            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task CapturesTheIconSheet()
    {
        await HeadlessApp.RunAsync(() =>
        {
            var grid = new UniformGrid { Columns = 5, Margin = new Thickness(12) };
            foreach (var key in Keys)
            {
                var label = new TextBlock { Text = key["Icon.".Length..], FontSize = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                var icon = new PathIcon { Width = 32, Height = 32, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                icon.Bind(PathIcon.DataProperty, icon.GetResourceObservable(key));
                var small = new PathIcon { Width = 16, Height = 16, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                small.Bind(PathIcon.DataProperty, small.GetResourceObservable(key));
                var cell = new StackPanel { Margin = new Thickness(6), Spacing = 4 };
                cell.Children.Add(icon);
                cell.Children.Add(small);
                cell.Children.Add(label);
                grid.Children.Add(cell);
            }

            var window = UiTesting.Host(grid, 440);
            UiTesting.CaptureBoth(window, "icons");
            window.Close();
            return Task.CompletedTask;
        });
    }
}
