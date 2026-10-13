import Foundation
import Testing
@testable import LGACCore

@Suite struct ACLabelsTests {
    @Test func jobModes() {
        #expect(["COOL", "HEAT", "AIR_DRY", "FAN", "AUTO"].map(ACLabels.jobMode) == ["Frio", "Quente", "Seco", "Ventilar", "Auto"])
        #expect(ACLabels.jobMode("ENERGY_SAVING") == "Energy saving")
    }

    @Test func symbols() {
        #expect(ACLabels.symbol(forJobMode: "COOL") == "snowflake")
        #expect(ACLabels.symbol(forJobMode: "HEAT") == "sun.max")
        #expect(ACLabels.symbol(forJobMode: nil) == "thermometer.medium")
    }

    @Test func windStrengths() {
        #expect(["LOW", "MID", "HIGH", "AUTO"].map(ACLabels.windStrength) == ["Baixa", "Média", "Alta", "Auto"])
        #expect(ACLabels.windStrength("POWER") == "Power")
    }

    @Test func durations() {
        #expect(ACLabels.duration(minutes: 30) == "30 min")
        #expect(ACLabels.duration(minutes: 120) == "2 h")
        #expect(ACLabels.duration(minutes: 80) == "1h 20min")
    }

    @Test func temperatures() {
        let ptBR = Locale(identifier: "pt_BR")
        #expect(ACLabels.temperature(23, locale: ptBR) == "23°")
        #expect(ACLabels.temperature(23.5, locale: ptBR) == "23,5°")
        #expect(ACLabels.temperature(22.5) == "22,5°")
    }
}
