import Foundation
@testable import LGACCore

/// Cliente falso para os testes do controlador: respostas configuráveis e registro dos comandos.
actor FakeClient: ThinQClientProtocol {
    private var devicesResult: Result<[ThinQDevice], ThinQError>
    private var profileResult: Result<JSONValue, ThinQError>
    private var stateResult: Result<JSONValue, ThinQError>
    private var controlError: ThinQError?
    private(set) var controls: [JSONValue] = []
    private(set) var stateCalls = 0
    private var holdState = false
    private var stateGate: CheckedContinuation<Void, Never>?
    private(set) var isStateHeld = false

    init(profile: JSONValue, state: JSONValue, devices: [ThinQDevice] = []) {
        devicesResult = .success(devices)
        profileResult = .success(profile)
        stateResult = .success(state)
    }

    /// Aparelho do fixture "full" ligado em COOL, 23 °C.
    static func full() throws -> FakeClient {
        FakeClient(profile: try Fixture.json("profile-full"), state: try Fixture.json("state-full"))
    }

    func devices() async throws -> [ThinQDevice] { try devicesResult.get() }
    func profile(deviceId: String) async throws -> JSONValue { try profileResult.get() }

    func state(deviceId: String) async throws -> JSONValue {
        stateCalls += 1
        if holdState {
            isStateHeld = true
            await withCheckedContinuation { stateGate = $0 }
        }
        return try stateResult.get()
    }

    func control(deviceId: String, payload: JSONValue) async throws {
        controls.append(payload)
        if let controlError { throw controlError }
    }

    /// Opt-in: a próxima chamada de `state` fica suspensa até `releaseState()`.
    func holdNextState() { holdState = true }
    func releaseState() {
        holdState = false
        isStateHeld = false
        stateGate?.resume()
        stateGate = nil
    }

    func setDevices(_ result: Result<[ThinQDevice], ThinQError>) { devicesResult = result }
    func setProfile(_ result: Result<JSONValue, ThinQError>) { profileResult = result }
    func setState(_ result: Result<JSONValue, ThinQError>) { stateResult = result }
    func setControlError(_ error: ThinQError?) { controlError = error }
}

/// Espera falsa: atrasos curtos (debounce, confirmação) passam na hora;
/// o intervalo de polling fica suspenso até a tarefa ser cancelada.
let testSleep: @Sendable (Duration) async throws -> Void = { duration in
    try Task.checkCancellation()
    if duration >= ACController.basePollInterval {
        try await Task.sleep(for: .seconds(3600))
    }
    await Task.yield()
}

/// Relógio controlável para testar a regra dos 5 s.
final class TestNow: @unchecked Sendable {
    var date = Date(timeIntervalSince1970: 1_000_000)
}

/// Guarda as credenciais que o controlador usou para criar clientes.
final class CredentialsLog: @unchecked Sendable {
    var all: [ThinQCredentials] = []
}

@MainActor
func makeController(
    fake: FakeClient,
    store: InMemoryCredentialStore = .configured(),
    clock: TestNow = TestNow(),
    log: CredentialsLog = CredentialsLog()
) -> ACController {
    ACController(
        store: store,
        makeClient: { credentials in
            log.all.append(credentials)
            return fake
        },
        sleep: testSleep,
        now: { clock.date })
}
