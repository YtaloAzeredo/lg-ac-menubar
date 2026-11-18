using Avalonia;
using LGAC.Core;
using LGAC.Core.Tests.Support;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Tray;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests;

[TestClass]
public class AppCompositionTests
{
    private sealed class Harness : IDisposable
    {
        private readonly PanelFixture fixture;

        public Harness(bool trayAvailable, bool configured = true, bool failTray = false, Func<ScreenInfo?>? screen = null)
        {
            fixture = PanelFixture.Create(configured);
            Tray = new FakeTrayHost();
            Composition = new AppComposition(
                fixture.Controller,
                fixture.Store,
                fixture.Autostart,
                trayAvailable,
                (toggle, show, quit) =>
                {
                    if (failTray)
                    {
                        throw new InvalidOperationException("sem bandeja");
                    }

                    TrayCreations++;
                    Toggle = toggle;
                    Show = show;
                    Quit = quit;
                    return Tray;
                },
                () => ShutdownCount++,
                TimeSpan.Zero,
                screen);
            Panel = Composition.Start();
        }

        public PanelFixture Fixture => fixture;

        public AppComposition Composition { get; }

        public PanelWindow Panel { get; }

        public FakeTrayHost Tray { get; }

        public int ShutdownCount { get; private set; }

        public int TrayCreations { get; private set; }

        public Action? Toggle { get; private set; }

        public Action? Show { get; private set; }

        public Action? Quit { get; private set; }

        public void Dispose()
        {
            Composition.Quit();
            fixture.Dispose();
        }
    }

    private sealed class Probe : IDisposable
    {
        public int Disposed { get; private set; }

        public void Dispose()
        {
            Disposed++;
        }
    }

    [TestMethod]
    public async Task WithoutTrayTheWindowIsVisibleAndClosingShutsDownOnce()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var harness = new Harness(trayAvailable: false);
            Assert.IsTrue(harness.Panel.IsVisible);
            Assert.AreEqual(0, harness.TrayCreations);
            Assert.IsNotNull(harness.Panel.Icon);

            harness.Panel.Close();

            Assert.AreEqual(1, harness.ShutdownCount);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task WithTrayTheWindowStartsHiddenAndToggleShowsAndHides()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var harness = new Harness(trayAvailable: true);
            Assert.AreEqual(1, harness.TrayCreations);
            Assert.IsFalse(harness.Panel.IsVisible);

            harness.Toggle!();
            Assert.IsTrue(harness.Panel.IsVisible);
            Assert.IsTrue(double.IsFinite(harness.Panel.MaxHeight));

            harness.Toggle!();
            Assert.IsFalse(harness.Panel.IsVisible);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task TrayShowActionShowsThePanel()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var harness = new Harness(trayAvailable: true);
            harness.Show!();
            Assert.IsTrue(harness.Panel.IsVisible);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ActivationShowsThePanelInBothModes()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var tray = new Harness(trayAvailable: true);
            tray.Composition.Activate();
            Assert.IsTrue(tray.Panel.IsVisible);

            using var window = new Harness(trayAvailable: false);
            window.Panel.Hide();
            window.Composition.Activate();
            Assert.IsTrue(window.Panel.IsVisible);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task QuitTwiceIsIdempotentAndDisposesEverything()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var harness = new Harness(trayAvailable: true);
            var probe = new Probe();
            harness.Composition.Own(probe);
            harness.Toggle!();

            harness.Composition.Quit();
            harness.Composition.Quit();
            harness.Quit!();

            Assert.AreEqual(1, harness.ShutdownCount);
            Assert.AreEqual(1, harness.Tray.DisposeCount);
            Assert.AreEqual(1, probe.Disposed);
            Assert.IsFalse(harness.Panel.IsVisible);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task OwnedAfterQuitIsDisposedImmediately()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var harness = new Harness(trayAvailable: false);
            harness.Composition.Quit();
            var probe = new Probe();
            harness.Composition.Own(probe);
            Assert.AreEqual(1, probe.Disposed);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task FailureCreatingTheTrayFallsBackToTheWindow()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var harness = new Harness(trayAvailable: true, failTray: true);
            Assert.IsTrue(harness.Panel.IsVisible);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ResumeRefreshesWithForceAfterTheDelay()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var harness = new Harness(trayAvailable: false);
            await harness.Fixture.Controller.BootstrapAsync();
            var before = harness.Fixture.Client.StateCalls;
            harness.Fixture.Clock.Date = harness.Fixture.Clock.Date.AddSeconds(1);

            await harness.Composition.RefreshAfterResumeAsync();

