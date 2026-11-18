using Avalonia.Controls;
using LGAC.Core;
using LGAC.Core.Tests.Support;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests;

[TestClass]
public class LoadingViewTests
{
    [TestMethod]
    public async Task WithoutMessageShowsOnlyTheIndicator()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            using var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);

            Assert.IsTrue(view.Spinner.IsVisible);
            Assert.IsFalse(view.MessagePanel.IsVisible);
            Assert.IsFalse(view.RetryButton.IsEffectivelyVisible);
            Assert.IsFalse(view.SettingsButton.IsEffectivelyVisible);
            Assert.IsFalse(view.SwitchDeviceButton.IsVisible);
            window.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task WithMessageShowsTextRetryAndSettings()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToLoadingWithMessageAsync();
            using var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);

            Assert.IsNotNull(fixture.Controller.StatusMessage);
            Assert.IsTrue(view.MessagePanel.IsVisible);
            Assert.AreEqual(fixture.Controller.StatusMessage, view.MessageText.Text);
            Assert.AreEqual("Tentar de novo", view.RetryButton.Content);
            Assert.IsTrue(view.RetryButton.IsEffectivelyVisible);
            Assert.IsTrue(view.SettingsButton.IsEffectivelyVisible);
            window.Close();
        });
    }

    [TestMethod]
    public async Task BackArrowExistsOnlyWhenTheDeviceCanBeSwitched()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            fixture.ToLoadingHeld();
            using var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);

            Assert.IsTrue(fixture.Controller.CanSwitchDevice);
            Assert.IsTrue(view.SwitchDeviceButton.IsVisible);
            Assert.IsFalse(view.MessagePanel.IsVisible);
            window.Close();

            using var bare = PanelFixture.Create();
            using var bareView = new LoadingView(bare.Services);
            var bareWindow = UiTesting.Host(bareView);
            Assert.IsFalse(bareView.SwitchDeviceButton.IsVisible);
            bareWindow.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ReactsToControllerChanges()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            using var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);
            Assert.IsFalse(view.MessagePanel.IsVisible);

            await fixture.ToLoadingWithMessageAsync();
            await PanelFixture.SettleAsync();

            Assert.IsTrue(view.MessagePanel.IsVisible);
            Assert.AreEqual(fixture.Controller.StatusMessage, view.MessageText.Text);
            Assert.IsTrue(view.SwitchDeviceButton.IsVisible);
            window.Close();
        });
    }

    [TestMethod]
    public async Task StopsReactingAfterDispose()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);
            view.Dispose();

            await fixture.ToLoadingWithMessageAsync();
            await PanelFixture.SettleAsync();

            Assert.IsFalse(view.MessagePanel.IsVisible);
            window.Close();
        });
    }

    [TestMethod]
    public async Task RetryRefreshesAgain()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToLoadingWithMessageAsync();
            using var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);
            var before = fixture.Client.StateCalls;
            fixture.Client.SetState(Fixture.Json("state-full"));

            UiTesting.Click(window, view.RetryButton);
            await PanelFixture.SettleAsync();

            Assert.IsGreaterThan(before, fixture.Client.StateCalls);
            Assert.AreEqual(new ACPhase.Ready(), fixture.Controller.Phase);
            window.Close();
        });
    }

    [TestMethod]
    public async Task BackArrowChoosesAnotherDevice()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToLoadingWithMessageAsync();
            using var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);

            UiTesting.Click(window, view.SwitchDeviceButton);
            await PanelFixture.SettleAsync();

            Assert.IsInstanceOfType<ACPhase.ChoosingDevice>(fixture.Controller.Phase);
            Assert.IsTrue(fixture.Controller.IsSwitchingDevice);
            window.Close();
        });
    }

    [TestMethod]
    public async Task SettingsButtonOpensTheMenu()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToLoadingWithMessageAsync();
            using var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);

            UiTesting.Click(window, view.SettingsButton);
            await PanelFixture.SettleAsync();

            Assert.IsNotNull(view.OpenMenu);
            Assert.IsTrue(view.OpenMenu.IsOpen);
            view.Dispose();
            Assert.IsFalse(view.OpenMenu?.IsOpen ?? false);
            window.Close();
        });
    }

    [TestMethod]
    public async Task CapturesTheLoadingScreens()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            using var view = new LoadingView(fixture.Services);
            var window = UiTesting.Host(view);
            UiTesting.CaptureBoth(window, "loading");
            window.Close();

            using var failing = PanelFixture.Create(configured: true);
            await failing.ToLoadingWithMessageAsync();
            using var failingView = new LoadingView(failing.Services);
            var failingWindow = UiTesting.Host(failingView);
            UiTesting.CaptureBoth(failingWindow, "loading-message");
            failingWindow.Close();
        });
    }

    [TestMethod]
    public async Task SettingsMenuCountsAsAnOpenPopup()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToLoadingWithMessageAsync();
            using var view = new LoadingView(fixture.Services);
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

    [TestMethod]
    public async Task SpinnerAnimatesOnlyWhileTheWindowIsShown()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var view = new LoadingView(fixture.Services);
            Assert.IsFalse(view.IsSpinning);

            var window = UiTesting.Host(view);
            Assert.IsTrue(view.IsSpinning);

            window.Hide();
            Assert.IsFalse(view.IsSpinning);

            window.Show();
            Assert.IsTrue(view.IsSpinning);

            view.Dispose();
            Assert.IsFalse(view.IsSpinning);
            await Task.CompletedTask;
        });
    }
}
