import AppKit
import LGACCore
import ServiceManagement
import SwiftUI

/// Botão ⚙ do rodapé: trocar token, abrir ao iniciar sessão, sair.
struct SettingsMenu: View {
    let controller: ACController
    @State private var launchAtLogin = SMAppService.mainApp.status == .enabled

    var body: some View {
        Menu {
            Button("Trocar token…") { controller.resetCredentials() }
            Toggle("Abrir ao iniciar sessão", isOn: $launchAtLogin)
            Divider()
            Button("Sair") { NSApp.terminate(nil) }
        } label: {
            Image(systemName: "gearshape")
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
        .fixedSize()
        .onChange(of: launchAtLogin) { _, enabled in
            do {
                if enabled {
                    try SMAppService.mainApp.register()
                } else {
                    try SMAppService.mainApp.unregister()
                }
            } catch {
                // Fora do .app instalado (ex.: `swift run`) o registro falha; o toggle volta ao valor real.
                launchAtLogin = SMAppService.mainApp.status == .enabled
            }
        }
    }
}
