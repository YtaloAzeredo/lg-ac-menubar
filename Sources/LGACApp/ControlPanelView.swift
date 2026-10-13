import LGACCore
import SwiftUI

/// Painel no estilo Central de Controle. Só mostra o que o modelo suporta.
struct ControlPanelView: View {
    let controller: ACController

    private static let timerOptions = [30, 60, 120, 180, 240, 360, 480]

    private var caps: ACCapabilities? { controller.capabilities }
    private var state: ACState { controller.state ?? ACState() }
    private var isOn: Bool { state.isOn == true }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            header
            Divider()
            VStack(alignment: .leading, spacing: 12) {
                if caps?.targetTemp != nil { temperatureRow }
                if let modes = caps?.jobModes, !modes.isEmpty {
                    section("Modo") {
                        ChoiceGrid(
                            options: modes, selected: state.jobMode, title: ACLabels.jobMode,
                            symbol: { ACLabels.symbol(forJobMode: $0) }
                        ) { mode in send(.jobMode(mode)) }
                    }
                }
                if let speeds = caps?.windStrengths, !speeds.isEmpty {
                    section("Ventilador") {
                        ChoiceGrid(options: speeds, selected: state.windStrength, title: ACLabels.windStrength) { speed in
                            send(.windStrength(speed))
                        }
                    }
                }
                switches
            }
            .disabled(!isOn)
            .opacity(isOn ? 1 : 0.45)
            Divider()
            footer
        }
        .padding(14)
    }

    // MARK: - Seções

    private var header: some View {
        HStack {
            VStack(alignment: .leading, spacing: 2) {
                Text(controller.deviceAlias.isEmpty ? "Ar-condicionado" : controller.deviceAlias)
                    .font(.headline)
                if let current = state.currentTemp {
                    Text("Ambiente \(ACLabels.temperature(current))")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
            Spacer()
            Button {
                send(.power(!isOn))
            } label: {
                Image(systemName: "power")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(isOn ? Color.white : Color.primary)
                    .frame(width: 34, height: 34)
                    .background(Circle().fill(isOn ? Color.accentColor : Color.secondary.opacity(0.2)))
            }
            .buttonStyle(.plain)
            .disabled(caps?.power != true)
            .help(isOn ? "Desligar" : "Ligar")
        }
    }

    private var temperatureRow: some View {
        HStack {
            Spacer()
            roundButton("minus") { controller.stepTargetTemp(by: -1) }
            Text(state.targetTemp.map { ACLabels.temperature($0) } ?? "–")
                .font(.system(size: 30, weight: .light, design: .rounded))
                .monospacedDigit()
                .frame(minWidth: 96)
            roundButton("plus") { controller.stepTargetTemp(by: 1) }
            Spacer()
        }
    }

    private var switches: some View {
        VStack(spacing: 6) {
            if caps?.swingUpDown == true {
                switchRow("Swing vertical", symbol: "arrow.up.and.down", isOn: state.swingUpDown ?? false) {
                    .swingUpDown($0)
                }
            }
            if caps?.swingLeftRight == true {
                switchRow("Swing horizontal", symbol: "arrow.left.and.right", isOn: state.swingLeftRight ?? false) {
                    .swingLeftRight($0)
                }
            }
            if caps?.displayLight != nil {
                switchRow("Luz do visor", symbol: "lightbulb", isOn: state.displayLightOn ?? false) {
                    .displayLight($0)
                }
            }
            if caps?.offTimer != nil { timerRow }
        }
    }

    private var timerRow: some View {
        HStack {
            Label("Desligar em", systemImage: "timer")
            Spacer()
            Menu {
                Button("Desativado") { send(.offTimer(minutes: nil)) }
                ForEach(Self.timerOptions, id: \.self) { minutes in
                    Button(ACLabels.duration(minutes: minutes)) { send(.offTimer(minutes: minutes)) }
                }
            } label: {
                Text(state.offTimerRemainingMinutes.map { ACLabels.duration(minutes: $0) } ?? "Desativado")
            }
            .fixedSize()
        }
    }

    private var footer: some View {
        HStack(spacing: 10) {
            if let message = controller.statusMessage {
                Label(message, systemImage: "exclamationmark.triangle")
                    .foregroundStyle(.orange)
                    .lineLimit(2)
            } else if let updated = controller.lastUpdated {
                Text("Atualizado há \(Text(updated, style: .relative))")
                    .foregroundStyle(.secondary)
            }
            Spacer()
            Button {
                Task { await controller.refresh(force: true) }
            } label: {
                Image(systemName: "arrow.clockwise")
            }
            .buttonStyle(.borderless)
            .help("Atualizar agora")
            SettingsMenu(controller: controller)
        }
        .font(.caption)
    }

    // MARK: - Peças

    private func send(_ command: ACCommand) {
        Task { await controller.send(command) }
    }

    private func section(_ title: String, @ViewBuilder content: () -> some View) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            content()
        }
    }

    private func roundButton(_ symbol: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Image(systemName: symbol)
                .font(.system(size: 13, weight: .semibold))
                .frame(width: 30, height: 30)
                .background(Circle().fill(Color.secondary.opacity(0.15)))
        }
        .buttonStyle(.plain)
    }

    /// `command` monta o comando a partir do novo valor da chave.
    private func switchRow(
        _ title: String, symbol: String, isOn: Bool, command: @escaping @Sendable (Bool) -> ACCommand
    ) -> some View {
        let controller = controller
        return HStack {
            Label(title, systemImage: symbol)
            Spacer()
            Toggle(
                title,
                isOn: Binding(get: { isOn }, set: { value in Task { await controller.send(command(value)) } }))
                .labelsHidden()
                .toggleStyle(.switch)
                .controlSize(.mini)
        }
    }
}

/// Grade de botões (modos, velocidades) com o valor atual destacado.
struct ChoiceGrid: View {
    let options: [String]
    let selected: String?
    let title: (String) -> String
    var symbol: ((String) -> String)?
    let choose: (String) -> Void

    var body: some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 66), spacing: 6)], spacing: 6) {
            ForEach(options, id: \.self) { option in
                let isSelected = option == selected
                Button {
                    choose(option)
                } label: {
                    VStack(spacing: 3) {
                        if let symbol {
                            Image(systemName: symbol(option))
                        }
                        Text(title(option)).font(.caption)
                    }
                    .frame(maxWidth: .infinity, minHeight: symbol == nil ? 26 : 40)
                    .foregroundStyle(isSelected ? Color.white : Color.primary)
                    .background(
                        RoundedRectangle(cornerRadius: 8)
                            .fill(isSelected ? Color.accentColor : Color.secondary.opacity(0.12)))
                }
                .buttonStyle(.plain)
            }
        }
    }
}
