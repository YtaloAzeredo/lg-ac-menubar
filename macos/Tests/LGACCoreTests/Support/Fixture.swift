import Foundation
@testable import LGACCore

enum Fixture {
    struct Missing: Error { let name: String }

    static func json(_ name: String) throws -> JSONValue {
        guard let url = Bundle.module.url(forResource: name, withExtension: "json", subdirectory: "Fixtures") else {
            throw Missing(name: name)
        }
        return try JSONDecoder().decode(JSONValue.self, from: Data(contentsOf: url))
    }

    static func capabilities(_ name: String) throws -> ACCapabilities {
        ACCapabilities(profile: try json(name))
    }
}
