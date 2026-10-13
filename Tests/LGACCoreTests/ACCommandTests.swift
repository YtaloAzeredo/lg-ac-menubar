import Foundation
import Testing
@testable import LGACCore

@Suite struct ACCommandTests {
    let full: ACCapabilities
    let basic: ACCapabilities

    init() throws {
        full = try Fixture.capabilities("profile-full")
        basic = try Fixture.capabilities("profile-basic")
    }

    @Test func power() {
        #expect(ACCommand.power(true).payload(using: full) == ["operation": ["airConOperationMode": "POWER_ON"]])
        #expect(ACCommand.power(false).payload(using: full) == ["operation": ["airConOperationMode": "POWER_OFF"]])
    }

    @Test func jobMode() {
        #expect(ACCommand.jobMode("HEAT").payload(using: full) == ["airConJobMode": ["currentJobMode": "HEAT"]])
        #expect(ACCommand.jobMode("HEAT").payload(using: basic) == nil)
    }

    @Test func targetTempOnLegacyResource() {
        #expect(ACCommand.targetTemp(24).payload(using: full) == ["temperature": ["targetTemperature": 24]])
    }

    @Test func targetTempInUnitsIsClampedAndCarriesUnit() {
        #expect(ACCommand.targetTemp(22.5).payload(using: basic)
            == ["temperatureInUnits": ["targetTemperature": 22.5, "unit": "C"]])
        #expect(ACCommand.targetTemp(40).payload(using: basic)
            == ["temperatureInUnits": ["targetTemperature": 30, "unit": "C"]])
    }

    @Test func windStrengthUsesProfileKey() {
        #expect(ACCommand.windStrength("HIGH").payload(using: full) == ["airFlow": ["windStrength": "HIGH"]])
        #expect(ACCommand.windStrength("HIGH").payload(using: basic) == ["airFlow": ["windStrengthDetail": "HIGH"]])
        #expect(ACCommand.windStrength("MID").payload(using: basic) == nil)
    }

    @Test func swing() {
        #expect(ACCommand.swingUpDown(true).payload(using: full) == ["windDirection": ["rotateUpDown": true]])
        #expect(ACCommand.swingLeftRight(false).payload(using: full) == ["windDirection": ["rotateLeftRight": false]])
        #expect(ACCommand.swingLeftRight(true).payload(using: basic) == nil)
    }

    @Test func offTimer() {
        #expect(ACCommand.offTimer(minutes: 120).payload(using: full) == ["timer": ["relativeHourToStop": 2]])
        #expect(ACCommand.offTimer(minutes: 90).payload(using: full)
            == ["timer": ["relativeHourToStop": 1, "relativeMinuteToStop": 30]])
        #expect(ACCommand.offTimer(minutes: 30).payload(using: full)
            == ["timer": ["relativeHourToStop": 0, "relativeMinuteToStop": 30]])
        #expect(ACCommand.offTimer(minutes: nil).payload(using: full) == ["timer": ["relativeStopTimer": "UNSET"]])
        #expect(ACCommand.offTimer(minutes: 60).payload(using: basic) == nil)
    }

    @Test func offTimerOnSleepTimerModels() {
        let caps = ACCapabilities(profile: [
            "property": ["sleepTimer": ["relativeHourToStop": ["type": "number", "mode": ["r", "w"]]]]
        ])
        #expect(ACCommand.offTimer(minutes: 60).payload(using: caps) == ["sleepTimer": ["relativeHourToStop": 1]])
    }

    @Test func displayLight() {
        #expect(ACCommand.displayLight(false).payload(using: full) == ["display": ["light": "OFF"]])
        #expect(ACCommand.displayLight(true).payload(using: basic) == nil)
        let booleanCaps = ACCapabilities(profile: [
            "property": ["display": ["light": ["type": "boolean", "mode": ["r", "w"]]]]
        ])
        #expect(ACCommand.displayLight(true).payload(using: booleanCaps) == ["display": ["light": true]])
    }

    @Test func applyUpdatesOnlyTheTouchedField() {
        let before = ACState(isOn: true, jobMode: "COOL", targetTemp: 23, offTimerRemainingMinutes: 45)
        #expect(ACCommand.power(false).apply(to: before).isOn == false)
        #expect(ACCommand.jobMode("FAN").apply(to: before) == ACState(isOn: true, jobMode: "FAN", targetTemp: 23, offTimerRemainingMinutes: 45))
        #expect(ACCommand.targetTemp(25).apply(to: before).targetTemp == 25)
        #expect(ACCommand.offTimer(minutes: nil).apply(to: before).offTimerRemainingMinutes == nil)
        #expect(ACCommand.offTimer(minutes: 120).apply(to: before).offTimerRemainingMinutes == 120)
        #expect(ACCommand.displayLight(false).apply(to: before).displayLightOn == false)
    }
}