            Assert.IsGreaterThan(before, harness.Fixture.Client.StateCalls);
        });
    }

    [TestMethod]
    public async Task ResumeWaitIsCancelledByQuit()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var composition = new AppComposition(
                fixture.Controller,
                fixture.Store,
                fixture.Autostart,
                false,
                (_, _, _) => new FakeTrayHost(),
                () => { },
                TimeSpan.FromSeconds(30));
            composition.Start();
            await fixture.Controller.BootstrapAsync();
            var before = fixture.Client.StateCalls;

            var pending = composition.RefreshAfterResumeAsync();
            composition.Quit();
            await pending.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(before, fixture.Client.StateCalls);
        });
    }

    [TestMethod]
    public async Task TwoResumeSignalsProduceOneRefresh()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var fixture = PanelFixture.Create(configured: true);
            using var _ = fixture;
            var composition = new AppComposition(
                fixture.Controller,
                fixture.Store,
                fixture.Autostart,
                false,
                (_, _, _) => new FakeTrayHost(),
                () => { },
                TimeSpan.FromMilliseconds(150));
            composition.Start();
            await fixture.Controller.BootstrapAsync();

            var start = fixture.Client.StateCalls;
            fixture.Clock.Date = fixture.Clock.Date.AddSeconds(10);
            await composition.RefreshAfterResumeAsync();
            var single = fixture.Client.StateCalls - start;
            Assert.IsGreaterThan(0, single);

            start = fixture.Client.StateCalls;
            fixture.Clock.Date = fixture.Clock.Date.AddSeconds(10);
            var first = composition.RefreshAfterResumeAsync();
            var second = composition.RefreshAfterResumeAsync();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(single, fixture.Client.StateCalls - start);
            composition.Quit();
        });
    }

    [TestMethod]
    public async Task BootstrapOnlyStartsAfterThePanelExistsAndTheTokenIsPrimedOffTheUiThread()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var fixture = PanelFixture.Create(configured: true);
            using var _ = fixture;
            AppComposition? composition = null;
            bool? panelExisted = null;
            bool? onUiThread = null;
            int? callsWhenPrimed = null;
            composition = new AppComposition(
                fixture.Controller,
                fixture.Store,
                fixture.Autostart,
                false,
                (_, _, _) => new FakeTrayHost(),
                () => { });
            composition.Start();

            await composition.BootstrapAsync(() =>
            {
                panelExisted = composition.Panel is not null;
                onUiThread = Avalonia.Threading.Dispatcher.UIThread.CheckAccess();
                callsWhenPrimed = fixture.Client.StateCalls;
            });

            Assert.IsTrue(panelExisted);
            Assert.IsFalse(onUiThread);
            Assert.AreEqual(0, callsWhenPrimed);
            Assert.IsInstanceOfType<ACPhase.Ready>(fixture.Controller.Phase);
            composition.Quit();
        });
    }

    [TestMethod]
    public async Task BootstrapSurvivesAFailingPrime()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var fixture = PanelFixture.Create(configured: true);
            using var _ = fixture;
            var composition = new AppComposition(
                fixture.Controller,
                fixture.Store,
                fixture.Autostart,
                false,
                (_, _, _) => new FakeTrayHost(),
                () => { });
            composition.Start();

            await composition.BootstrapAsync(() => throw new InvalidOperationException("cofre"));

            composition.Quit();
        });
    }

    private static ScreenInfo WorkScreen()
    {
        return new ScreenInfo(new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 1);
    }

    [TestMethod]
    public async Task TrayPanelIsPlacedInsideTheWorkArea()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var harness = new Harness(trayAvailable: true, screen: WorkScreen);
            harness.Toggle!();

            Assert.AreEqual(1920 - 320 - 12, harness.Panel.Position.X);
            Assert.IsGreaterThanOrEqualTo(0, harness.Panel.Position.Y);
            Assert.IsLessThan(1040, harness.Panel.Position.Y);
            Assert.AreEqual(1040 - 24, harness.Panel.MaxHeight);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task FailingScreenProviderStillShowsThePanel()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var harness = new Harness(
                trayAvailable: true,
                screen: () => throw new InvalidOperationException("sem tela"));

            harness.Toggle!();
            Assert.IsTrue(harness.Panel.IsVisible);
            harness.Toggle!();
            Assert.IsFalse(harness.Panel.IsVisible);
            harness.Show!();
            Assert.IsTrue(harness.Panel.IsVisible);
            harness.Panel.HidePanel();
            harness.Composition.Activate();
            Assert.IsTrue(harness.Panel.IsVisible);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task VisiblePanelIsRepositionedWhenItsSizeChanges()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var calls = 0;
            using var harness = new Harness(
                trayAvailable: true,
                screen: () =>
                {
                    calls++;
                    return WorkScreen();
                });
            harness.Toggle!();
            await PanelFixture.SettleAsync();
            harness.Panel.UpdateLayout();
            var before = calls;

            harness.Panel.SetMaxPanelHeight(60);
            harness.Panel.UpdateLayout();
            await PanelFixture.SettleAsync();

            Assert.IsGreaterThan(before, calls);
        });
    }
}
