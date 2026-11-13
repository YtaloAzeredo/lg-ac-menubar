import Foundation
@testable import LGACCore

final class InMemoryCredentialStore: CredentialStoring {
    var country = "BR"
    var deviceId: String?
    var deviceAlias: String?
    let clientId = "lgac-test-client"
    var saveError: Error?
    /// Quantas vezes o token foi lido, salvo ou apagado (no Keychain real, cada acesso pode pedir a senha do Mac).
    private(set) var keychainAccesses = 0
    private var storedToken: String?

    var token: String? {
        get {
            keychainAccesses += 1
            return storedToken
        }
        set { storedToken = newValue }
    }

    func saveToken(_ token: String) throws {
        keychainAccesses += 1
        if let saveError { throw saveError }
        storedToken = token
    }

    func deleteToken() {
        keychainAccesses += 1
        storedToken = nil
    }

    /// Store de quem já configurou o app.
    static func configured() -> InMemoryCredentialStore {
        let store = InMemoryCredentialStore()
        store.token = "tok"
        store.deviceId = "ac-1"
        store.deviceAlias = "Ar do Quarto"
        return store
    }
}
