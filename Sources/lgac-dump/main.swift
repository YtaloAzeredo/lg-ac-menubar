import Foundation
import LGACCore

// Ferramenta de desenvolvimento: salva devices/profile/state do primeiro ar-condicionado da conta.
// Uso: LGAC_TOKEN=... LGAC_COUNTRY=BR swift run lgac-dump <diretório>

func fail(_ message: String, code: Int32 = 1) -> Never {
    FileHandle.standardError.write(Data((message + "\n").utf8))
    exit(code)
}

func write(_ value: JSONValue, to directory: URL, name: String) throws {
    let encoder = JSONEncoder()
    encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
    try encoder.encode(value).write(to: directory.appendingPathComponent(name))
    print("✓ \(name)")
}

let environment = ProcessInfo.processInfo.environment
guard let token = environment["LGAC_TOKEN"], !token.isEmpty else {
    fail("Defina LGAC_TOKEN com o seu Personal Access Token da LG.")
}
let country = environment["LGAC_COUNTRY"] ?? "BR"
let directory = URL(fileURLWithPath: CommandLine.arguments.dropFirst().first ?? "dump", isDirectory: true)

do {
    let client = try ThinQClient(
        credentials: ThinQCredentials(token: token, country: country, clientId: "lgac-dump-\(UUID().uuidString.lowercased())"))
    try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)

    let devices = try await client.rawGET("devices")
    try write(devices, to: directory, name: "devices.json")

    guard let ac = devices.arrayValue?.compactMap(ThinQDevice.init(json:)).first(where: \.isAirConditioner) else {
        fail("Nenhum ar-condicionado encontrado nesta conta.", code: 2)
    }
    print("Ar-condicionado: \(ac.alias) (\(ac.modelName))")
    try write(try await client.profile(deviceId: ac.id), to: directory, name: "profile.json")
    try write(try await client.state(deviceId: ac.id), to: directory, name: "state.json")
} catch let error as ThinQError {
    fail("Erro da API: \(error.userMessage) (\(error))")
} catch {
    fail("Erro: \(error)")
}
