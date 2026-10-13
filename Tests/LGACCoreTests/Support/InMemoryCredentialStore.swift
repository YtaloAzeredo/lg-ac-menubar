import Foundation
@testable import LGACCore

final class InMemoryCredentialStore: CredentialStoring {
    var token: String?
    var country = "BR"
    var deviceId: String?
    var deviceAlias: String?
    let clientId = "lgac-test-client"
    var saveError: Error?

    func saveToken(_ token: String) throws {
        if let saveError { throw saveError }
        self.token = token
    }

    func deleteToken() { token = nil }

    /// Store de quem já configurou o app.
    static func configured() -> InMemoryCredentialStore {
        let store = InMemoryCredentialStore()
        store.token = "tok"
        store.deviceId = "ac-1"
        store.deviceAlias = "Ar do Quarto"
        return store
    }
}
