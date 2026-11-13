import Foundation
import Testing
@testable import LGACCore

@MainActor @Suite struct ACControllerCommandTests {
    private func readyController(_ fake: FakeClient) async -> ACController {
        let controller = makeController(fake: fake)
        await controller.bootstrap()
        return controller
    }

    @Test func sendIsOptimisticThenConfirms() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)

        await controller.send(.windStrength("HIGH"))

        #expect(controller.state?.windStrength == "HIGH")
        #expect(await fake.controls == [["airFlow": ["windStrength": "HIGH"]]])
        await controller.confirmTask?.value
        #expect(await fake.stateCalls == 2)
    }

    @Test func rejectedCommandRollsBack() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)
        await fake.setControlError(.commandRejected(code: "2305", message: "Modo não permite"))

        await controller.send(.jobMode("HEAT"))

        #expect(controller.state?.jobMode == "COOL")
        #expect(controller.statusMessage == "Comando recusado: Modo não permite")
    }

    @Test func unsupportedCommandIsIgnored() async throws {
        let fake = FakeClient(profile: try Fixture.json("profile-basic"), state: try Fixture.json("state-basic"))
        let controller = await readyController(fake)
        let before = controller.state

        await controller.send(.displayLight(false))

        #expect(await fake.controls.isEmpty)
        #expect(controller.state == before)
    }

    @Test func rapidClicksSendOneCommand() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)

        controller.stepTargetTemp(by: 1)
        controller.stepTargetTemp(by: 1)
        controller.stepTargetTemp(by: 1)
        #expect(controller.state?.targetTemp == 26)

        await controller.tempTask?.value
        #expect(await fake.controls == [["temperature": ["targetTemperature": 26]]])
    }

    @Test func stepStopsAtTheLimit() async throws {
        let fake = try FakeClient.full()
        await fake.setState(.success([
            "operation": ["airConOperationMode": "POWER_ON"],
            "temperature": ["targetTemperature": 30, "currentTemperature": 31, "unit": "C"],
        ]))
        let controller = await readyController(fake)

        controller.stepTargetTemp(by: 1)

        #expect(controller.state?.targetTemp == 30)
        #expect(controller.tempTask == nil)
        #expect(await fake.controls.isEmpty)
    }

    @Test func rejectedBurstRollsBackToValueBeforeFirstClick() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)
        await fake.setControlError(.commandRejected(code: "2304", message: ""))

        controller.stepTargetTemp(by: 1)
        controller.stepTargetTemp(by: 1)
        await controller.tempTask?.value

        #expect(controller.state?.targetTemp == 23)
        #expect(controller.statusMessage == "Comando recusado pelo aparelho")
    }

    @Test func refreshDuringDebounceKeepsPendingValue() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)

        controller.stepTargetTemp(by: 1)
        await controller.refresh(force: true)
        #expect(controller.state?.targetTemp == 24)

        await controller.tempTask?.value
        #expect(await fake.controls == [["temperature": ["targetTemperature": 24]]])
    }

    @Test func offTimerCancelUpdatesState() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)
        #expect(controller.state?.offTimerRemainingMinutes == 80)

        await controller.send(.offTimer(minutes: nil))

        #expect(controller.state?.offTimerRemainingMinutes == nil)
        #expect(await fake.controls == [["timer": ["relativeStopTimer": "UNSET"]]])
    }

    @Test func staleRejectionAfterResetIsIgnored() async throws {
        let fake = try FakeClient.full()
        let store = InMemoryCredentialStore.configured()
        let controller = makeController(fake: fake, store: store)
        await controller.bootstrap()
        await fake.setControlError(.invalidToken)
        await fake.holdNextControl()

        let sending = Task { await controller.send(.jobMode("HEAT")) }
        for _ in 0..<10_000 where !(await fake.isControlHeld) { await Task.yield() }
        #expect(await fake.isControlHeld)

        controller.resetCredentials()
        store.token = "novo-token"
        await fake.releaseControl()
        await sending.value

        #expect(store.token == "novo-token")
        #expect(controller.state == nil)
        #expect(controller.statusMessage == nil)
        #expect(controller.phase == .needsSetup(message: nil))
        #expect(controller.confirmTask == nil)
    }
}
