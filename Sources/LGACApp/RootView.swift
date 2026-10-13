import LGACCore
import SwiftUI

/// Conteúdo do painel: escolhe a tela conforme a fase do controlador.
struct RootView: View {
    let controller: ACController

    var body: some View {
        Group {
            switch controller.phase {
            case .needsSetup(let message):
                SetupView(controller: controller, message: message)
            case .choosingDevice(let devices):
                DeviceChoiceView(controller: controller, devices: devices)
            case .loading:
                LoadingView(controller: controller)
            case .ready:
                ControlPanelView(controller: controller)
            }
        }
        .frame(width: 320)
        .onAppear { Task { await controller.refresh() } }
    }
}

struct LoadingView: View {
    let controller: ACController

    var body: some View {
        VStack(spacing: 10) {
            ProgressView().controlSize(.small)
            if let message = controller.statusMessage {
                Text(message).font(.caption).foregroundStyle(.secondary)
                HStack(spacing: 10) {
                    Button("Tentar de novo") { Task { await controller.refresh(force: true) } }
                    SettingsMenu(controller: controller)
                }
            }
        }
        .frame(maxWidth: .infinity)
        .padding(20)
    }
}
