import Foundation
import Testing
@testable import LGACCore

/// Usa um service e uma suíte de UserDefaults exclusivos, apagados no fim de cada teste.
@Suite(.serialized) struct KeychainCredentialStoreTests {
    let suiteName = "lgac-tests-\(UUID().uuidString)"

    private func withStore(_ body: (KeychainCredentialStore) throws -> Void) rethrows {
        let defaults = UserDefaults(suiteName: suiteName)!
        let store = KeychainCredentialStore(service: "com.local.lgac.tests.\(UUID().uuidString)", defaults: defaults)
        defer {
            store.deleteToken()
            defaults.removePersistentDomain(forName: suiteName)
        }
        try body(store)
    }

    @Test func tokenRoundTrip() throws {
        try withStore { store in
            #expect(store.token == nil)
            try store.saveToken("primeiro")
            try store.saveToken("segundo")
            #expect(store.token == "segundo")
            store.deleteToken()
            #expect(store.token == nil)
        }
    }

    @Test func preferences() {
        withStore { store in
            #expect(store.country == "BR")
            store.country = "US"
            store.deviceId = "ac-1"
            store.deviceAlias = "Sala"
            #expect(store.country == "US")
            #expect(store.deviceId == "ac-1")
            #expect(store.deviceAlias == "Sala")
        }
    }

    @Test func clientIdIsStable() {
        withStore { store in
            let first = store.clientId
            #expect(first.hasPrefix("lgac-"))
            #expect(store.clientId == first)
        }
    }
}
