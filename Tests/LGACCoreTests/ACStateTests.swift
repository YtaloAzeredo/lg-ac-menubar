import Foundation
import Testing
@testable import LGACCore

@Suite struct ACStateTests {
    @Test func fullState() throws {
        let state = ACState(state: try Fixture.json("state-full"))
        #expect(state == ACState(
            isOn: true, jobMode: "COOL", targetTemp: 23, currentTemp: 25, windStrength: "MID",
            swingUpDown: false, swingLeftRight: true, displayLightOn: true, offTimerRemainingMinutes: 80))
    }

    @Test func basicState() throws {
        let state = ACState(state: try Fixture.json("state-basic"))
        #expect(state == ACState(
            isOn: false, jobMode: "FAN", targetTemp: 22.5, currentTemp: 27.5, windStrength: "HIGH",
            swingUpDown: true))
    }

    @Test func emptyStateHasNoValues() {
        #expect(ACState(state: [:]) == ACState())
    }

    @Test func legacyTemperatureResource() {
        let state = ACState(state: ["temperature": ["currentTemperature": 26, "targetTemperature": 21, "unit": "C"]])
        #expect(state.targetTemp == 21)
        #expect(state.currentTemp == 26)
    }

    @Test func unsetTimerIsIgnored() {
        let state = ACState(state: ["timer": ["relativeStopTimer": "UNSET", "relativeHourToStop": 2]])
        #expect(state.offTimerRemainingMinutes == nil)
    }

    @Test func sleepTimerRemaining() {
        let state = ACState(state: [
            "sleepTimer": ["relativeStopTimer": "SET", "relativeHourToStop": 0, "relativeMinuteToStop": 30]
        ])
        #expect(state.offTimerRemainingMinutes == 30)
    }

    @Test func booleanLight() {
        #expect(ACState(state: ["display": ["light": false]]).displayLightOn == false)
    }
}
