import Foundation

/// O que o modelo do aparelho permite controlar, lido do profile (`GET /devices/{id}/profile`).
/// Controle ausente ou só de leitura fica `nil`/`false`/vazio e não aparece na interface.
public struct ACCapabilities: Sendable, Equatable {
    public struct TemperatureControl: Sendable, Equatable {
        /// `temperatureInUnits` ou `temperature`.
        public let resource: String
        /// `targetTemperature` ou `coolTargetTemperature`.
        public let key: String
        public let min: Double
        public let max: Double
        public let step: Double

        /// Arredonda para o passo do modelo e limita à faixa permitida.
        public func clamp(_ value: Double) -> Double {
            let stepped = (value / step).rounded() * step
            return Swift.min(max, Swift.max(min, stepped))
        }
    }

    public enum OffTimerKind: String, Sendable {
        case timer
        case sleepTimer
    }

    public enum DisplayLightKind: Sendable {
        /// Valores `"ON"` / `"OFF"`.
        case onOff
        /// Valores `true` / `false`.
        case boolean
    }

    /// Modos que o app sabe exibir, na ordem do painel.
    public static let supportedJobModes = ["COOL", "HEAT", "AIR_DRY", "FAN", "AUTO"]

    public let power: Bool
    public let jobModes: [String]
    public let targetTemp: TemperatureControl?
    public let windStrengths: [String]
    public let windStrengthKey: String
    public let swingUpDown: Bool
    public let swingLeftRight: Bool
    public let offTimer: OffTimerKind?
    public let displayLight: DisplayLightKind?

    public init(profile: JSONValue) {
        let property = profile["property"] ?? [:]

        power = Self.writableEnum(property["operation"]?["airConOperationMode"]).contains("POWER_ON")

        let modes = Self.writableEnum(property["airConJobMode"]?["currentJobMode"])
        jobModes = Self.supportedJobModes.filter(modes.contains)

        targetTemp = Self.temperatureControl(in: property)

        // `windStrength` é o do controle remoto (com Auto); `windStrengthDetail` só quando ele não existe.
        if let strength = property["airFlow"]?["windStrength"], Self.isWritable(strength) {
            windStrengthKey = "windStrength"
            windStrengths = Self.writableEnum(strength)
        } else {
            windStrengthKey = "windStrengthDetail"
            windStrengths = Self.writableEnum(property["airFlow"]?["windStrengthDetail"])
        }

        swingUpDown = Self.isWritable(property["windDirection"]?["rotateUpDown"])
        swingLeftRight = Self.isWritable(property["windDirection"]?["rotateLeftRight"])

        if Self.isWritable(property["timer"]?["relativeHourToStop"]) {
            offTimer = .timer
        } else if Self.isWritable(property["sleepTimer"]?["relativeHourToStop"]) {
            offTimer = .sleepTimer
        } else {
            offTimer = nil
        }

        let light = property["display"]?["light"]
        if Self.isWritable(light) {
            displayLight = light?["type"]?.stringValue == "boolean" ? .boolean : .onOff
        } else {
            displayLight = nil
        }
    }

    static func isWritable(_ entry: JSONValue?) -> Bool {
        entry?["mode"]?.arrayValue?.contains("w") ?? false
    }

    static func writableEnum(_ entry: JSONValue?) -> [String] {
        guard isWritable(entry) else { return [] }
        return entry?["value"]?["w"]?.arrayValue?.compactMap(\.stringValue) ?? []
    }

    /// Item `"unit": "C"` de `temperatureInUnits` (é uma lista, no profile e no estado).
    static func celsiusEntry(_ resource: JSONValue?) -> JSONValue? {
        resource?.arrayValue?.first { $0["unit"]?.stringValue == "C" }
    }

    /// Primeiro candidato gravável do tipo `range`, na ordem definida no spec.
    static func temperatureControl(in property: JSONValue) -> TemperatureControl? {
        let inUnits = celsiusEntry(property["temperatureInUnits"])
        let legacy = property["temperature"]
        let candidates: [(resource: String, key: String, entry: JSONValue?)] = [
            ("temperatureInUnits", "targetTemperature", inUnits?["targetTemperature"]),
            ("temperature", "targetTemperature", legacy?["targetTemperature"]),
            ("temperatureInUnits", "coolTargetTemperature", inUnits?["coolTargetTemperature"]),
            ("temperature", "coolTargetTemperature", legacy?["coolTargetTemperature"]),
        ]
        for candidate in candidates {
            guard isWritable(candidate.entry), let range = candidate.entry?["value"]?["w"],
                let min = range["min"]?.doubleValue, let max = range["max"]?.doubleValue
            else { continue }
            let step = range["step"]?.doubleValue ?? 1
            return TemperatureControl(
                resource: candidate.resource, key: candidate.key, min: min, max: max, step: step > 0 ? step : 1)
        }
        return nil
    }
}
