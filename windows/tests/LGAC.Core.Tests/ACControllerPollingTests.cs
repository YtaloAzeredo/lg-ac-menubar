using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACControllerPollingTests
{
    private static async Task<ACController> PollingControllerAsync(FakeClient fake, PollClock clock)
    {
        var controller = ControllerHarness.MakeController(fake, delay: clock.SleepAsync);
        await controller.BootstrapAsync();
        await clock.WaitForSleepsAsync(1);
        return controller;
    }

    [TestMethod]
    public void EachCycleReadsTheStateAgain()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var clock = new PollClock();
            var controller = await PollingControllerAsync(fake, clock);
            Assert.IsTrue(controller.State?.IsOn);

            fake.SetState(Fixture.Json("state-basic"));
            clock.Tick();
            await clock.WaitForSleepsAsync(2);

            Assert.IsFalse(controller.State?.IsOn);
            Assert.AreEqual(22.5, controller.State?.TargetTemp);
            CollectionAssert.AreEqual(
                new[] { TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(300) },
                clock.Intervals);
        });
    }

    [TestMethod]
    public void RateLimitMakesTheLoopWaitLonger()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var clock = new PollClock();
            var controller = await PollingControllerAsync(fake, clock);

            fake.FailState(new ThinQException(ThinQErrorKind.RateLimited));
            clock.Tick();
            await clock.WaitForSleepsAsync(2);
            clock.Tick();
            await clock.WaitForSleepsAsync(3);

            CollectionAssert.AreEqual(
                new[] { TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(600), TimeSpan.FromSeconds(1200) },
                clock.Intervals);
            Assert.AreEqual(new ThinQException(ThinQErrorKind.RateLimited).UserMessage, controller.StatusMessage);
        });
    }

    [TestMethod]
    public void NoNetworkAtStartupRecoversOnTheNextCycle()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.FailProfile(new ThinQException(ThinQErrorKind.Network));
            var clock = new PollClock();
            var controller = await PollingControllerAsync(fake, clock);
            Assert.AreEqual(new ACPhase.Loading(), controller.Phase);

            fake.SetProfile(Fixture.Json("profile-full"));
            clock.Tick();
            await clock.WaitForSleepsAsync(2);

            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
            Assert.AreEqual(23, controller.State?.TargetTemp);
        });
    }
}
