using Avalonia.Controls;
using Avalonia.Interactivity;
using LGAC.Core;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests;

[TestClass]
public class SettingsMenuTests
{
    private static MenuItem Item(ContextMenu menu, string header)
    {
        return menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header);
    }

    private static void Click(MenuItem item)
    {
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    [TestMethod]
    public async Task HasAllItemsWhenTheDeviceCanBeSwitched()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToReadyAsync();

            var menu = SettingsMenu.Build(fixture.Services);

            Assert.IsTrue(fixture.Controller.CanSwitchDevice);
            var items = menu.Items.Cast<Control>().ToList();
            Assert.HasCount(5, items);
            Assert.AreEqual("Trocar aparelho…", ((MenuItem)items[0]).Header);
            Assert.AreEqual("Trocar token…", ((MenuItem)items[1]).Header);
            Assert.AreEqual("Iniciar com o sistema", ((MenuItem)items[2]).Header);
            Assert.IsInstanceOfType<Separator>(items[3]);
            Assert.AreEqual("Sair", ((MenuItem)items[4]).Header);
        });
    }

    [TestMethod]
    public async Task OmitsSwitchDeviceWhenItCannotBeSwitched()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();

            var menu = SettingsMenu.Build(fixture.Services);

            Assert.IsFalse(fixture.Controller.CanSwitchDevice);
            var headers = menu.Items.OfType<MenuItem>().Select(item => (string?)item.Header).ToList();
            CollectionAssert.AreEqual(new string?[] { "Trocar token…", "Iniciar com o sistema", "Sair" }, headers);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task AutostartItemCreatesAndRemovesTheDesktopEntry()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var menu = SettingsMenu.Build(fixture.Services);
            var item = Item(menu, "Iniciar com o sistema");
            Assert.AreEqual(MenuItemToggleType.CheckBox, item.ToggleType);
            Assert.IsFalse(item.IsChecked);

            Click(item);
            Assert.IsTrue(File.Exists(fixture.AutostartPath));
            Assert.IsTrue(fixture.Autostart.IsEnabled);
            Assert.IsTrue(item.IsChecked);

            Click(item);
            Assert.IsFalse(File.Exists(fixture.AutostartPath));
            Assert.IsFalse(item.IsChecked);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task AutostartItemStartsCheckedWhenTheEntryExists()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            fixture.Autostart.IsEnabled = true;

            var menu = SettingsMenu.Build(fixture.Services);

            Assert.IsTrue(Item(menu, "Iniciar com o sistema").IsChecked);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task QuitCallsTheQuitAction()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var menu = SettingsMenu.Build(fixture.Services);

            Click(Item(menu, "Sair"));

            Assert.AreEqual(1, fixture.QuitCount);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task SwitchTokenResetsTheCredentials()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToReadyAsync();
            var menu = SettingsMenu.Build(fixture.Services);

            Click(Item(menu, "Trocar token…"));

            Assert.AreEqual(new ACPhase.NeedsSetup(null), fixture.Controller.Phase);
            Assert.IsNull(fixture.Store.Token);
        });
    }

    [TestMethod]
    public async Task SwitchDeviceListsTheDevicesAgain()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToReadyAsync();
            var menu = SettingsMenu.Build(fixture.Services);

            Click(Item(menu, "Trocar aparelho…"));
            await PanelFixture.SettleAsync();

            Assert.IsInstanceOfType<ACPhase.ChoosingDevice>(fixture.Controller.Phase);
            Assert.IsTrue(fixture.Controller.IsSwitchingDevice);
        });
    }

    [TestMethod]
    public async Task CapturesTheOpenMenu()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToReadyAsync();
            var anchor = new Button { Content = "Configurações", Margin = new Avalonia.Thickness(10) };
            var window = UiTesting.Host(new StackPanel { Height = 220, Children = { anchor } });
            var menu = SettingsMenu.Build(fixture.Services);
            menu.Placement = Avalonia.Controls.PlacementMode.Bottom;
            menu.Open(anchor);
            await PanelFixture.SettleAsync();

            UiTesting.CaptureBoth(window, "settings-menu");
            menu.Close();
            window.Close();
        });
    }
}
