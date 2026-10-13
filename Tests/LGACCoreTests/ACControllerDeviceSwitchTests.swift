import Foundation
import Testing
@testable import LGACCore

private let ac1 = ThinQDevice(id: "ac-1", type: "DEVICE_AIR_CONDITIONER", alias: "Quarto", modelName: "M1")
private let ac2 = ThinQDevice(id: "ac-2", type: "DEVICE_AIR_CONDITIONER", alias: "Sala", modelName: "M2")
private let washer = ThinQDevice(id: "w-1", type: "DEVICE_WASHER", alias: "Lavadora", modelName: "W1")

/// "Trocar aparelho…": escolher outro ar-condicionado usando o token já salvo.
@MainActor @Suite struct ACControllerDeviceSwitchTests {
    private func readyController(
        _ fake: FakeClient, store: InMemoryCredentialStore = .configured(), log: CredentialsLog = CredentialsLog()
    ) async -> ACController {
        await fake.setDevices(.success([ac1, washer, ac2]))
        let controller = makeController(fake: fake, store: store, log: log)
        await controller.bootstrap()
        return controller
    }

    @Test func listsAirConditionersReusingTheCurrentConnection() async throws {
        let store = InMemoryCredentialStore.configured()
        let log = CredentialsLog()
        let controller = await readyController(try .full(), store: store, log: log)
        let clientsBefore = log.all.count

        await controller.chooseAnotherDevice()

        #expect(controller.phase == .choosingDevice([ac1, ac2]))
        #expect(controller.isSwitchingDevice)
        #expect(!controller.canSwitchDevice)
        #expect(log.all.count == clientsBefore)
        #expect(store.token == "tok")
        #expect(store.deviceId == "ac-1")
    }

    @Test func switchingNeverTouchesTheKeychain() async throws {
        let store = InMemoryCredentialStore.configured()
        let controller = await readyController(try .full(), store: store)
        let accessesBefore = store.keychainAccesses

        await controller.chooseAnotherDevice()
        await controller.cancelDeviceChoice()
        await controller.chooseAnotherDevice()
        await controller.selectDevice(ac2)

        #expect(store.keychainAccesses == accessesBefore)
        #expect(controller.phase == .ready)
        #expect(store.deviceId == "ac-2")
    }

    @Test func canSwitchOnlyWithAnOpenConnection() async throws {
        let fresh = makeController(fake: try .full(), store: .configured())
        #expect(!fresh.canSwitchDevice)

        let ready = await readyController(try .full())
        #expect(ready.canSwitchDevice)
    }

    @Test func choosingAnotherLoadsTheNewDevice() async throws {
        let fake = try FakeClient.full()
        let store = InMemoryCredentialStore.configured()
        let controller = await readyController(fake, store: store)
        await controller.chooseAnotherDevice()
        let basic = try Fixture.json("profile-basic")
        await fake.setProfile(.success(basic))
        await fake.setState(.success(try Fixture.json("state-basic")))

        await controller.selectDevice(ac2)

        #expect(controller.phase == .ready)
        #expect(!controller.isSwitchingDevice)
        #expect(store.token == "tok")
        #expect(store.deviceId == "ac-2")
        #expect(store.deviceAlias == "Sala")
        #expect(controller.deviceAlias == "Sala")
        #expect(controller.capabilities == ACCapabilities(profile: basic))
        #expect(controller.state?.targetTemp == 22.5)
    }

    @Test func cancelKeepsTheCurrentDevice() async throws {
        let store = InMemoryCredentialStore.configured()
        let controller = await readyController(try .full(), store: store)
        await controller.chooseAnotherDevice()

        await controller.cancelDeviceChoice()

        #expect(controller.phase == .ready)
        #expect(!controller.isSwitchingDevice)
        #expect(store.deviceId == "ac-1")
        #expect(controller.deviceAlias == "Ar do Quarto")
        #expect(controller.state?.targetTemp == 23)
    }

    @Test func refreshWhileChoosingKeepsTheList() async throws {
        let controller = await readyController(try .full())
        await controller.chooseAnotherDevice()

        await controller.refresh(force: true)

        #expect(controller.phase == .choosingDevice([ac1, ac2]))
    }

    @Test func refreshInFlightDoesNotLeaveTheList() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)

        await fake.holdNextState()
        let inFlight = Task { @MainActor in await controller.refresh(force: true) }
        while !(await fake.isStateHeld) { await Task.yield() }

        await controller.chooseAnotherDevice()
        await fake.releaseState()
        await inFlight.value

        #expect(controller.phase == .choosingDevice([ac1, ac2]))
    }

    @Test func networkErrorKeepsTheCurrentDevice() async throws {
        let fake = try FakeClient.full()
        let store = InMemoryCredentialStore.configured()
        let controller = await readyController(fake, store: store)
        await fake.setDevices(.failure(.network(.notConnectedToInternet)))

        await controller.chooseAnotherDevice()

        #expect(controller.phase == .ready)
        #expect(!controller.isSwitchingDevice)
        #expect(controller.statusMessage == ThinQError.network(.notConnectedToInternet).userMessage)
        #expect(store.deviceId == "ac-1")
    }

    @Test func accountWithoutAirConditionerKeepsTheCurrentDevice() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)
        await fake.setDevices(.success([washer]))

        await controller.chooseAnotherDevice()

        #expect(controller.phase == .ready)
        #expect(controller.statusMessage == "Nenhum ar-condicionado encontrado nesta conta")
    }

    @Test func invalidTokenGoesToSetup() async throws {
        let fake = try FakeClient.full()
        let store = InMemoryCredentialStore.configured()
        let controller = await readyController(fake, store: store)
        await fake.setDevices(.failure(.invalidToken))

        await controller.chooseAnotherDevice()

        #expect(controller.phase == .needsSetup(message: "Token inválido, gere um novo"))
        #expect(store.token == nil)
    }

    @Test func withoutConnectionGoesToSetup() async throws {
        let controller = makeController(fake: try .full(), store: .configured())

        await controller.chooseAnotherDevice()

        #expect(controller.phase == .needsSetup(message: nil))
    }

    @Test func changeTokenWhileChoosingEndsTheSwitch() async throws {
        let store = InMemoryCredentialStore.configured()
        let controller = await readyController(try .full(), store: store)
        await controller.chooseAnotherDevice()

        controller.resetCredentials()
        await controller.cancelDeviceChoice()

        #expect(controller.phase == .needsSetup(message: nil))
        #expect(!controller.isSwitchingDevice)
        #expect(store.token == nil)
    }
}
