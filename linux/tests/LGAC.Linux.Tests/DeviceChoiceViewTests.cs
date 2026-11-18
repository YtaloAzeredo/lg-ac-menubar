using Avalonia.Controls;
using LGAC.Core;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests;

[TestClass]
public class DeviceChoiceViewTests
{
    private static DeviceChoiceView Create(PanelFixture fixture)
    {
        var devices = ((ACPhase.ChoosingDevice)fixture.Controller.Phase).Devices;
        return new DeviceChoiceView(fixture.Services, devices);
    }

    [TestMethod]
    public async Task ListsAliasAndModelOfEachDevice()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToChoosingAsync();
            using var view = Create(fixture);
            var window = UiTesting.Host(view);

            Assert.AreEqual(2, view.DeviceList.Children.Count);
            var texts = UiTesting.VisibleTexts(view);
            CollectionAssert.IsSubsetOf(
                new[] { "Escolha o ar-condicionado", "Quarto", "M1", "Sala", "M2" },
                texts.ToList());
            window.Close();
        });
    }

    [TestMethod]
    public async Task DeviceWithoutAliasShowsTheModelAsTitle()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToChoosingAsync();
            using var view = new DeviceChoiceView(
                fixture.Services,
                [new ThinQDevice("x", "DEVICE_AIR_CONDITIONER", "", "MODELO-9")]);
            var window = UiTesting.Host(view);

            var texts = UiTesting.VisibleTexts(view).Where(text => text == "MODELO-9").ToList();
            Assert.HasCount(2, texts);
            window.Close();
        });
    }

    [TestMethod]
    public async Task ClickingADeviceSelectsIt()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToChoosingAsync();
            using var view = Create(fixture);
            var window = UiTesting.Host(view);

            UiTesting.Click(window, (Button)view.DeviceList.Children[1]);
            await PanelFixture.SettleAsync();

            Assert.AreEqual("ac-2", fixture.Store.DeviceId);
            Assert.AreEqual("Sala", fixture.Store.DeviceAlias);
            Assert.AreEqual(new ACPhase.Ready(), fixture.Controller.Phase);
            window.Close();
        });
    }

    [TestMethod]
    public async Task CancelExistsOnlyWhenSwitchingDevices()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var firstFixture = PanelFixture.Create();
            await firstFixture.ToChoosingAsync();
            using var first = Create(firstFixture);
            var firstWindow = UiTesting.Host(first);
            Assert.IsFalse(first.CancelButton.IsVisible);
            firstWindow.Close();

            using var secondFixture = PanelFixture.Create(configured: true);
            await secondFixture.ToChoosingAsync(switching: true);
            using var second = Create(secondFixture);
            var secondWindow = UiTesting.Host(second);
            Assert.IsTrue(second.CancelButton.IsVisible);
            Assert.AreEqual("Cancelar", second.CancelButton.Content);

            UiTesting.Click(secondWindow, second.CancelButton);
            await PanelFixture.SettleAsync();

            Assert.IsFalse(secondFixture.Controller.IsSwitchingDevice);
            Assert.AreEqual(new ACPhase.Ready(), secondFixture.Controller.Phase);
            secondWindow.Close();
        });
    }

    [TestMethod]
    public async Task ReactsToControllerChangesUntilDisposed()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToChoosingAsync(switching: true);
            var view = Create(fixture);
            var window = UiTesting.Host(view);
            Assert.IsTrue(view.CancelButton.IsVisible);

            view.Dispose();
            await fixture.Controller.CancelDeviceChoiceAsync();
            await PanelFixture.SettleAsync();

            Assert.IsFalse(fixture.Controller.IsSwitchingDevice);
            Assert.IsTrue(view.CancelButton.IsVisible);
            window.Close();
        });
    }

    [TestMethod]
    public async Task CancelFollowsIsSwitchingDeviceWhileAlive()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToChoosingAsync(switching: true);
            using var view = Create(fixture);
            var window = UiTesting.Host(view);

            await fixture.Controller.CancelDeviceChoiceAsync();
            await PanelFixture.SettleAsync();

            Assert.IsFalse(view.CancelButton.IsVisible);
            window.Close();
        });
    }

    [TestMethod]
    public async Task CapturesTheDeviceChoiceScreens()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToChoosingAsync();
            using var view = Create(fixture);
            var window = UiTesting.Host(view);
            UiTesting.CaptureBoth(window, "devices");
            window.Close();

            using var switching = PanelFixture.Create(configured: true);
            await switching.ToChoosingAsync(switching: true);
            using var switchingView = Create(switching);
            var switchingWindow = UiTesting.Host(switchingView);
            UiTesting.CaptureBoth(switchingWindow, "devices-switching");
            switchingWindow.Close();
        });
    }

    [TestMethod]
    public async Task SettingsMenuCountsAsAnOpenPopup()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToChoosingAsync();
            using var view = Create(fixture);
            var window = UiTesting.Host(view);
            var closed = 0;
            view.PopupClosed += (_, _) => closed++;
            Assert.IsFalse(view.HasOpenPopup);

            UiTesting.Click(window, view.SettingsButton);
            Assert.IsTrue(view.HasOpenPopup);
            view.OpenMenu!.Close();

            Assert.IsFalse(view.HasOpenPopup);
            Assert.AreEqual(1, closed);
        });
    }
}
