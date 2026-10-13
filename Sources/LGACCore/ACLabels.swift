import Foundation

/// Textos em português e SF Symbols para os valores da API.
public enum ACLabels {
    public static func jobMode(_ mode: String) -> String {
        switch mode {
        case "COOL": "Frio"
        case "HEAT": "Quente"
        case "AIR_DRY": "Seco"
        case "FAN": "Ventilar"
        case "AUTO": "Auto"
        default: humanize(mode)
        }
    }

    /// Ícone da barra de menu e dos botões de modo.
    public static func symbol(forJobMode mode: String?) -> String {
        switch mode ?? "" {
        case "COOL": "snowflake"
        case "HEAT": "sun.max"
        case "AIR_DRY": "drop"
        case "FAN": "fan"
        default: "thermometer.medium"
        }
    }

    public static func windStrength(_ value: String) -> String {
        switch value {
        case "LOW": "Baixa"
        case "MID": "Média"
        case "HIGH": "Alta"
        case "AUTO": "Auto"
        default: humanize(value)
        }
    }

    /// `80` → "1h 20min", `30` → "30 min", `120` → "2 h".
    public static func duration(minutes: Int) -> String {
        let hours = minutes / 60
        let rest = minutes % 60
        if hours == 0 { return "\(rest) min" }
        if rest == 0 { return "\(hours) h" }
        return "\(hours)h \(rest)min"
    }

    /// `23` → "23°", `23.5` → "23,5°" (em pt-BR).
    public static func temperature(_ value: Double, locale: Locale = Locale(identifier: "pt_BR")) -> String {
        value.formatted(.number.precision(.fractionLength(0...1)).locale(locale)) + "°"
    }

    /// `ENERGY_SAVING` → "Energy saving".
    static func humanize(_ raw: String) -> String {
        let words = raw.lowercased().split(separator: "_").joined(separator: " ")
        return words.prefix(1).uppercased() + words.dropFirst()
    }
}
