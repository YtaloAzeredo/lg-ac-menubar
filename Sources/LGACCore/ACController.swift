import Foundation
import Observation

/// O "cérebro" observado pela interface: configuração, estado, polling e tratamento de erros.
/// Os comandos (otimistas, com debounce) ficam em `ACController+Commands.swift`.
@MainActor @Observable
public final class ACController {
    public enum Phase: Equatable {
        case needsSetup(message: String?)
        case loading
        case choosingDevice([ThinQDevice])
        case ready
    }

    nonisolated public static let basePollInterval: Duration = .seconds(300)
    nonisolated public static let maxPollInterval: Duration = .seconds(1800)
    nonisolated static let freshness: TimeInterval = 5
    nonisolated static let confirmDelay: Duration = .milliseconds(1500)
    nonisolated static let tempDebounce: Duration = .milliseconds(600)

    public internal(set) var phase: Phase = .loading
    public internal(set) var capabilities: ACCapabilities?
    public internal(set) var state: ACState?
    public internal(set) var deviceAlias = ""
    public internal(set) var lastUpdated: Date?
    public internal(set) var statusMessage: String?
    public internal(set) var isDeviceOffline = false
    public internal(set) var pollInterval: Duration = ACController.basePollInterval

    @ObservationIgnored let store: any CredentialStoring
    @ObservationIgnored let makeClient: @Sendable (ThinQCredentials) throws -> any ThinQClientProtocol
    @ObservationIgnored let sleep: @Sendable (Duration) async throws -> Void
    @ObservationIgnored let now: @Sendable () -> Date
    @ObservationIgnored var client: (any ThinQClientProtocol)?
    @ObservationIgnored var pendingSetup: (client: any ThinQClientProtocol, token: String)?
    @ObservationIgnored var pollTask: Task<Void, Never>?
    // Usados pelos comandos (ACController+Commands.swift).
    @ObservationIgnored var confirmTask: Task<Void, Never>?
    @ObservationIgnored var tempTask: Task<Void, Never>?
    @ObservationIgnored var pendingTargetTemp: Double?
    @ObservationIgnored var tempBaseline: ACState?

    public init(
        store: any CredentialStoring,
        makeClient: @escaping @Sendable (ThinQCredentials) throws -> any ThinQClientProtocol = {
            try ThinQClient(credentials: $0)
        },
        sleep: @escaping @Sendable (Duration) async throws -> Void = { try await Task.sleep(for: $0) },
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.store = store
        self.makeClient = makeClient
        self.sleep = sleep
        self.now = now
    }

    // MARK: - Configuração

    /// Chamado uma vez ao abrir o app.
    public func bootstrap() async {
        guard let token = store.token, store.deviceId != nil else {
            phase = .needsSetup(message: nil)
            return
        }
        do {
            client = try makeClient(ThinQCredentials(token: token, country: store.country, clientId: store.clientId))
        } catch {
            phase = .needsSetup(message: Self.message(for: error))
            return
        }
        deviceAlias = store.deviceAlias ?? ""
        await load()
    }

    /// Valida o token listando os aparelhos. O token só é salvo depois de escolhido o aparelho.
    public func connect(token rawToken: String, country: String) async {
        let token = rawToken.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !token.isEmpty else {
            phase = .needsSetup(message: "Cole o token")
            return
        }
        phase = .loading
        store.country = country
        do {
            let client = try makeClient(ThinQCredentials(token: token, country: country, clientId: store.clientId))
            let airConditioners = try await client.devices().filter(\.isAirConditioner)
            pendingSetup = (client, token)
            switch airConditioners.count {
            case 0: phase = .needsSetup(message: "Nenhum ar-condicionado encontrado nesta conta")
            case 1: await selectDevice(airConditioners[0])
            default: phase = .choosingDevice(airConditioners)
            }
        } catch {
            phase = .needsSetup(message: Self.message(for: error))
        }
    }

    public func selectDevice(_ device: ThinQDevice) async {
        guard let pendingSetup else { return }
        do {
            try store.saveToken(pendingSetup.token)
        } catch {
            phase = .needsSetup(message: "Não foi possível salvar o token no Keychain")
            return
        }
        self.pendingSetup = nil
        store.deviceId = device.id
        store.deviceAlias = device.alias
        deviceAlias = device.alias
        client = pendingSetup.client
        await load()
    }

    /// "Trocar token…": apaga o token e volta para a configuração.
    public func resetCredentials() {
        stopPolling()
        store.deleteToken()
        client = nil
        capabilities = nil
        state = nil
        lastUpdated = nil
        statusMessage = nil
        phase = .needsSetup(message: nil)
    }

    /// Carrega profile e estado. Sem rede, fica em `.loading` e o polling tenta de novo.
    func load() async {
        phase = .loading
        capabilities = nil
        await refresh(force: true)
        if case .needsSetup = phase { return }
        startPolling()
    }

    // MARK: - Estado

    /// Busca o estado. Sem `force`, ignora se a última leitura tem menos de 5 s.
    public func refresh(force: Bool = false) async {
        guard let client, let deviceId = store.deviceId else { return }
        if !force, let lastUpdated, now().timeIntervalSince(lastUpdated) < Self.freshness { return }
        do {
            if capabilities == nil {
                capabilities = ACCapabilities(profile: try await client.profile(deviceId: deviceId))
            }
            var fresh = ACState(state: try await client.state(deviceId: deviceId))
            // Um −/+ ainda não enviado vence o valor antigo do servidor.
            if let pendingTargetTemp { fresh.targetTemp = pendingTargetTemp }
            state = fresh
            lastUpdated = now()
            statusMessage = nil
            isDeviceOffline = false
            pollInterval = Self.basePollInterval
            phase = .ready
        } catch {
            handle(error)
        }
    }

    public func startPolling() {
        pollTask?.cancel()
        pollTask = Task { [weak self] in
            while let self, !Task.isCancelled {
                do { try await self.sleep(self.pollInterval) } catch { return }
                await self.refresh(force: true)
            }
        }
    }

    public func stopPolling() {
        pollTask?.cancel()
        pollTask = nil
    }

    // MARK: - Erros

    func handle(_ error: Error) {
        if error is CancellationError { return }
        guard let error = error as? ThinQError else {
            statusMessage = "Erro: \(error.localizedDescription)"
            return
        }
        switch error {
        case .invalidToken:
            stopPolling()
            store.deleteToken()
            client = nil
            phase = .needsSetup(message: error.userMessage)
        case .rateLimited:
            pollInterval = min(pollInterval * 2, Self.maxPollInterval)
            statusMessage = error.userMessage
        case .deviceOffline:
            isDeviceOffline = true
            statusMessage = error.userMessage
        default:
            statusMessage = error.userMessage
        }
    }

    static func message(for error: Error) -> String {
        (error as? ThinQError)?.userMessage ?? error.localizedDescription
    }
}
