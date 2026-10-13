import Foundation
import Testing
@testable import LGACCore

@Suite struct ACCapabilitiesTests {
    @Test func fullProfile() throws {
        let caps = try Fixture.capabilities("profile-full")
        #expect(caps.power)
        // AIR_CLEAN fica de fora: o app só exibe os cinco modos suportados.
        #expect(caps.jobModes == ["COOL", "HEAT", "AIR_DRY", "FAN", "AUTO"])
        // targetTemperature em temperatureInUnits é só leitura; o gravável é o de `temperature`.
        #expect(caps.targetTemp == .init(resource: "temperature", key: "targetTemperature", min: 18, max: 30, step: 1))
        #expect(caps.windStrengths == ["LOW", "MID", "HIGH", "AUTO"])
        #expect(caps.windStrengthKey == "windStrength")
        #expect(caps.swingUpDown)
        #expect(caps.swingLeftRight)
        #expect(caps.offTimer == .timer)
        #expect(caps.displayLight == .onOff)
    }

    /// Modelos com os dois controles: o de `windStrength` é o do controle remoto (com Auto);
    /// o `windStrengthDetail` traz opções como NATURE e não tem Auto.
    @Test func prefersWindStrengthWhenBothAreWritable() {
        let profile: JSONValue = [
            "property": [
                "airFlow": [
                    "windStrength": [
                        "mode": ["r", "w"], "type": "enum",
                        "value": ["w": ["LOW", "MID", "HIGH", "AUTO"]],
                    ],
                    "windStrengthDetail": [
                        "mode": ["r", "w"], "type": "enum",
                        "value": ["w": ["NATURE", "LOW", "MID", "HIGH"]],
                    ],
                ]
            ]
        ]
        let caps = ACCapabilities(profile: profile)
        #expect(caps.windStrengthKey == "windStrength")
        #expect(caps.windStrengths == ["LOW", "MID", "HIGH", "AUTO"])
    }

    @Test func basicProfile() throws {
        let caps = try Fixture.capabilities("profile-basic")
        #expect(caps.power)
        #expect(caps.jobModes == ["COOL", "FAN"])
        #expect(caps.targetTemp == .init(resource: "temperatureInUnits", key: "targetTemperature", min: 16, max: 30, step: 0.5))
        #expect(caps.windStrengthKey == "windStrengthDetail")
        #expect(caps.windStrengths == ["LOW", "HIGH"])
        #expect(caps.swingUpDown)
        #expect(!caps.swingLeftRight)
        #expect(caps.offTimer == nil)
        #expect(caps.displayLight == nil)
    }

    @Test func coolTargetIsLastResort() {
        let profile: JSONValue = [
            "property": [
                "temperatureInUnits": [
                    [
                        "unit": "C",
                        "targetTemperature": ["type": "number", "mode": ["r"]],
                        "coolTargetTemperature": [
                            "type": "range", "mode": ["w"], "value": ["w": ["min": 18, "max": 30, "step": 1]],
                        ],
                    ]
                ]
            ]
        ]
        #expect(
            ACCapabilities(profile: profile).targetTemp
                == .init(resource: "temperatureInUnits", key: "coolTargetTemperature", min: 18, max: 30, step: 1))
    }

    @Test func sleepTimerFallback() {
        let profile: JSONValue = [
            "property": ["sleepTimer": ["relativeHourToStop": ["type": "number", "mode": ["r", "w"]]]]
        ]
        #expect(ACCapabilities(profile: profile).offTimer == .sleepTimer)
    }

    @Test func booleanDisplayLight() {
        let profile: JSONValue = [
            "property": ["display": ["light": ["type": "boolean", "mode": ["r", "w"], "value": ["w": [true, false]]]]]
        ]
        #expect(ACCapabilities(profile: profile).displayLight == .boolean)
    }

    @Test func readOnlyControlsAreHidden() {
        let profile: JSONValue = [
            "property": ["windDirection": ["rotateUpDown": ["type": "boolean", "mode": ["r"]]]]
        ]
        #expect(!ACCapabilities(profile: profile).swingUpDown)
    }

    @Test func emptyProfile() {
        let caps = ACCapabilities(profile: [:])
        #expect(!caps.power)
        #expect(caps.jobModes.isEmpty)
        #expect(caps.targetTemp == nil)
        #expect(caps.windStrengths.isEmpty)
        #expect(caps.offTimer == nil)
        #expect(caps.displayLight == nil)
    }

    @Test func clampRoundsToStepAndRange() {
        let temp = ACCapabilities.TemperatureControl(resource: "temperature", key: "targetTemperature", min: 16, max: 30, step: 0.5)
        #expect(temp.clamp(31) == 30)
        #expect(temp.clamp(15) == 16)
        #expect(temp.clamp(23.3) == 23.5)
        #expect(temp.clamp(23.2) == 23)
    }
}
