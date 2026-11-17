import Foundation
import Testing
@testable import LGACCore

@Suite struct SharedCommandsTests {
    struct Case: Decodable {
        struct Spec: Decodable {
            let type: String
            let value: JSONValue
        }

        let name: String
        let profile: String
        let command: Spec
        let payload: JSONValue
    }

    struct UnknownType: Error { let type: String }

    static func cases() throws -> [Case] {
        let url = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("shared/fixtures/commands.json")
        return try JSONDecoder().decode([Case].self, from: Data(contentsOf: url))
    }

    static func command(from spec: Case.Spec) throws -> ACCommand {
        switch (spec.type, spec.value) {
        case ("power", .bool(let on)): return .power(on)
        case ("jobMode", .string(let mode)): return .jobMode(mode)
        case ("targetTemp", .number(let value)): return .targetTemp(value)
        case ("windStrength", .string(let value)): return .windStrength(value)
        case ("swingUpDown", .bool(let on)): return .swingUpDown(on)
        case ("swingLeftRight", .bool(let on)): return .swingLeftRight(on)
        case ("offTimer", .number(let minutes)): return .offTimer(minutes: Int(minutes))
        case ("offTimer", .null): return .offTimer(minutes: nil)
        case ("displayLight", .bool(let on)): return .displayLight(on)
        default: throw UnknownType(type: spec.type)
        }
    }

    @Test func everySharedCasePayloadMatches() throws {
        for item in try Self.cases() {
            let caps = try Fixture.capabilities(item.profile)
            let command = try Self.command(from: item.command)
            let expected: JSONValue? = item.payload == .null ? nil : item.payload
            #expect(command.payload(using: caps) == expected, "caso \"\(item.name)\": payload diferente do esperado")
        }
    }

    @Test func sharedCasesCoverEveryCommandType() throws {
        let types = Set(try Self.cases().map(\.command.type))
        let all: Set<String> = [
            "power", "jobMode", "targetTemp", "windStrength",
            "swingUpDown", "swingLeftRight", "offTimer", "displayLight",
        ]
        #expect(types == all, "tipos ausentes ou desconhecidos: \(types.symmetricDifference(all).sorted())")
    }
}
