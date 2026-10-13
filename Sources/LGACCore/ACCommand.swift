import Foundation

/// Uma ação do usuário no painel.
public enum ACCommand: Sendable, Equatable {
    case power(Bool)
    case jobMode(String)
    case targetTemp(Double)
    case windStrength(String)
    case swingUpDown(Bool)
    case swingLeftRight(Bool)
    /// `nil` ou `0` cancela o timer.
    case offTimer(minutes: Int?)
    case displayLight(Bool)

    /// Corpo do `POST /control`, ou `nil` se o modelo não suporta o comando.
    public func payload(using caps: ACCapabilities) -> JSONValue? {
        switch self {
        case .power(let on):
            guard caps.power else { return nil }
            return ["operation": ["airConOperationMode": .string(on ? "POWER_ON" : "POWER_OFF")]]

        case .jobMode(let mode):
            guard caps.jobModes.contains(mode) else { return nil }
            return ["airConJobMode": ["currentJobMode": .string(mode)]]

        case .targetTemp(let value):
            guard let temp = caps.targetTemp else { return nil }
            var body: [String: JSONValue] = [temp.key: .number(temp.clamp(value))]
            if temp.resource == "temperatureInUnits" { body["unit"] = "C" }
            return [temp.resource: .object(body)]

        case .windStrength(let value):
            guard caps.windStrengths.contains(value) else { return nil }
            return ["airFlow": [caps.windStrengthKey: .string(value)]]

        case .swingUpDown(let on):
            guard caps.swingUpDown else { return nil }
            return ["windDirection": ["rotateUpDown": .bool(on)]]

        case .swingLeftRight(let on):
            guard caps.swingLeftRight else { return nil }
            return ["windDirection": ["rotateLeftRight": .bool(on)]]

        case .offTimer(let minutes):
            guard let kind = caps.offTimer else { return nil }
            guard let minutes, minutes > 0 else {
                return [kind.rawValue: ["relativeStopTimer": "UNSET"]]
            }
            var body: [String: JSONValue] = ["relativeHourToStop": .number(Double(minutes / 60))]
            if minutes % 60 != 0 { body["relativeMinuteToStop"] = .number(Double(minutes % 60)) }
            return [kind.rawValue: .object(body)]

        case .displayLight(let on):
            guard let kind = caps.displayLight else { return nil }
            let value: JSONValue = kind == .boolean ? .bool(on) : .string(on ? "ON" : "OFF")
            return ["display": ["light": value]]
        }
    }

    /// Estado previsto depois do comando, para a interface responder na hora (atualização otimista).
    public func apply(to state: ACState) -> ACState {
        var next = state
        switch self {
        case .power(let on): next.isOn = on
        case .jobMode(let mode): next.jobMode = mode
        case .targetTemp(let value): next.targetTemp = value
        case .windStrength(let value): next.windStrength = value
        case .swingUpDown(let on): next.swingUpDown = on
        case .swingLeftRight(let on): next.swingLeftRight = on
        case .offTimer(let minutes): next.offTimerRemainingMinutes = (minutes ?? 0) > 0 ? minutes : nil
        case .displayLight(let on): next.displayLightOn = on
        }
        return next
    }
}
