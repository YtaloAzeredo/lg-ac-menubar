using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACControllerDeviceSwitchTests
{
    private static async Task<ACController> ReadyControllerAsync(
        FakeClient fake, InMemoryCredentialStore? store = null, CredentialsLog? log = null)
    {
        fake.SetDevices([TestDevices.Ac1, TestDevices.Washer, TestDevices.Ac2]);
        var controller = ControllerHarness.MakeController(fake, store ?? InMemoryCredentialStore.Configured(), log: log);
        await controller.BootstrapAsync();
        return controller;
    }

    private static ACPhase ChoosingBoth => new ACPhase.ChoosingDevice([TestDevices.Ac1, TestDevices.Ac2]);

    [TestMethod]
    public void ListsAirConditionersReusingTheCurrentConnection()
    {
        ControllerHarness.Run(async () =>
        {
            var store = InMemoryCredentialStore.Configured();
            var log = new CredentialsLog();
            var controller = await ReadyControllerAsync(FakeClient.Full(), store, log);
            var clientsBefore = log.All.Count;

            await controller.ChooseAnotherDeviceAsync();

            Assert.AreEqual(ChoosingBoth, controller.Phase);
            Assert.IsTrue(controller.IsSwitchingDevice);
            Assert.IsFalse(controller.CanSwitchDevice);
            Assert.HasCount(clientsBefore, log.All);
            Assert.AreEqual("tok", store.Token);
            Assert.AreEqual("ac-1", store.DeviceId);
        });
    }

    [TestMethod]
    public void SwitchingNeverTouchesTheVault()
    {
        ControllerHarness.Run(async () =>
        {
            var store = InMemoryCredentialStore.Configured();
            var controller = await ReadyControllerAsync(FakeClient.Full(), store);
            var accessesBefore = store.VaultAccesses;

            await controller.ChooseAnotherDeviceAsync();
            await controller.CancelDeviceChoiceAsync();
            await controller.ChooseAnotherDeviceAsync();
            await controller.SelectDeviceAsync(TestDevices.Ac2);

            Assert.AreEqual(accessesBefore, store.VaultAccesses);
            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
            Assert.AreEqual("ac-2", store.DeviceId);
        });
    }

    [TestMethod]
    public void CanSwitchOnlyWithAnOpenConnection()
    {
        ControllerHarness.Run(async () =>
        {
            var fresh = ControllerHarness.MakeController(FakeClient.Full(), InMemoryCredentialStore.Configured());
            Assert.IsFalse(fresh.CanSwitchDevice);

            var ready = await ReadyControllerAsync(FakeClient.Full());
            Assert.IsTrue(ready.CanSwitchDevice);
        });
    }

    [TestMethod]
    public void ChoosingAnotherLoadsTheNewDevice()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var store = InMemoryCredentialStore.Configured();
            var controller = await ReadyControllerAsync(fake, store);
            await controller.ChooseAnotherDeviceAsync();
            var basic = Fixture.Json("profile-basic");
            fake.SetProfile(basic);
            fake.SetState(Fixture.Json("state-basic"));

            await controller.SelectDeviceAsync(TestDevices.Ac2);

            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
            Assert.IsFalse(controller.IsSwitchingDevice);
            Assert.AreEqual("tok", store.Token);
            Assert.AreEqual("ac-2", store.DeviceId);
            Assert.AreEqual("Sala", store.DeviceAlias);
            Assert.AreEqual("Sala", controller.DeviceAlias);
            Assert.AreEqual(new ACCapabilities(basic), controller.Capabilities);
            Assert.AreEqual(22.5, controller.State?.TargetTemp);
        });
    }

    [TestMethod]
    public void CancelKeepsTheCurrentDevice()
    {
        ControllerHarness.Run(async () =>
        {
            var store = InMemoryCredentialStore.Configured();
            var controller = await ReadyControllerAsync(FakeClient.Full(), store);
            await controller.ChooseAnotherDeviceAsync();

            await controller.CancelDeviceChoiceAsync();

            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
            Assert.IsFalse(controller.IsSwitchingDevice);
            Assert.AreEqual("ac-1", store.DeviceId);
            Assert.AreEqual("Ar do Quarto", controller.DeviceAlias);
            Assert.AreEqual(23, controller.State?.TargetTemp);
        });
    }

    [TestMethod]
    public void RefreshWhileChoosingKeepsTheList()
    {
        ControllerHarness.Run(async () =>
        {
            var controller = await ReadyControllerAsync(FakeClient.Full());
            await controller.ChooseAnotherDeviceAsync();

            await controller.RefreshAsync(force: true);

            Assert.AreEqual(ChoosingBoth, controller.Phase);
        });
    }

    [TestMethod]
    public void RefreshInFlightDoesNotLeaveTheList()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = await ReadyControllerAsync(fake);

            fake.HoldNextState();
            var inFlight = controller.RefreshAsync(force: true);
            while (!fake.IsStateHeld)
            {
                await ControllerHarness.YieldAsync();
            }

            await controller.ChooseAnotherDeviceAsync();
            fake.ReleaseState();
            await inFlight;

            Assert.AreEqual(ChoosingBoth, controller.Phase);
        });
    }

    [TestMethod]
    public void NetworkErrorKeepsTheCurrentDevice()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var store = InMemoryCredentialStore.Configured();
            var controller = await ReadyControllerAsync(fake, store);
            fake.FailDevices(new ThinQException(ThinQErrorKind.Network));

            await controller.ChooseAnotherDeviceAsync();

            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
            Assert.IsFalse(controller.IsSwitchingDevice);
            Assert.AreEqual(new ThinQException(ThinQErrorKind.Network).UserMessage, controller.StatusMessage);
            Assert.AreEqual("ac-1", store.DeviceId);
        });
    }

    [TestMethod]
    public void AccountWithoutAirConditionerKeepsTheCurrentDevice()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = await ReadyControllerAsync(fake);
            fake.SetDevices([TestDevices.Washer]);

            await controller.ChooseAnotherDeviceAsync();

            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
            Assert.AreEqual("Nenhum ar-condicionado encontrado nesta conta", controller.StatusMessage);
        });
    }

    [TestMethod]
    public void InvalidTokenGoesToSetupKeepingTheToken()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var store = InMemoryCredentialStore.Configured();
            var controller = await ReadyControllerAsync(fake, store);
            fake.FailDevices(new ThinQException(ThinQErrorKind.InvalidToken));

            await controller.ChooseAnotherDeviceAsync();

            Assert.AreEqual(new ACPhase.NeedsSetup("Token inválido, gere um novo"), controller.Phase);
            Assert.AreEqual("tok", store.Token);
        });
    }

    [TestMethod]
    public void WithoutConnectionGoesToSetup()
    {
        ControllerHarness.Run(async () =>
        {
            var controller = ControllerHarness.MakeController(FakeClient.Full(), InMemoryCredentialStore.Configured());

            await controller.ChooseAnotherDeviceAsync();

            Assert.AreEqual(new ACPhase.NeedsSetup(null), controller.Phase);
        });
    }

    [TestMethod]
    public void ChangeTokenWhileChoosingEndsTheSwitch()
    {
        ControllerHarness.Run(async () =>
        {
            var store = InMemoryCredentialStore.Configured();
            var controller = await ReadyControllerAsync(FakeClient.Full(), store);
            await controller.ChooseAnotherDeviceAsync();

            controller.ResetCredentials();
            await controller.CancelDeviceChoiceAsync();

            Assert.AreEqual(new ACPhase.NeedsSetup(null), controller.Phase);
            Assert.IsFalse(controller.IsSwitchingDevice);
            Assert.IsNull(store.Token);
        });
    }
}
