import Foundation
import Testing
@testable import LGACCore

private let ac1 = ThinQDevice(id: "ac-1", type: "DEVICE_AIR_CONDITIONER", alias: "Quarto", modelName: "M1")
private let ac2 = ThinQDevice(id: "ac-2", type: "DEVICE_AIR_CONDITIONER", alias: "Sala", modelName: "M2")
private let washer = ThinQDevice(id: "w-1", type: "DEVICE_WASHER", alias: "Lavadora", modelName: "W1")

@MainActor @Suite struct ACControllerSetupTests {
    @Test func withoutTokenNeedsSetup() async throws {
        let controller = makeController(fake: try .full(), store: InMemoryCredentialStore())
        await controller.bootstrap()
        #expect(controller.phase == .needsSetup(message: nil))
    }

    @Test func bootstrapLoadsCapabilitiesAndState() async throws {
        let controller = makeController(fake: try .full())
        await controller.bootstrap()
        #expect(controller.phase == .ready)
        #expect(controller.capabilities?.power == true)
        #expect(controller.state?.targetTemp == 23)
        #expect(controller.deviceAlias == "Ar do Quarto")
        #expect(controller.lastUpdated != nil)
    }

    @Test func connectTrimsTokenAndSavesAfterSuccess() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.success([ac1, washer]))
        let store = InMemoryCredentialStore()
        let log = CredentialsLog()
        let controller = makeController(fake: fake, store: store, log: log)

        await controller.connect(token: "  tok-123\n", country: "BR")

        #expect(log.all.first == ThinQCredentials(token: "tok-123", country: "BR", clientId: "lgac-test-client"))
        #expect(store.token == "tok-123")
        #expect(store.deviceId == "ac-1")
        #expect(store.deviceAlias == "Quarto")
        #expect(controller.phase == .ready)
    }

    @Test func blankTokenIsRejected() async throws {
        let store = InMemoryCredentialStore()
        let controller = makeController(fake: try .full(), store: store)
        await controller.connect(token: "   ", country: "BR")
        #expect(controller.phase == .needsSetup(message: "Cole o token"))
        #expect(store.token == nil)
    }

    @Test func invalidTokenIsNotSaved() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.failure(.invalidToken))
        let store = InMemoryCredentialStore()
        let controller = makeController(fake: fake, store: store)

        await controller.connect(token: "ruim", country: "BR")

        #expect(store.token == nil)
        #expect(controller.phase == .needsSetup(message: "Token inválido, gere um novo"))
    }

    @Test func accountWithoutAirConditioner() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.success([washer]))
        let controller = makeController(fake: fake, store: InMemoryCredentialStore())
        await controller.connect(token: "tok", country: "BR")
        #expect(controller.phase == .needsSetup(message: "Nenhum ar-condicionado encontrado nesta conta"))
    }

    @Test func twoAirConditionersAskWhichOne() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.success([ac1, ac2]))
        let store = InMemoryCredentialStore()
        let controller = makeController(fake: fake, store: store)

        await controller.connect(token: "tok", country: "BR")
        #expect(controller.phase == .choosingDevice([ac1, ac2]))
        #expect(store.token == nil)

        await controller.selectDevice(ac2)
        #expect(store.token == "tok")
        #expect(store.deviceId == "ac-2")
        #expect(controller.deviceAlias == "Sala")
        #expect(controller.phase == .ready)
    }

    @Test func keychainFailureIsReported() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.success([ac1]))
        let store = InMemoryCredentialStore()
        store.saveError = CredentialStoreError.keychain(-25300)
        let controller = makeController(fake: fake, store: store)

        await controller.connect(token: "tok", country: "BR")

        #expect(controller.phase == .needsSetup(message: "Não foi possível salvar o token no Keychain"))
    }

    @Test func resetCredentialsReturnsToSetup() async throws {
        let store = InMemoryCredentialStore.configured()
        let controller = makeController(fake: try .full(), store: store)
        await controller.bootstrap()

        controller.resetCredentials()

        #expect(controller.phase == .needsSetup(message: nil))
        #expect(store.token == nil)
        #expect(controller.capabilities == nil)
        #expect(controller.state == nil)
    }
}
