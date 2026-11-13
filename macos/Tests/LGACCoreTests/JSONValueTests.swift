import Foundation
import Testing
@testable import LGACCore

@Suite struct JSONValueTests {
    @Test func decodesMixedDocument() throws {
        let json = #"{"a": 1, "b": true, "c": "x", "d": [1.5, null], "e": {"f": false}}"#
        let value = try JSONDecoder().decode(JSONValue.self, from: Data(json.utf8))
        #expect(value == ["a": 1, "b": true, "c": "x", "d": [1.5, .null], "e": ["f": false]])
    }

    @Test func keepsBoolAndNumberApart() throws {
        let decoder = JSONDecoder()
        #expect(try decoder.decode(JSONValue.self, from: Data("1".utf8)) == .number(1))
        #expect(try decoder.decode(JSONValue.self, from: Data("0".utf8)) == .number(0))
        #expect(try decoder.decode(JSONValue.self, from: Data("true".utf8)) == .bool(true))
    }

    @Test func encodesWholeNumbersWithoutDecimals() throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = .sortedKeys
        let value: JSONValue = ["h": 2, "t": 23.5, "on": true]
        #expect(String(decoding: try encoder.encode(value), as: UTF8.self) == #"{"h":2,"on":true,"t":23.5}"#)
    }

    @Test func accessors() {
        let value: JSONValue = ["s": "x", "n": 3, "b": false, "a": [1], "o": ["k": "v"]]
        #expect(value["s"]?.stringValue == "x")
        #expect(value["n"]?.doubleValue == 3)
        #expect(value["n"]?.intValue == 3)
        #expect(value["b"]?.boolValue == false)
        #expect(value["a"]?.arrayValue == [1])
        #expect(value["o"]?["k"]?.stringValue == "v")
        #expect(value["missing"] == nil)
        #expect(value["s"]?.doubleValue == nil)
        #expect(JSONValue.string("x")["k"] == nil)
    }
}
