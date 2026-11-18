using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACControllerRefreshTests
{
    [TestMethod]
    public void NetworkErrorKeepsLastState()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = ControllerHarness.MakeController(fake);
            await controller.BootstrapAsync();

            fake.FailState(new ThinQException(ThinQErrorKind.Network));
            await controller.RefreshAsync(force: true);

            Assert.AreEqual(23, controller.State?.TargetTemp);
            Assert.AreEqual("Sem conexão", controller.StatusMessage);
            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
        });
    }

    [TestMethod]
    public void NoNetworkAtStartupStaysLoadingThenRecovers()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.FailProfile(new ThinQException(ThinQErrorKind.Network));
            var controller = ControllerHarness.MakeController(fake);

            await controller.BootstrapAsync();
            Assert.AreEqual(new ACPhase.Loading(), controller.Phase);
            Assert.AreEqual("Sem conexão", controller.StatusMessage);

            fake.SetProfile(Fixture.Json("profile-full"));
            await controller.RefreshAsync(force: true);
            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
            Assert.IsNull(controller.StatusMessage);
        });
    }

    [TestMethod]
    public void InvalidTokenDuringRefreshReturnsToSetupKeepingTheToken()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var store = InMemoryCredentialStore.Configured();
            var controller = ControllerHarness.MakeController(fake, store);
            await controller.BootstrapAsync();

            fake.FailState(new ThinQException(ThinQErrorKind.InvalidToken));
            await controller.RefreshAsync(force: true);

            Assert.AreEqual(new ACPhase.NeedsSetup("Token inválido, gere um novo"), controller.Phase);
            Assert.AreEqual("tok", store.Token);
        });
    }

    [TestMethod]
    public void TransientInvalidTokenRecoversOnNextLaunch()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var store = InMemoryCredentialStore.Configured();
            var controller = ControllerHarness.MakeController(fake, store);
            await controller.BootstrapAsync();
            fake.FailState(new ThinQException(ThinQErrorKind.InvalidToken));
            await controller.RefreshAsync(force: true);

            fake.SetState(Fixture.Json("state-full"));
            var relaunched = ControllerHarness.MakeController(fake, store);
            await relaunched.BootstrapAsync();

            Assert.AreEqual(new ACPhase.Ready(), relaunched.Phase);
            Assert.AreEqual(23, relaunched.State?.TargetTemp);
        });
    }

    [TestMethod]
    public void InvalidTokenClearsStateAndOfflineFlag()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = ControllerHarness.MakeController(fake);
            await controller.BootstrapAsync();
            fake.FailState(new ThinQException(ThinQErrorKind.DeviceOffline));
            await controller.RefreshAsync(force: true);
            Assert.IsTrue(controller.IsDeviceOffline);

            fake.FailState(new ThinQException(ThinQErrorKind.InvalidToken));
            await controller.RefreshAsync(force: true);

            Assert.IsNull(controller.State);
            Assert.IsNull(controller.Capabilities);
            Assert.IsFalse(controller.IsDeviceOffline);
        });
    }

    [TestMethod]
    public void DeviceOfflineFlagClearsOnSuccess()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = ControllerHarness.MakeController(fake);
            await controller.BootstrapAsync();

            fake.FailState(new ThinQException(ThinQErrorKind.DeviceOffline));
            await controller.RefreshAsync(force: true);
            Assert.IsTrue(controller.IsDeviceOffline);
            Assert.AreEqual("Ar-condicionado offline", controller.StatusMessage);

            fake.SetState(Fixture.Json("state-full"));
            await controller.RefreshAsync(force: true);
            Assert.IsFalse(controller.IsDeviceOffline);
        });
    }

    [TestMethod]
    public void RateLimitBacksOffUpToThirtyMinutesAndResets()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = ControllerHarness.MakeController(fake);
            await controller.BootstrapAsync();
            fake.FailState(new ThinQException(ThinQErrorKind.RateLimited));

            var intervals = new List<TimeSpan>();
            for (var i = 0; i < 4; i++)
            {
                await controller.RefreshAsync(force: true);
                intervals.Add(controller.PollInterval);
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    TimeSpan.FromSeconds(600),
                    TimeSpan.FromSeconds(1200),
                    TimeSpan.FromSeconds(1800),
                    TimeSpan.FromSeconds(1800),
                },
                intervals);

            fake.SetState(Fixture.Json("state-full"));
            await controller.RefreshAsync(force: true);
            Assert.AreEqual(TimeSpan.FromSeconds(300), controller.PollInterval);
        });
    }

    [TestMethod]
    public void RefreshWithinFiveSecondsIsSkipped()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var clock = new TestNow();
            var controller = ControllerHarness.MakeController(fake, clock: clock);
            await controller.BootstrapAsync();
            Assert.AreEqual(1, fake.StateCalls);

            clock.Date += TimeSpan.FromSeconds(3);
            await controller.RefreshAsync();
            Assert.AreEqual(1, fake.StateCalls);

            clock.Date += TimeSpan.FromSeconds(3);
            await controller.RefreshAsync();
            Assert.AreEqual(2, fake.StateCalls);
        });
    }

    [TestMethod]
    public void StaleRefreshAfterResetDoesNotRevive()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = ControllerHarness.MakeController(fake);
            await controller.BootstrapAsync();

            fake.HoldNextState();
            var inFlight = controller.RefreshAsync(force: true);
            while (!fake.IsStateHeld)
            {
                await ControllerHarness.YieldAsync();
            }

            controller.ResetCredentials();
            fake.ReleaseState();
            await inFlight;

            Assert.AreEqual(new ACPhase.NeedsSetup(null), controller.Phase);
            Assert.IsNull(controller.State);
            Assert.IsNull(controller.Capabilities);
        });
    }
}
