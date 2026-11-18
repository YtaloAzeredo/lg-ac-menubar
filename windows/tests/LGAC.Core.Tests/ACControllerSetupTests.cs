using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACControllerSetupTests
{
    [TestMethod]
    public void WithoutTokenNeedsSetup()
    {
        ControllerHarness.Run(async () =>
        {
            var controller = ControllerHarness.MakeController(FakeClient.Full(), new InMemoryCredentialStore());
            await controller.BootstrapAsync();
            Assert.AreEqual(new ACPhase.NeedsSetup(null), controller.Phase);
        });
    }

    [TestMethod]
    public void BootstrapLoadsCapabilitiesAndState()
    {
        ControllerHarness.Run(async () =>
        {
            var controller = ControllerHarness.MakeController(FakeClient.Full());
            await controller.BootstrapAsync();
            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
            Assert.IsTrue(controller.Capabilities?.Power);
            Assert.AreEqual(23, controller.State?.TargetTemp);
            Assert.AreEqual("Ar do Quarto", controller.DeviceAlias);
            Assert.IsNotNull(controller.LastUpdated);
        });
    }

    [TestMethod]
    public void ConnectTrimsTokenAndSavesAfterSuccess()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.SetDevices([TestDevices.Ac1, TestDevices.Washer]);
            var store = new InMemoryCredentialStore();
            var log = new CredentialsLog();
            var controller = ControllerHarness.MakeController(fake, store, log: log);

            await controller.ConnectAsync("  tok-123\n", "BR");

            Assert.AreEqual(new ThinQCredentials("tok-123", "BR", "lgac-test-client"), log.All.First());
            Assert.AreEqual("tok-123", store.Token);
            Assert.AreEqual("ac-1", store.DeviceId);
            Assert.AreEqual("Quarto", store.DeviceAlias);
            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
        });
    }

    [TestMethod]
    public void BlankTokenIsRejected()
    {
        ControllerHarness.Run(async () =>
        {
            var store = new InMemoryCredentialStore();
            var controller = ControllerHarness.MakeController(FakeClient.Full(), store);
            await controller.ConnectAsync("   ", "BR");
            Assert.AreEqual(new ACPhase.NeedsSetup("Cole o token"), controller.Phase);
            Assert.IsNull(store.Token);
        });
    }

    [TestMethod]
    public void InvalidTokenIsNotSaved()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.FailDevices(new ThinQException(ThinQErrorKind.InvalidToken));
            var store = new InMemoryCredentialStore();
            var controller = ControllerHarness.MakeController(fake, store);

            await controller.ConnectAsync("ruim", "BR");

            Assert.IsNull(store.Token);
            Assert.AreEqual(new ACPhase.NeedsSetup("Token inválido, gere um novo"), controller.Phase);
        });
    }

    [TestMethod]
    public void AccountWithoutAirConditioner()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.SetDevices([TestDevices.Washer]);
            var controller = ControllerHarness.MakeController(fake, new InMemoryCredentialStore());
            await controller.ConnectAsync("tok", "BR");
            Assert.AreEqual(
                new ACPhase.NeedsSetup("Nenhum ar-condicionado encontrado nesta conta"),
                controller.Phase);
        });
    }

    [TestMethod]
    public void TwoAirConditionersAskWhichOne()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.SetDevices([TestDevices.Ac1, TestDevices.Ac2]);
            var store = new InMemoryCredentialStore();
            var controller = ControllerHarness.MakeController(fake, store);

            await controller.ConnectAsync("tok", "BR");
            Assert.AreEqual(new ACPhase.ChoosingDevice([TestDevices.Ac1, TestDevices.Ac2]), controller.Phase);
            Assert.IsNull(store.Token);

            await controller.SelectDeviceAsync(TestDevices.Ac2);
            Assert.AreEqual("tok", store.Token);
            Assert.AreEqual("ac-2", store.DeviceId);
            Assert.AreEqual("Sala", controller.DeviceAlias);
            Assert.AreEqual(new ACPhase.Ready(), controller.Phase);
        });
    }

    [TestMethod]
    public void VaultFailureIsReported()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.SetDevices([TestDevices.Ac1]);
            var store = new InMemoryCredentialStore { SaveError = new InvalidOperationException("cofre indisponível") };
            var controller = ControllerHarness.MakeController(fake, store);

            await controller.ConnectAsync("tok", "BR");

            Assert.AreEqual(new ACPhase.NeedsSetup("Não foi possível salvar o token"), controller.Phase);
        });
    }

    [TestMethod]
    public void ResetCredentialsReturnsToSetup()
    {
        ControllerHarness.Run(async () =>
        {
            var store = InMemoryCredentialStore.Configured();
            var controller = ControllerHarness.MakeController(FakeClient.Full(), store);
            await controller.BootstrapAsync();

            controller.ResetCredentials();

            Assert.AreEqual(new ACPhase.NeedsSetup(null), controller.Phase);
            Assert.IsNull(store.Token);
            Assert.IsNull(controller.Capabilities);
            Assert.IsNull(controller.State);
        });
    }
}
