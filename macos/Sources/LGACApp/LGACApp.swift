import AppKit
import LGACCore
import SwiftUI

@main
struct LGACApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var appDelegate

    var body: some Scene {
        MenuBarExtra {
            RootView(controller: appDelegate.controller)
        } label: {
            MenuBarIcon(controller: appDelegate.controller)
        }
        .menuBarExtraStyle(.window)
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    let controller = ACController(store: KeychainCredentialStore())

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Sem ícone no Dock também com `swift run` (no .app, o Info.plist já define LSUIElement).
        NSApp.setActivationPolicy(.accessory)
        NSWorkspace.shared.notificationCenter.addObserver(
            forName: NSWorkspace.didWakeNotification, object: nil, queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let controller = self?.controller else { return }
                // Espera o Wi-Fi reconectar antes de consultar.
                Task {
                    try? await Task.sleep(for: .seconds(5))
                    await controller.refresh(force: true)
                }
            }
        }
        Task { await controller.bootstrap() }
    }
}
