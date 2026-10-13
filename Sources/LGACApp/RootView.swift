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
                Text("Conectado: \(controller.deviceAlias)").padding()  // trocado na Task 12
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
                Button("Tentar de novo") { Task { await controller.refresh(force: true) } }
            }
        }
        .frame(maxWidth: .infinity)
        .padding(20)
    }
}
