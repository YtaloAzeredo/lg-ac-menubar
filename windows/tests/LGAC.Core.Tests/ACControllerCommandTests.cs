using System.Text.Json.Nodes;
using LGAC.Core.Tests.Support;

namespace LGAC.Core.Tests;

[TestClass]
public class ACControllerCommandTests
{
    private static async Task<ACController> ReadyControllerAsync(FakeClient fake, InMemoryCredentialStore? store = null)
    {
        var controller = ControllerHarness.MakeController(fake, store);
        await controller.BootstrapAsync();
        return controller;
    }

    private static void AssertControls(FakeClient fake, params string[] expected)
    {
        Assert.HasCount(expected.Length, fake.Controls);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.IsTrue(
                JsonNode.DeepEquals(JsonNode.Parse(expected[index]), fake.Controls[index]),
                $"payload {index} diferente: {fake.Controls[index].ToJsonString()}");
        }
    }

    [TestMethod]
    public void SendIsOptimisticThenConfirms()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = await ReadyControllerAsync(fake);

            await controller.SendAsync(new ACCommand.WindStrength("HIGH"));

            Assert.AreEqual("HIGH", controller.State?.WindStrength);
            AssertControls(fake, """{"airFlow":{"windStrength":"HIGH"}}""");
            await controller.ConfirmTask!;
            Assert.AreEqual(2, fake.StateCalls);
        });
    }

    [TestMethod]
    public void RejectedCommandRollsBack()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = await ReadyControllerAsync(fake);
            fake.SetControlError(new ThinQException(ThinQErrorKind.CommandRejected, "2305", "Modo não permite"));

            await controller.SendAsync(new ACCommand.JobMode("HEAT"));

            Assert.AreEqual("COOL", controller.State?.JobMode);
            Assert.AreEqual("Comando recusado: Modo não permite", controller.StatusMessage);
        });
    }

    [TestMethod]
    public void UnsupportedCommandIsIgnored()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = new FakeClient(Fixture.Json("profile-basic"), Fixture.Json("state-basic"));
            var controller = await ReadyControllerAsync(fake);
            var before = controller.State;

            await controller.SendAsync(new ACCommand.DisplayLight(false));

            Assert.IsEmpty(fake.Controls);
            Assert.AreEqual(before, controller.State);
        });
    }

    [TestMethod]
    public void RapidClicksSendOneCommand()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = await ReadyControllerAsync(fake);

            controller.StepTargetTemp(1);
            controller.StepTargetTemp(1);
            controller.StepTargetTemp(1);
            Assert.AreEqual(26, controller.State?.TargetTemp);

            await controller.TempTask!;
            AssertControls(fake, """{"temperature":{"targetTemperature":26}}""");
        });
    }

    [TestMethod]
    public void ClickDuringDebounceRestartsTheWait()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var waits = new List<(TaskCompletionSource Gate, CancellationToken Token)>();
            var controller = ControllerHarness.MakeController(
                fake,
                delay: (duration, ct) =>
                {
                    if (duration != ACController.TempDebounce)
                    {
                        return ControllerHarness.ShortDelayAsync(ct);
                    }

                    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    waits.Add((gate, ct));
                    return gate.Task;
                });
            await controller.BootstrapAsync();

            controller.StepTargetTemp(1);
            var first = controller.TempTask!;
            controller.StepTargetTemp(1);
            var second = controller.TempTask!;

            Assert.HasCount(2, waits);
            Assert.IsTrue(waits[0].Token.IsCancellationRequested);
            waits[0].Gate.SetResult();
            await first;
            Assert.IsEmpty(fake.Controls);

            waits[1].Gate.SetResult();
            await second;
            AssertControls(fake, """{"temperature":{"targetTemperature":25}}""");
        });
    }

    [TestMethod]
    public void StepStopsAtTheLimit()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            fake.SetState(JsonNode.Parse("""
                {
                  "operation": {"airConOperationMode": "POWER_ON"},
                  "temperature": {"targetTemperature": 30, "currentTemperature": 31, "unit": "C"}
                }
                """));
            var controller = await ReadyControllerAsync(fake);

            controller.StepTargetTemp(1);

            Assert.AreEqual(30, controller.State?.TargetTemp);
            Assert.IsNull(controller.TempTask);
            Assert.IsEmpty(fake.Controls);
        });
    }

    [TestMethod]
    public void RejectedBurstRollsBackToValueBeforeFirstClick()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = await ReadyControllerAsync(fake);
            fake.SetControlError(new ThinQException(ThinQErrorKind.CommandRejected, "2304", ""));

            controller.StepTargetTemp(1);
            controller.StepTargetTemp(1);
            await controller.TempTask!;

            Assert.AreEqual(23, controller.State?.TargetTemp);
            Assert.AreEqual("Comando recusado pelo aparelho", controller.StatusMessage);
        });
    }

    [TestMethod]
    public void RefreshDuringDebounceKeepsPendingValue()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = await ReadyControllerAsync(fake);

            controller.StepTargetTemp(1);
            await controller.RefreshAsync(force: true);
            Assert.AreEqual(24, controller.State?.TargetTemp);

            await controller.TempTask!;
            AssertControls(fake, """{"temperature":{"targetTemperature":24}}""");
        });
    }

    [TestMethod]
    public void OffTimerCancelUpdatesState()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var controller = await ReadyControllerAsync(fake);
            Assert.AreEqual(80, controller.State?.OffTimerRemainingMinutes);

            await controller.SendAsync(new ACCommand.OffTimer(null));

            Assert.IsNull(controller.State?.OffTimerRemainingMinutes);
            AssertControls(fake, """{"timer":{"relativeStopTimer":"UNSET"}}""");
        });
    }

    [TestMethod]
    public void StaleRejectionAfterResetIsIgnored()
    {
        ControllerHarness.Run(async () =>
        {
            var fake = FakeClient.Full();
            var store = InMemoryCredentialStore.Configured();
            var controller = await ReadyControllerAsync(fake, store);
            fake.SetControlError(new ThinQException(ThinQErrorKind.InvalidToken));
            fake.HoldNextControl();

            var sending = controller.SendAsync(new ACCommand.JobMode("HEAT"));
            while (!fake.IsControlHeld)
            {
                await ControllerHarness.YieldAsync();
            }

            controller.ResetCredentials();
            store.Token = "novo-token";
            fake.ReleaseControl();
            await sending;

            Assert.AreEqual("novo-token", store.Token);
            Assert.IsNull(controller.State);
            Assert.IsNull(controller.StatusMessage);
            Assert.AreEqual(new ACPhase.NeedsSetup(null), controller.Phase);
            Assert.IsNull(controller.ConfirmTask);
        });
    }
}
