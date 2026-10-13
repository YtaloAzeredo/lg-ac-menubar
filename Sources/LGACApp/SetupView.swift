import AppKit
import LGACCore
import SwiftUI

/// Primeira configuração: token + país.
struct SetupView: View {
    let controller: ACController
    let message: String?
    @State private var token = ""
    @State private var country = "BR"

    private static let countries: [(code: String, name: String)] = {
        let ptBR = Locale(identifier: "pt_BR")
        return ThinQRegion.supportedCountries
            .map { (code: $0, name: ptBR.localizedString(forRegionCode: $0) ?? $0) }
            .sorted { $0.name.compare($1.name, locale: ptBR) == .orderedAscending }
    }()

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Label("Conectar ao LG ThinQ", systemImage: "snowflake").font(.headline)
            Text("Gere um Personal Access Token com permissão para ver e controlar aparelhos e cole abaixo.")
                .font(.caption)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            SecureField("Token", text: $token)
                .textFieldStyle(.roundedBorder)
            Picker("País", selection: $country) {
                ForEach(Self.countries, id: \.code) { Text($0.name).tag($0.code) }
            }
            if let message {
                Text(message).font(.caption).foregroundStyle(.red)
            }
            HStack {
                Link("Gerar token", destination: URL(string: "https://connect-pat.lgthinq.com")!)
                Spacer()
                Button("Conectar") { Task { await controller.connect(token: token, country: country) } }
                    .keyboardShortcut(.defaultAction)
                    .disabled(token.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
            Divider()
            Button("Sair") { NSApp.terminate(nil) }
                .buttonStyle(.borderless)
        }
        .padding(16)
    }
}

/// Mais de um ar-condicionado na conta: o usuário escolhe qual controlar.
struct DeviceChoiceView: View {
    let controller: ACController
    let devices: [ThinQDevice]

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text("Escolha o ar-condicionado").font(.headline)
                Spacer()
                SettingsMenu(controller: controller)
            }
            ForEach(devices) { device in
                Button {
                    Task { await controller.selectDevice(device) }
                } label: {
                    VStack(alignment: .leading) {
                        Text(device.alias.isEmpty ? device.modelName : device.alias)
                        Text(device.modelName).font(.caption).foregroundStyle(.secondary)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
            }
        }
        .padding(16)
    }
}
