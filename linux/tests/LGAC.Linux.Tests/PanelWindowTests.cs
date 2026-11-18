using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using LGAC.Core;
using LGAC.Core.Tests.Support;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests;

[TestClass]
public class PanelWindowTests
{
    [TestMethod]
    public async Task ShowsTheViewOfEachPhase()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using (var setup = PanelFixture.Create())
            {
                await setup.ToSetupAsync();
                var window = setup.Panel(true);
                Assert.IsInstanceOfType<SetupView>(window.CurrentView);
                window.CloseForShutdown();
            }

            using (var loading = PanelFixture.Create())
            {
                var window = loading.Panel(true);
                Assert.IsInstanceOfType<LoadingView>(window.CurrentView);
                window.CloseForShutdown();
            }

            using (var choosing = PanelFixture.Create())
            {
                await choosing.ToChoosingAsync();
                var window = choosing.Panel(true);
                Assert.IsInstanceOfType<DeviceChoiceView>(window.CurrentView);
                window.CloseForShutdown();
            }

            using (var ready = PanelFixture.Create(configured: true))
            {
                await ready.ToReadyAsync();
                var window = ready.Panel(true);
                window.ShowPanel();
                Assert.IsInstanceOfType<ControlPanelView>(window.CurrentView);
                Assert.Contains("Ar do Quarto",UiTesting.VisibleTexts((Control)window.CurrentView!));
                window.CloseForShutdown();
            }
        });
    }

    [TestMethod]
    public async Task SwitchesTheViewWhenThePhaseChangesAndOldViewStopsReacting()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToChoosingAsync(switching: true);
            var window = fixture.Panel(true);
            window.ShowPanel();
            var old = (DeviceChoiceView)window.CurrentView!;
            Assert.IsTrue(old.CancelButton.IsVisible);

            await fixture.Controller.CancelDeviceChoiceAsync();
            await PanelFixture.SettleAsync();
            Assert.IsInstanceOfType<ControlPanelView>(window.CurrentView);
            Assert.IsFalse(old.CancelButton.IsVisible);

            await fixture.Controller.ChooseAnotherDeviceAsync();
            await PanelFixture.SettleAsync();

            var current = (DeviceChoiceView)window.CurrentView!;
            Assert.AreNotSame(old, current);
            Assert.IsTrue(current.CancelButton.IsVisible);
            Assert.IsFalse(old.CancelButton.IsVisible);
            window.CloseForShutdown();
        });
    }

    [TestMethod]
    public async Task ReplacesSetupWithLoadingAndNewMessageCreatesANewView()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToSetupAsync();
            var window = fixture.Panel(true);
            window.ShowPanel();
            var first = window.CurrentView;

            await fixture.Controller.ConnectAsync("  ", "BR");
            await PanelFixture.SettleAsync();

            Assert.AreNotSame(first, window.CurrentView);
            Assert.AreEqual("Cole o token", ((SetupView)window.CurrentView!).MessageText.Text);
            window.CloseForShutdown();
        });
    }

    [TestMethod]
    public async Task TrayModeHasNoDecorationAndHidesWithEscape()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var window = fixture.Panel(true);

            Assert.AreEqual(320, window.Width);
            Assert.AreEqual(WindowDecorations.None, window.WindowDecorations);
            Assert.IsTrue(window.Topmost);
            Assert.IsFalse(window.ShowInTaskbar);
            Assert.AreEqual(SizeToContent.Height, window.SizeToContent);

            window.ShowPanel();
            Assert.IsTrue(window.IsVisible);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

            Assert.IsFalse(window.IsVisible);
            window.CloseForShutdown();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task WindowModeIgnoresEscapeAndHasATitle()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var window = fixture.Panel(false);

            Assert.AreEqual("LG AC", window.Title);
            Assert.AreEqual(WindowDecorations.Full, window.WindowDecorations);
            Assert.IsFalse(window.Topmost);
            Assert.IsTrue(window.ShowInTaskbar);

            window.ShowPanel();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Assert.IsTrue(window.IsVisible);

            window.Close();
            Assert.AreEqual(1, fixture.QuitCount);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task WindowModeCloseForShutdownDoesNotCallQuit()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var window = fixture.Panel(false);
            window.ShowPanel();

            window.CloseForShutdown();

            Assert.AreEqual(0, fixture.QuitCount);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task TrayModeCloseHidesInsteadOfClosing()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var window = fixture.Panel(true);
            window.ShowPanel();

            window.Close();
            await PanelFixture.SettleAsync();

            Assert.IsFalse(window.IsVisible);
            Assert.AreEqual(0, fixture.QuitCount);
            window.ShowPanel();
            Assert.IsTrue(window.IsVisible);
            window.CloseForShutdown();
        });
    }

    [TestMethod]
    public async Task ToggleRightAfterAFocusLossHideIsIgnored()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            long now = 10_000;
            var window = fixture.Panel(true, () => now);
            window.ShowPanel();
            now += 250;

            window.HandleDeactivated();
            Assert.IsFalse(window.IsVisible);

            now += 100;
            window.Toggle();
            Assert.IsFalse(window.IsVisible);

            now += 250;
            window.Toggle();
            Assert.IsTrue(window.IsVisible);
            window.CloseForShutdown();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ToggleAfterAnExplicitHideShowsAgain()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            long now = 10_000;
            var window = fixture.Panel(true, () => now);

            window.Toggle();
            Assert.IsTrue(window.IsVisible);
            window.Toggle();
            Assert.IsFalse(window.IsVisible);
            window.Toggle();
            Assert.IsTrue(window.IsVisible);
            window.HidePanel();
            window.Toggle();
            Assert.IsTrue(window.IsVisible);
            window.CloseForShutdown();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task WindowModeDoesNotHideOnDeactivation()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var window = fixture.Panel(false);
            window.ShowPanel();

            window.HandleDeactivated();

            Assert.IsTrue(window.IsVisible);
            window.CloseForShutdown();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ShowPanelRefreshesWithoutBlocking()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToReadyAsync();
            var window = fixture.Panel(true);
            var before = fixture.Client.StateCalls;
            fixture.Clock.Date += TimeSpan.FromSeconds(30);

            window.ShowPanel();
            await PanelFixture.SettleAsync();

            Assert.AreEqual(before + 1, fixture.Client.StateCalls);
            window.CloseForShutdown();
        });
    }

    [TestMethod]
    public async Task ContentScrollsWhenItDoesNotFitAndShowPanelScrollsToTop()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToSetupAsync();
            var window = fixture.Panel(true);
            window.SetMaxPanelHeight(150);
            window.ShowPanel();
            window.UpdateLayout();

            Assert.IsLessThanOrEqualTo(150.5, window.Bounds.Height);
            var scroller = window.Scroller;
            Assert.IsGreaterThan(scroller.Viewport.Height, scroller.Extent.Height);

            scroller.Offset = new Avalonia.Vector(0, 40);
            window.UpdateLayout();
            Assert.AreEqual(40, scroller.Offset.Y);
            window.HidePanel();
            window.ShowPanel();
            window.UpdateLayout();
            Assert.AreEqual(0, scroller.Offset.Y);
            window.CloseForShutdown();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task HeightFollowsTheContentWhenThereIsRoom()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToSetupAsync();
            var window = fixture.Panel(true);
            window.SetMaxPanelHeight(2000);
            window.ShowPanel();
            window.UpdateLayout();

            Assert.IsGreaterThan(150, window.Bounds.Height);
            Assert.IsLessThan(2000, window.Bounds.Height);
            Assert.AreEqual(320, window.Bounds.Width);
            window.SetMaxPanelHeight(150);
            window.UpdateLayout();
            Assert.IsLessThanOrEqualTo(150.5, window.DesiredSize.Height);
            window.CloseForShutdown();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task CapturesEachPhaseInTrayMode()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using (var setup = PanelFixture.Create())
            {
                await setup.ToSetupAsync("Token inválido ou sem permissão");
                var window = setup.Panel(true);
                window.ShowPanel();
                UiTesting.CaptureBoth(window, "panel-setup");
                window.CloseForShutdown();
            }

            using (var loading = PanelFixture.Create(configured: true))
            {
                await loading.ToLoadingWithMessageAsync();
                var window = loading.Panel(true);
                window.ShowPanel();
                UiTesting.CaptureBoth(window, "panel-loading");
                window.CloseForShutdown();
            }

            using (var choosing = PanelFixture.Create())
            {
                await choosing.ToChoosingAsync();
                var window = choosing.Panel(true);
                window.ShowPanel();
                UiTesting.CaptureBoth(window, "panel-devices");
                window.CloseForShutdown();
            }

            using (var ready = PanelFixture.Create(configured: true))
            {
                ready.Clock.Date = DateTimeOffset.UtcNow;
                await ready.ToReadyAsync();
                var window = ready.Panel(false);
                window.ShowPanel();
                UiTesting.CaptureBoth(window, "panel-ready-window");
                window.CloseForShutdown();

                var tray = ready.Panel(true);
                tray.ShowPanel();
                UiTesting.CaptureBoth(tray, "panel-ready-tray");
                tray.CloseForShutdown();
            }
        });
    }

    [TestMethod]
    public async Task HidePanelAndToggleInWindowModeNeverHide()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var window = fixture.Panel(false);
            window.ShowPanel();

            window.HidePanel();
            Assert.IsTrue(window.IsVisible);
            window.Toggle();
            Assert.IsTrue(window.IsVisible);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ToggleInWindowModeNeverHides()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var window = fixture.Panel(false);

            window.Toggle();
            Assert.IsTrue(window.IsVisible);
            window.Toggle();
            Assert.IsTrue(window.IsVisible);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ShowPanelRestoresMinimizedWindow()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var window = fixture.Panel(false);
            window.ShowPanel();
            window.WindowState = WindowState.Minimized;
            Assert.AreEqual(WindowState.Minimized, window.WindowState);

            window.ShowPanel();

            Assert.AreEqual(WindowState.Normal, window.WindowState);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task DeactivationRightAfterShowIsIgnored()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            long now = 10_000;
            var window = fixture.Panel(true, () => now);
            window.ShowPanel();

            now += 150;
            window.HandleDeactivated();
            Assert.IsTrue(window.IsVisible);

            now += 100;
            window.HandleDeactivated();
            Assert.IsFalse(window.IsVisible);
            await Task.CompletedTask;
        });
    }

    private static async Task<(PanelFixture Fixture, PanelWindow Window, Func<long> Advance, Action<bool> SetActive)> OpenLoadingPanelAsync()
    {
        var fixture = PanelFixture.Create(configured: true);
        await fixture.ToLoadingWithMessageAsync();
        long now = 10_000;
        var active = false;
        var window = fixture.Panel(true, () => now, () => active);
        window.ShowPanel();
        now += 500;
        return (fixture, window, () => now += 500, value => active = value);
    }

    private static void OpenMenu(PanelWindow window)
    {
        var view = (LoadingView)window.CurrentView!;
        UiTesting.Click(window, view.SettingsButton);
        Assert.IsTrue(view.HasOpenPopup);
    }

    [TestMethod]
    public async Task OpenPopupKeepsTheTrayPanelVisibleOnDeactivation()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var (fixture, window, _, _) = await OpenLoadingPanelAsync();
            using (fixture)
            {
                OpenMenu(window);

                window.HandleDeactivated();

                Assert.IsTrue(window.IsVisible);
            }
        });
    }

    [TestMethod]
    public async Task ClosingThePopupWhileInactiveHidesThePanelOnRecheck()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var (fixture, window, _, setActive) = await OpenLoadingPanelAsync();
            using (fixture)
            {
                OpenMenu(window);
                window.HandleDeactivated();
                setActive(false);

                ((LoadingView)window.CurrentView!).OpenMenu!.Close();
                Assert.IsTrue(window.IsVisible);
                window.Recheck();

                Assert.IsFalse(window.IsVisible);
            }
        });
    }

    [TestMethod]
    public async Task PanelReactivatedBeforeTheRecheckStaysVisible()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var (fixture, window, _, setActive) = await OpenLoadingPanelAsync();
            using (fixture)
            {
                OpenMenu(window);
                setActive(false);
                ((LoadingView)window.CurrentView!).OpenMenu!.Close();

                setActive(true);
                window.Recheck();

                Assert.IsTrue(window.IsVisible);
            }
        });
    }

    [TestMethod]
    public async Task RecheckKeepsThePanelWhileAPopupIsStillOpen()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var (fixture, window, _, setActive) = await OpenLoadingPanelAsync();
            using (fixture)
            {
                OpenMenu(window);
                setActive(false);

                window.Recheck();

                Assert.IsTrue(window.IsVisible);
            }
        });
    }

    [TestMethod]
    public async Task DeactivationInsideTheShowGuardIsRecheckedLater()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            long now = 10_000;
            var active = true;
            var window = fixture.Panel(true, () => now, () => active);
            window.ShowPanel();
            now += 100;

            window.HandleDeactivated();
            Assert.IsTrue(window.IsVisible);

            window.Recheck();
            Assert.IsTrue(window.IsVisible);

            active = false;
            window.HandleDeactivated();
            window.Recheck();
            Assert.IsFalse(window.IsVisible);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task RecheckHideCountsAsAFocusLossForTheToggleGuard()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            long now = 10_000;
            var window = fixture.Panel(true, () => now, () => false);
            window.ShowPanel();
            now += 100;
            window.HandleDeactivated();

            window.Recheck();
            Assert.IsFalse(window.IsVisible);
            now += 100;
            window.Toggle();

            Assert.IsFalse(window.IsVisible);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ViewReplacedWhileAPopupIsOpenStillGuardsTheNewView()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var (fixture, window, _, setActive) = await OpenLoadingPanelAsync();
            using (fixture)
            {
                OpenMenu(window);
                var old = (LoadingView)window.CurrentView!;
                var menu = old.OpenMenu!;

                fixture.Client.SetState(LGAC.Core.Tests.Support.Fixture.Json("state-full"));
                await fixture.Controller.RefreshAsync(force: true);
                await PanelFixture.SettleAsync();
                Assert.IsInstanceOfType<ControlPanelView>(window.CurrentView);

                setActive(false);
                window.HandleDeactivated();
                Assert.IsFalse(window.IsVisible);
                Assert.IsFalse(menu.IsOpen);
                window.Recheck();
            }
        });
    }
}
