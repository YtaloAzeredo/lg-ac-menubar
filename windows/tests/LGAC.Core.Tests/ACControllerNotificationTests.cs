using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACControllerNotificationTests
{
    private static List<string?> Watch(ACController controller)
    {
        var names = new List<string?>();
        controller.PropertyChanged += (_, args) => names.Add(args.PropertyName);
        return names;
    }

    [TestMethod]
    public void BootstrapNotifiesPhaseCapabilitiesStateAndLastUpdated()
    {
        ControllerHarness.Run(async () =>
        {
            var controller = ControllerHarness.MakeController(FakeClient.Full());
            var names = Watch(controller);

            await controller.BootstrapAsync();

            CollectionAssert.Contains(names, nameof(ACController.Phase));
            CollectionAssert.Contains(names, nameof(ACController.Capabilities));
            CollectionAssert.Contains(names, nameof(ACController.State));
            CollectionAssert.Contains(names, nameof(ACController.LastUpdated));
            CollectionAssert.Contains(names, nameof(ACController.CanSwitchDevice));
        });
    }

    [TestMethod]
    public void ChooseAnotherDeviceNotifiesCanSwitchAndIsSwitching()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.SetDevices([TestDevices.Ac1, TestDevices.Ac2]);
            var controller = ControllerHarness.MakeController(fake);
            await controller.BootstrapAsync();
            var names = Watch(controller);

            await controller.ChooseAnotherDeviceAsync();

            CollectionAssert.Contains(names, nameof(ACController.CanSwitchDevice));
            CollectionAssert.Contains(names, nameof(ACController.IsSwitchingDevice));
        });
    }

    [TestMethod]
    public void RefreshWithSameStateDoesNotNotifyState()
    {
        ControllerHarness.Run(async () =>
        {
            var controller = ControllerHarness.MakeController(FakeClient.Full());
            await controller.BootstrapAsync();
            var names = Watch(controller);

            await controller.RefreshAsync(force: true);

            CollectionAssert.DoesNotContain(names, nameof(ACController.State));
            CollectionAssert.DoesNotContain(names, nameof(ACController.LastUpdated));
        });
    }

    [TestMethod]
    public void StopPollingAfterTickDoesNotReadStateAgain()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var clock = new PollClock();
            var controller = ControllerHarness.MakeController(fake, delay: clock.SleepAsync);
            await controller.BootstrapAsync();
            await clock.WaitForSleepsAsync(1);
            Assert.AreEqual(1, fake.StateCalls);

            clock.Tick();
            controller.StopPolling();
            for (var i = 0; i < 5; i++)
            {
                await ControllerHarness.YieldAsync();
            }

            Assert.AreEqual(1, fake.StateCalls);
        });
    }

    [TestMethod]
    public void CancellationDuringRefreshDoesNotChangeStatusMessage()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = ControllerHarness.MakeController(fake);
            await controller.BootstrapAsync();

            fake.FailState(new OperationCanceledException());
            await controller.RefreshAsync(force: true);

            Assert.IsNull(controller.StatusMessage);
            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
        });
    }

    [TestMethod]
    public void BootstrapWithVaultReadFailureNeedsSetup()
    {
        ControllerHarness.Run(async () =>
        {
            var store = InMemoryCredentialStore.Configured();
            store.ReadError = new InvalidOperationException("cofre indisponível");
            var controller = ControllerHarness.MakeController(FakeClient.Full(), store);

            await controller.BootstrapAsync();

            Assert.AreEqual(new ACPhase.NeedsSetup(null), controller.Phase);
        });
    }

    [TestMethod]
    public void ResetCredentialsSurvivesVaultDeleteFailure()
    {
        ControllerHarness.Run(async () =>
        {
            var store = InMemoryCredentialStore.Configured();
            var controller = ControllerHarness.MakeController(FakeClient.Full(), store);
            await controller.BootstrapAsync();
            store.DeleteError = new InvalidOperationException("cofre indisponível");

            controller.ResetCredentials();

            Assert.AreEqual(new ACPhase.NeedsSetup(null), controller.Phase);
            Assert.IsNull(controller.State);
        });
    }

    [TestMethod]
    public void FakeDelayObservesCancellationAfterYielding()
    {
        ControllerHarness.Run(async () =>
        {
            using var source = new CancellationTokenSource();
            var delay = ControllerHarness.TestDelayAsync(ACController.TempDebounce, source.Token);
            source.Cancel();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => delay);
        });
    }

    [TestMethod]
    public void HarnessFailsWhenNotificationComesFromAnotherThread()
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => ControllerHarness.Run(async () =>
        {
            var controller = ControllerHarness.MakeController(FakeClient.Full());
            await Task.Run(() => controller.StatusMessage = "fora da thread");
        }));
        StringAssert.Contains(error.Message, "thread");
    }
}
