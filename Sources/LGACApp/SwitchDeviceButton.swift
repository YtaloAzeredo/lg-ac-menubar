import LGACCore
import SwiftUI

/// Seta de voltar no canto superior direito: abre a lista de ar-condicionados da conta.
struct SwitchDeviceButton: View {
    let controller: ACController

    var body: some View {
        Button {
            Task { await controller.chooseAnotherDevice() }
        } label: {
            Image(systemName: "chevron.backward")
                .font(.system(size: 12, weight: .semibold))
                .frame(width: 26, height: 26)
                .background(Circle().fill(Color.secondary.opacity(0.15)))
                .contentShape(Circle())
        }
        .buttonStyle(.plain)
        .help("Trocar aparelho")
        .accessibilityLabel("Trocar aparelho")
    }
}
