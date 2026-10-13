import Foundation

/// Estado atual do aparelho (`GET /devices/{id}/state`). Campo ausente na resposta fica `nil`.
public struct ACState: Sendable, Equatable {
    public var isOn: Bool?
    public var jobMode: String?
    public var targetTemp: Double?
    public var currentTemp: Double?
    public var windStrength: String?
    public var swingUpDown: Bool?
    public var swingLeftRight: Bool?
    public var displayLightOn: Bool?
    public var offTimerRemainingMinutes: Int?

    public init(
        isOn: Bool? = nil, jobMode: String? = nil, targetTemp: Double? = nil, currentTemp: Double? = nil,
        windStrength: String? = nil, swingUpDown: Bool? = nil, swingLeftRight: Bool? = nil,
        displayLightOn: Bool? = nil, offTimerRemainingMinutes: Int? = nil
    ) {
        self.isOn = isOn
        self.jobMode = jobMode
        self.targetTemp = targetTemp
        self.currentTemp = currentTemp
        self.windStrength = windStrength
        self.swingUpDown = swingUpDown
        self.swingLeftRight = swingLeftRight
        self.displayLightOn = displayLightOn
        self.offTimerRemainingMinutes = offTimerRemainingMinutes
    }

    public init(state: JSONValue) {
        let celsius = ACCapabilities.celsiusEntry(state["temperatureInUnits"])
        let legacy = state["temperature"]
        self.init(
            isOn: state["operation"]?["airConOperationMode"]?.stringValue.map { $0 == "POWER_ON" },
            jobMode: state["airConJobMode"]?["currentJobMode"]?.stringValue,
            targetTemp: celsius?["targetTemperature"]?.doubleValue ?? legacy?["targetTemperature"]?.doubleValue,
            currentTemp: celsius?["currentTemperature"]?.doubleValue ?? legacy?["currentTemperature"]?.doubleValue,
            windStrength: state["airFlow"]?["windStrengthDetail"]?.stringValue
                ?? state["airFlow"]?["windStrength"]?.stringValue,
            swingUpDown: state["windDirection"]?["rotateUpDown"]?.boolValue,
            swingLeftRight: state["windDirection"]?["rotateLeftRight"]?.boolValue,
            displayLightOn: Self.lightIsOn(state["display"]?["light"]),
            offTimerRemainingMinutes: Self.remainingMinutes(state["timer"]) ?? Self.remainingMinutes(state["sleepTimer"]))
    }

    static func lightIsOn(_ value: JSONValue?) -> Bool? {
        switch value {
        case .bool(let on)?: return on
        case .string(let text)?: return text == "ON"
        default: return nil
        }
    }

    /// Tempo restante até desligar, ou `nil` se o timer não está ativo.
    static func remainingMinutes(_ timer: JSONValue?) -> Int? {
        guard let timer, timer["relativeStopTimer"]?.stringValue != "UNSET" else { return nil }
        let total = (timer["relativeHourToStop"]?.intValue ?? 0) * 60 + (timer["relativeMinuteToStop"]?.intValue ?? 0)
        return total > 0 ? total : nil
    }
}
