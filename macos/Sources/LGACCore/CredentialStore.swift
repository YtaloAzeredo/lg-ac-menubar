import Foundation
import Security

/// Onde o app guarda o token e as preferências.
public protocol CredentialStoring: AnyObject {
    var token: String? { get }
    func saveToken(_ token: String) throws
    func deleteToken()
    var country: String { get set }
    var deviceId: String? { get set }
    var deviceAlias: String? { get set }
    /// Gerado uma vez e reaproveitado (header `x-client-id`).
    var clientId: String { get }
}

public enum CredentialStoreError: Error, Equatable {
    case keychain(OSStatus)
}

/// Token no Keychain do macOS; o resto em `UserDefaults`.
public final class KeychainCredentialStore: CredentialStoring {
    private let service: String
    private let defaults: UserDefaults

    public init(service: String = "com.local.lgac", defaults: UserDefaults = .standard) {
        self.service = service
        self.defaults = defaults
    }

    public var token: String? {
        var query = baseQuery
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne
        var result: AnyObject?
        guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess,
            let data = result as? Data
        else { return nil }
        return String(data: data, encoding: .utf8)
    }

    public func saveToken(_ token: String) throws {
        deleteToken()
        var query = baseQuery
        query[kSecValueData as String] = Data(token.utf8)
        let status = SecItemAdd(query as CFDictionary, nil)
        guard status == errSecSuccess else { throw CredentialStoreError.keychain(status) }
    }

    public func deleteToken() {
        SecItemDelete(baseQuery as CFDictionary)
    }

    public var country: String {
        get { defaults.string(forKey: Keys.country) ?? "BR" }
        set { defaults.set(newValue, forKey: Keys.country) }
    }

    public var deviceId: String? {
        get { defaults.string(forKey: Keys.deviceId) }
        set { defaults.set(newValue, forKey: Keys.deviceId) }
    }

    public var deviceAlias: String? {
        get { defaults.string(forKey: Keys.deviceAlias) }
        set { defaults.set(newValue, forKey: Keys.deviceAlias) }
    }

    public var clientId: String {
        if let existing = defaults.string(forKey: Keys.clientId) { return existing }
        let created = "lgac-\(UUID().uuidString.lowercased())"
        defaults.set(created, forKey: Keys.clientId)
        return created
    }

    private var baseQuery: [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: "personal-access-token",
        ]
    }

    private enum Keys {
        static let country = "country"
        static let deviceId = "deviceId"
        static let deviceAlias = "deviceAlias"
        static let clientId = "clientId"
    }
}
