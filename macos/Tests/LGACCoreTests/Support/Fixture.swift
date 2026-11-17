import Foundation
@testable import LGACCore

enum Fixture {
    struct Missing: Error { let name: String }

    static func json(_ name: String) throws -> JSONValue {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().deletingLastPathComponent()
        let url = root.appendingPathComponent("shared/fixtures/\(name).json")
        guard FileManager.default.fileExists(atPath: url.path) else {
            throw Missing(name: name)
        }
        return try JSONDecoder().decode(JSONValue.self, from: Data(contentsOf: url))
    }

    static func capabilities(_ name: String) throws -> ACCapabilities {
        ACCapabilities(profile: try json(name))
    }
}
