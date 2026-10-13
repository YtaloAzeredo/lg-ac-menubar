# LG AC Menu Bar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** App nativo de barra de menu do macOS que controla um ar-condicionado LG pela API oficial LG ThinQ Connect.

**Architecture:** Pacote Swift com três alvos: `LGACCore` (cliente HTTP, modelos e um controlador `@Observable`, sem UI e coberto por testes), `LGACApp` (SwiftUI `MenuBarExtra` no estilo janela) e `lgac-dump` (ferramenta de desenvolvimento). A UI só lê o `ACController`; toda regra (capacidades do modelo, payloads, atualização otimista, debounce, backoff) fica no core.

**Tech Stack:** Swift 6.2 (modo de linguagem Swift 6, concorrência estrita), SwiftUI, Observation, Security (Keychain), ServiceManagement, Swift Testing. Sem dependências externas.

**Spec:** `docs/superpowers/specs/2026-10-13-lg-ac-menubar-design.md`

## Global Constraints

- `swift-tools-version: 6.0`, plataforma `.macOS(.v14)`; compila só com as Command Line Tools (sem Xcode).
- Nenhuma dependência externa.
- Build sem erros **e sem avisos** (`swift build` limpo).
- Testes com Swift Testing, sempre por `./scripts/test.sh` (com só as CLT, `swift test` puro falha com `no such module 'Testing'`).
- Os testes nunca acessam a rede nem a API real da LG.
- O token nunca é logado, impresso, commitado nem exibido depois de salvo.
- Textos da interface em português do Brasil.
- Bundle id `com.local.lgac`; service do Keychain `com.local.lgac`.
- Repositório público: nada de dados reais do aparelho (`deviceId`, apelido, modelo) em arquivos commitados; `dump/` está no `.gitignore`.
- Commits terminam com a linha `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

Condições que o spec implica, sem teste óbvio, e que mais podem incomodar quem usa o app. Cada uma tem um teste na task dona do código.

1. **Estado parcial ou vazio** (aparelho recém-religado responde sem alguns recursos): o app não quebra e os campos ficam vazios. Teste `ACStateTests.emptyStateHasNoValues` (Task 6).
2. **Clicar `+` no máximo da faixa**: nada muda e nenhum comando é enviado. Teste `ACControllerCommandTests.stepStopsAtTheLimit` (Task 10).
3. **Vários cliques rápidos e a LG recusa**: a tela volta ao valor de **antes do primeiro clique**, não a um valor intermediário. Teste `ACControllerCommandTests.rejectedBurstRollsBackToValueBeforeFirstClick` (Task 10).
4. **Token colado com espaço ou quebra de linha**: é aparado antes de ser usado e salvo. Teste `ACControllerSetupTests.connectTrimsTokenAndSavesAfterSuccess` (Task 9).
5. **Gateway devolve HTML em vez de JSON** (ex.: 502): vira erro tratável, sem crash. Teste `ThinQClientTests.nonJSONErrorBodyDoesNotCrash` (Task 3).

---

## Mapa de arquivos

| Arquivo | Responsabilidade | Task |
|---|---|---|
| `Package.swift` | Alvos e plataforma (evolui nas Tasks 1, 4, 5 e 11) | 1 |
| `scripts/test.sh` | Roda `swift test` com os flags que as CLT exigem | 1 |
| `Sources/LGACCore/JSONValue.swift` | JSON genérico (respostas e payloads) | 1 |
| `Sources/LGACCore/ThinQRegion.swift` | País → servidor | 2 |
| `Sources/LGACCore/ThinQError.swift` | Códigos de erro da LG → casos tratáveis + mensagens | 2 |
| `Sources/LGACCore/ThinQClient.swift` | HTTP: headers, endpoints, decodificação; `ThinQDevice`, `ThinQCredentials` | 3 |
| `Sources/lgac-dump/main.swift` | Ferramenta que salva devices/profile/state reais | 4 |
| `Tests/LGACCoreTests/Fixtures/*` | Profile e estado de exemplo (sem dados reais) | 5 |
| `Sources/LGACCore/ACCapabilities.swift` | O que o modelo permite controlar | 5 |
| `Sources/LGACCore/ACState.swift` | Estado atual do aparelho | 6 |
| `Sources/LGACCore/ACCommand.swift` | Ação → JSON do comando + atualização otimista | 7 |
| `Sources/LGACCore/CredentialStore.swift` | Token no Keychain, preferências no UserDefaults | 8 |
| `Sources/LGACCore/ACController.swift` | Configuração, estado, polling, erros | 9 |
| `Sources/LGACCore/ACController+Commands.swift` | Envio otimista, rollback, debounce | 10 |
| `Sources/LGACCore/ACLabels.swift` | Rótulos pt-BR e SF Symbols | 11 |
| `Sources/LGACApp/LGACApp.swift` | `@main`, `MenuBarExtra`, `AppDelegate` | 11 |
| `Sources/LGACApp/RootView.swift` | Escolhe a tela pela fase; `LoadingView` | 11 |
| `Sources/LGACApp/SetupView.swift` | Token + país; `DeviceChoiceView` | 11 |
| `Sources/LGACApp/MenuBarIcon.swift` | Ícone da barra (cheio/esmaecido) | 11 |
| `Sources/LGACApp/ControlPanelView.swift` | Painel de controles; `ChoiceGrid` | 12 |
| `Sources/LGACApp/SettingsMenu.swift` | Menu ⚙ (trocar token, abrir no login, sair) | 12 |
| `Resources/Info.plist`, `Resources/AppIcon.icns` | Metadados e ícone do `.app` | 13 |
| `scripts/make-icon.swift` | Gera o `AppIcon.icns` | 13 |
| `build-app.sh` | Compila, monta, assina e instala o `.app` | 13 |

Arquivos de apoio aos testes, em `Tests/LGACCoreTests/Support/`: `Fixture.swift` (Task 5), `InMemoryCredentialStore.swift` (Task 8) e `FakeClient.swift` (Task 9).

---

### Task 1: Pacote, script de testes e `JSONValue`

**Files:**
- Create: `Package.swift`
- Create: `scripts/test.sh`
- Create: `Sources/LGACCore/JSONValue.swift`
- Test: `Tests/LGACCoreTests/JSONValueTests.swift`

**Interfaces:**
- Consumes: nada.
- Produces: `public enum JSONValue: Sendable, Equatable, Codable` com casos `object([String: JSONValue])`, `array([JSONValue])`, `string(String)`, `number(Double)`, `bool(Bool)`, `null`; literais de string/inteiro/float/bool/array/dicionário; `subscript(key: String) -> JSONValue?`; `arrayValue`, `stringValue`, `doubleValue`, `intValue`, `boolValue` (todos opcionais). Script `./scripts/test.sh [args do swift test]`.

- [ ] **Step 1: Criar o `Package.swift`**

```swift
// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "LGACMenuBar",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "LGACCore"),
        .testTarget(name: "LGACCoreTests", dependencies: ["LGACCore"]),
    ]
)
```

- [ ] **Step 2: Criar `scripts/test.sh` e torná-lo executável**

As Command Line Tools trazem o `Testing.framework` fora do caminho padrão e o overlay `_Testing_Foundation` sem módulo. O script contorna as duas coisas só quando necessário.

```bash
#!/usr/bin/env bash
# Roda os testes. Só com as Command Line Tools (sem Xcode), o SwiftPM não acha o
# módulo Testing, e o overlay _Testing_Foundation vem sem módulo; contornamos os dois.
set -euo pipefail
cd "$(dirname "$0")/.."

flags=()
dev_dir="$(xcode-select -p 2>/dev/null || true)"
fw="$dev_dir/Library/Developer/Frameworks"
if [[ "$dev_dir" == *CommandLineTools* && -d "$fw" ]]; then
  flags=(
    -Xswiftc -F -Xswiftc "$fw"
    -Xswiftc -Xfrontend -Xswiftc -disable-cross-import-overlays
    -Xlinker -F -Xlinker "$fw"
    -Xlinker -rpath -Xlinker "$fw"
  )
fi

exec swift test ${flags[@]+"${flags[@]}"} "$@"
```

Run: `chmod +x scripts/test.sh`

- [ ] **Step 3: Escrever os testes**

```swift
import Foundation
import Testing
@testable import LGACCore

@Suite struct JSONValueTests {
    @Test func decodesMixedDocument() throws {
        let json = #"{"a": 1, "b": true, "c": "x", "d": [1.5, null], "e": {"f": false}}"#
        let value = try JSONDecoder().decode(JSONValue.self, from: Data(json.utf8))
        #expect(value == ["a": 1, "b": true, "c": "x", "d": [1.5, .null], "e": ["f": false]])
    }

    @Test func keepsBoolAndNumberApart() throws {
        let decoder = JSONDecoder()
        #expect(try decoder.decode(JSONValue.self, from: Data("1".utf8)) == .number(1))
        #expect(try decoder.decode(JSONValue.self, from: Data("0".utf8)) == .number(0))
        #expect(try decoder.decode(JSONValue.self, from: Data("true".utf8)) == .bool(true))
    }

    @Test func encodesWholeNumbersWithoutDecimals() throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = .sortedKeys
        let value: JSONValue = ["h": 2, "t": 23.5, "on": true]
        #expect(String(decoding: try encoder.encode(value), as: UTF8.self) == #"{"h":2,"on":true,"t":23.5}"#)
    }

    @Test func accessors() {
        let value: JSONValue = ["s": "x", "n": 3, "b": false, "a": [1], "o": ["k": "v"]]
        #expect(value["s"]?.stringValue == "x")
        #expect(value["n"]?.doubleValue == 3)
        #expect(value["n"]?.intValue == 3)
        #expect(value["b"]?.boolValue == false)
        #expect(value["a"]?.arrayValue == [1])
        #expect(value["o"]?["k"]?.stringValue == "v")
        #expect(value["missing"] == nil)
        #expect(value["s"]?.doubleValue == nil)
        #expect(JSONValue.string("x")["k"] == nil)
    }
}
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `./scripts/test.sh --filter JSONValueTests`
Expected: FAIL na compilação, com `cannot find type 'JSONValue' in scope` (o alvo `LGACCore` ainda não tem fontes; se o SwiftPM reclamar de alvo sem arquivos, é a mesma falha esperada).

- [ ] **Step 5: Implementar `JSONValue`**

A ordem de decodificação (`Bool` antes de `Double`) importa: o `JSONDecoder` não converte `1` em `Bool` nem `true` em `Double`, então os dois ficam separados.

```swift
import Foundation

/// Valor JSON arbitrário: respostas cruas da API (profile/state) e payloads de comando.
public enum JSONValue: Sendable, Equatable {
    case object([String: JSONValue])
    case array([JSONValue])
    case string(String)
    case number(Double)
    case bool(Bool)
    case null
}

extension JSONValue: Codable {
    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() {
            self = .null
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else if let value = try? container.decode(Double.self) {
            self = .number(value)
        } else if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode([JSONValue].self) {
            self = .array(value)
        } else if let value = try? container.decode([String: JSONValue].self) {
            self = .object(value)
        } else {
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Valor JSON inválido")
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .object(let value): try container.encode(value)
        case .array(let value): try container.encode(value)
        case .string(let value): try container.encode(value)
        case .number(let value): try container.encode(value)
        case .bool(let value): try container.encode(value)
        case .null: try container.encodeNil()
        }
    }
}

extension JSONValue: ExpressibleByStringLiteral, ExpressibleByIntegerLiteral, ExpressibleByFloatLiteral,
    ExpressibleByBooleanLiteral, ExpressibleByArrayLiteral, ExpressibleByDictionaryLiteral
{
    public init(stringLiteral value: String) { self = .string(value) }
    public init(integerLiteral value: Int) { self = .number(Double(value)) }
    public init(floatLiteral value: Double) { self = .number(value) }
    public init(booleanLiteral value: Bool) { self = .bool(value) }
    public init(arrayLiteral elements: JSONValue...) { self = .array(elements) }
    public init(dictionaryLiteral elements: (String, JSONValue)...) {
        self = .object(Dictionary(elements, uniquingKeysWith: { _, last in last }))
    }
}

extension JSONValue {
    public subscript(key: String) -> JSONValue? {
        if case .object(let object) = self { return object[key] }
        return nil
    }

    public var arrayValue: [JSONValue]? {
        if case .array(let value) = self { return value }
        return nil
    }

    public var stringValue: String? {
        if case .string(let value) = self { return value }
        return nil
    }

    public var doubleValue: Double? {
        if case .number(let value) = self { return value }
        return nil
    }

    public var intValue: Int? { doubleValue.map { Int($0) } }

    public var boolValue: Bool? {
        if case .bool(let value) = self { return value }
        return nil
    }
}
```

- [ ] **Step 6: Rodar e ver passar**

Run: `./scripts/test.sh --filter JSONValueTests`
Expected: PASS, 4 testes.

- [ ] **Step 7: Commit**

```bash
git add Package.swift scripts/test.sh Sources/LGACCore/JSONValue.swift Tests/LGACCoreTests/JSONValueTests.swift
git commit -m "feat: pacote Swift, script de testes e JSONValue" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `ThinQRegion` e `ThinQError`

**Files:**
- Create: `Sources/LGACCore/ThinQRegion.swift`
- Create: `Sources/LGACCore/ThinQError.swift`
- Test: `Tests/LGACCoreTests/ThinQRegionTests.swift`
- Test: `Tests/LGACCoreTests/ThinQErrorTests.swift`

**Interfaces:**
- Consumes: nada.
- Produces:
  - `public enum ThinQRegion: String { case kic, aic, eic }` com `static func region(forCountry: String) -> ThinQRegion?`, `static func baseURL(forCountry: String) -> URL?` e `static var supportedCountries: [String]` (ordenada).
  - `public enum ThinQError: Error, Equatable, Sendable` com `invalidToken`, `rateLimited`, `deviceOffline`, `commandRejected(code: String, message: String)`, `network(URLError.Code)`, `unexpected(code: String, message: String)`, `unsupportedCountry`; `static func from(code: String?, message: String?, httpStatus: Int) -> ThinQError`; `var userMessage: String`.

- [ ] **Step 1: Escrever os testes**

```swift
import Foundation
import Testing
@testable import LGACCore

@Suite struct ThinQRegionTests {
    @Test func brazilUsesAmericasServer() {
        #expect(ThinQRegion.region(forCountry: "BR") == .aic)
        #expect(ThinQRegion.baseURL(forCountry: "BR")?.absoluteString == "https://api-aic.lgthinq.com/")
    }

    @Test func otherRegions() {
        #expect(ThinQRegion.region(forCountry: "US") == .aic)
        #expect(ThinQRegion.region(forCountry: "DE") == .eic)
        #expect(ThinQRegion.region(forCountry: "KR") == .kic)
    }

    @Test func acceptsLowercase() {
        #expect(ThinQRegion.region(forCountry: "br") == .aic)
    }

    @Test func unknownCountry() {
        #expect(ThinQRegion.region(forCountry: "XX") == nil)
        #expect(ThinQRegion.baseURL(forCountry: "XX") == nil)
    }

    @Test func supportedCountriesAreSorted() {
        let countries = ThinQRegion.supportedCountries
        #expect(countries.contains("BR"))
        #expect(countries == countries.sorted())
    }
}
```

```swift
import Foundation
import Testing
@testable import LGACCore

@Suite struct ThinQErrorTests {
    @Test(arguments: ["1103", "1218", "1302"])
    func tokenCodes(code: String) {
        #expect(ThinQError.from(code: code, message: "x", httpStatus: 400) == .invalidToken)
    }

    @Test func http401WithoutBodyIsInvalidToken() {
        #expect(ThinQError.from(code: nil, message: nil, httpStatus: 401) == .invalidToken)
    }

    @Test func rateLimit() {
        #expect(ThinQError.from(code: "1306", message: "x", httpStatus: 429) == .rateLimited)
    }

    @Test func deviceOffline() {
        #expect(ThinQError.from(code: "1222", message: "x", httpStatus: 400) == .deviceOffline)
    }

    @Test(arguments: ["1102", "2205", "2207", "2208", "2301", "2302", "2303", "2304", "2305"])
    func rejectedCommands(code: String) {
        #expect(
            ThinQError.from(code: code, message: "não pode", httpStatus: 400)
                == .commandRejected(code: code, message: "não pode"))
    }

    @Test func unknownCode() {
        #expect(ThinQError.from(code: "9999", message: "m", httpStatus: 500) == .unexpected(code: "9999", message: "m"))
    }

    @Test func noBodyUsesHTTPStatus() {
        #expect(ThinQError.from(code: nil, message: nil, httpStatus: 502) == .unexpected(code: "HTTP 502", message: ""))
    }

    @Test func userMessages() {
        #expect(ThinQError.network(.notConnectedToInternet).userMessage == "Sem conexão")
        #expect(ThinQError.invalidToken.userMessage == "Token inválido, gere um novo")
        #expect(ThinQError.deviceOffline.userMessage == "Ar-condicionado offline")
        #expect(ThinQError.commandRejected(code: "2304", message: "").userMessage == "Comando recusado pelo aparelho")
        #expect(ThinQError.commandRejected(code: "2304", message: "Desligado").userMessage == "Comando recusado: Desligado")
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `./scripts/test.sh --filter "ThinQRegionTests|ThinQErrorTests"`
Expected: FAIL na compilação, com `cannot find 'ThinQRegion' in scope` e `cannot find 'ThinQError' in scope`.

- [ ] **Step 3: Implementar `ThinQRegion`**

Tabela copiada de `thinqconnect/country.py` (SDK oficial). Não remova países.

```swift
import Foundation

/// País → servidor da API ThinQ Connect. Tabela copiada de `thinqconnect/country.py` (SDK oficial, Apache-2.0).
public enum ThinQRegion: String, Sendable, CaseIterable {
    case kic, aic, eic

    public static func region(forCountry country: String) -> ThinQRegion? {
        let code = country.uppercased()
        return allCases.first { countries[$0]?.contains(code) ?? false }
    }

    public static func baseURL(forCountry country: String) -> URL? {
        region(forCountry: country).flatMap { URL(string: "https://api-\($0.rawValue).lgthinq.com/") }
    }

    public static var supportedCountries: [String] {
        countries.values.flatMap { $0 }.sorted()
    }

    static let countries: [ThinQRegion: Set<String>] = [
        .kic: [
            "AU", "BD", "CN", "HK", "ID", "IN", "JP", "KH", "KR", "LA", "LK", "MM", "MY", "NP", "NZ", "PH",
            "SG", "TH", "TW", "VN",
        ],
        .aic: [
            "AG", "AR", "AW", "BB", "BO", "BR", "BS", "BZ", "CA", "CL", "CO", "CR", "CU", "DM", "DO", "EC",
            "GD", "GT", "GY", "HN", "HT", "JM", "KN", "LC", "MX", "NI", "PA", "PE", "PR", "PY", "SR", "SV",
            "TT", "US", "UY", "VC", "VE",
        ],
        .eic: [
            "AE", "AF", "AL", "AM", "AO", "AT", "AZ", "BA", "BE", "BF", "BG", "BH", "BJ", "BY", "CD", "CF",
            "CG", "CH", "CI", "CM", "CV", "CY", "CZ", "DE", "DJ", "DK", "DZ", "EE", "EG", "ES", "ET", "FI",
            "FR", "GA", "GB", "GE", "GH", "GM", "GN", "GQ", "GR", "HR", "HU", "IE", "IL", "IQ", "IR", "IS",
            "IT", "JO", "KE", "KG", "KW", "KZ", "LB", "LR", "LT", "LU", "LV", "LY", "MA", "MD", "ME", "MK",
            "ML", "MR", "MT", "MU", "MW", "NE", "NG", "NL", "NO", "OM", "PK", "PL", "PS", "PT", "QA", "RO",
            "RS", "RU", "RW", "SA", "SD", "SE", "SI", "SK", "SL", "SN", "SO", "ST", "SY", "TD", "TG", "TN",
            "TR", "TZ", "UA", "UG", "UZ", "XK", "YE", "ZA", "ZM",
        ],
    ]
}
```

- [ ] **Step 4: Implementar `ThinQError`**

```swift
import Foundation

/// Erros da API ThinQ Connect, agrupados pelo que o app faz com cada um.
/// Códigos da tabela `ThinQAPIErrorCodes` do SDK oficial.
public enum ThinQError: Error, Equatable, Sendable {
    case invalidToken
    case rateLimited
    case deviceOffline
    case commandRejected(code: String, message: String)
    case network(URLError.Code)
    case unexpected(code: String, message: String)
    case unsupportedCountry

    /// Converte o corpo `{"error": {"code", "message"}}` (ou só o status HTTP) no caso certo.
    public static func from(code: String?, message: String?, httpStatus: Int) -> ThinQError {
        let message = message ?? ""
        guard let code else {
            return httpStatus == 401 ? .invalidToken : .unexpected(code: "HTTP \(httpStatus)", message: message)
        }
        switch code {
        case "1103", "1218", "1302":
            return .invalidToken
        case "1306":
            return .rateLimited
        case "1222":
            return .deviceOffline
        case "1102", "2205", "2207", "2208", "2301", "2302", "2303", "2304", "2305":
            return .commandRejected(code: code, message: message)
        default:
            return httpStatus == 401 ? .invalidToken : .unexpected(code: code, message: message)
        }
    }

    /// Texto curto em português para o rodapé do painel.
    public var userMessage: String {
        switch self {
        case .invalidToken:
            "Token inválido, gere um novo"
        case .rateLimited:
            "Limite de chamadas atingido, atualizando mais devagar"
        case .deviceOffline:
            "Ar-condicionado offline"
        case .commandRejected(_, let message):
            message.isEmpty ? "Comando recusado pelo aparelho" : "Comando recusado: \(message)"
        case .network:
            "Sem conexão"
        case .unexpected(let code, let message):
            message.isEmpty ? "Erro LG \(code)" : "Erro LG \(code): \(message)"
        case .unsupportedCountry:
            "País não suportado pela API da LG"
        }
    }
}
```

- [ ] **Step 5: Rodar e ver passar**

Run: `./scripts/test.sh --filter "ThinQRegionTests|ThinQErrorTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Sources/LGACCore/ThinQRegion.swift Sources/LGACCore/ThinQError.swift Tests/LGACCoreTests/ThinQRegionTests.swift Tests/LGACCoreTests/ThinQErrorTests.swift
git commit -m "feat: regiões da API e erros da LG" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `ThinQClient`

**Files:**
- Create: `Sources/LGACCore/ThinQClient.swift`
- Test: `Tests/LGACCoreTests/ThinQClientTests.swift`

**Interfaces:**
- Consumes: `JSONValue` (Task 1); `ThinQRegion.baseURL(forCountry:)`, `ThinQError.from(code:message:httpStatus:)` (Task 2).
- Produces:
  - `public struct ThinQDevice: Sendable, Equatable, Identifiable` com `id`, `type`, `alias`, `modelName`, `init(id:type:alias:modelName:)`, `init?(json: JSONValue)` e `var isAirConditioner: Bool`.
  - `public struct ThinQCredentials: Sendable, Equatable` com `token`, `country`, `clientId`.
  - `public protocol ThinQClientProtocol: Sendable` com `devices() async throws -> [ThinQDevice]`, `profile(deviceId:) async throws -> JSONValue`, `state(deviceId:) async throws -> JSONValue` e `control(deviceId:payload:) async throws`.
  - `public struct ThinQClient: ThinQClientProtocol` com `init(credentials:transport:) throws` (o `transport` padrão é `URLSession.shared.data(for:)`; lança `ThinQError.unsupportedCountry`), `rawGET(_ path: String) async throws -> JSONValue` e, internos, `static let apiKey` e `static func makeMessageId() -> String`.

O transporte HTTP é injetável. Os testes passam uma closure que grava a requisição e devolve uma resposta pronta, então nunca há rede.

- [ ] **Step 1: Escrever os testes**

```swift
import Foundation
import Testing
@testable import LGACCore

/// Guarda as requisições que o cliente tentou fazer.
actor RequestLog {
    private(set) var requests: [URLRequest] = []
    func append(_ request: URLRequest) { requests.append(request) }
}

private let credentials = ThinQCredentials(token: "tok", country: "BR", clientId: "lgac-test")

/// Cliente com transporte falso: nunca acessa a rede.
private func makeClient(status: Int = 200, body: String, log: RequestLog = RequestLog()) throws -> ThinQClient {
    try ThinQClient(credentials: credentials) { request in
        await log.append(request)
        let response = HTTPURLResponse(url: request.url!, statusCode: status, httpVersion: nil, headerFields: nil)!
        return (Data(body.utf8), response)
    }
}

@Suite struct ThinQClientTests {
    @Test func listsDevicesWithAllHeaders() async throws {
        let log = RequestLog()
        let body = """
            {"response": [
              {"deviceId": "ac-1", "deviceInfo": {"deviceType": "DEVICE_AIR_CONDITIONER", "modelName": "M1", "alias": "Ar do Quarto", "reportable": true}},
              {"deviceId": "w-1", "deviceInfo": {"deviceType": "DEVICE_WASHER", "modelName": "W1", "alias": "Lavadora"}}
            ]}
            """
        let devices = try await makeClient(body: body, log: log).devices()

        #expect(devices == [
            ThinQDevice(id: "ac-1", type: "DEVICE_AIR_CONDITIONER", alias: "Ar do Quarto", modelName: "M1"),
            ThinQDevice(id: "w-1", type: "DEVICE_WASHER", alias: "Lavadora", modelName: "W1"),
        ])
        #expect(devices.map(\.isAirConditioner) == [true, false])

        let request = try #require(await log.requests.first)
        #expect(request.httpMethod == "GET")
        #expect(request.url?.absoluteString == "https://api-aic.lgthinq.com/devices")
        #expect(request.value(forHTTPHeaderField: "Authorization") == "Bearer tok")
        #expect(request.value(forHTTPHeaderField: "x-country") == "BR")
        #expect(request.value(forHTTPHeaderField: "x-client-id") == "lgac-test")
        #expect(request.value(forHTTPHeaderField: "x-api-key") == ThinQClient.apiKey)
        #expect(request.value(forHTTPHeaderField: "x-service-phase") == "OP")
        #expect(request.value(forHTTPHeaderField: "x-message-id")?.count == 22)
        #expect(request.value(forHTTPHeaderField: "x-conditional-control") == nil)
    }

    @Test func messageIdsAreUnique() {
        #expect(ThinQClient.makeMessageId() != ThinQClient.makeMessageId())
    }

    @Test func profileAndStateReturnResponseContent() async throws {
        let log = RequestLog()
        let client = try makeClient(body: #"{"response": {"property": {"x": 1}}}"#, log: log)
        #expect(try await client.profile(deviceId: "ac-1") == ["property": ["x": 1]])
        _ = try await client.state(deviceId: "ac-1")
        let urls = await log.requests.map { $0.url?.absoluteString }
        #expect(urls == [
            "https://api-aic.lgthinq.com/devices/ac-1/profile",
            "https://api-aic.lgthinq.com/devices/ac-1/state",
        ])
    }

    @Test func controlPostsBodyWithConditionalHeader() async throws {
        let log = RequestLog()
        let payload: JSONValue = ["display": ["light": "OFF"]]
        try await makeClient(body: #"{"response": {}}"#, log: log).control(deviceId: "ac-1", payload: payload)

        let request = try #require(await log.requests.first)
        #expect(request.httpMethod == "POST")
        #expect(request.url?.absoluteString == "https://api-aic.lgthinq.com/devices/ac-1/control")
        #expect(request.value(forHTTPHeaderField: "x-conditional-control") == "true")
        let sent = try JSONDecoder().decode(JSONValue.self, from: try #require(request.httpBody))
        #expect(sent == payload)
    }

    @Test func errorBodyBecomesThinQError() async throws {
        let client = try makeClient(status: 400, body: #"{"error": {"code": "1306", "message": "limit"}}"#)
        await #expect(throws: ThinQError.rateLimited) { try await client.state(deviceId: "ac-1") }
    }

    @Test func numericErrorCodeIsAccepted() async throws {
        let client = try makeClient(status: 400, body: #"{"error": {"code": 1222, "message": "offline"}}"#)
        await #expect(throws: ThinQError.deviceOffline) { try await client.state(deviceId: "ac-1") }
    }

    @Test func nonJSONErrorBodyDoesNotCrash() async throws {
        let client = try makeClient(status: 502, body: "<html>Bad Gateway</html>")
        await #expect(throws: ThinQError.unexpected(code: "HTTP 502", message: "")) {
            try await client.state(deviceId: "ac-1")
        }
    }

    @Test func transportFailureBecomesNetworkError() async throws {
        let client = try ThinQClient(credentials: credentials) { _ in throw URLError(.notConnectedToInternet) }
        await #expect(throws: ThinQError.network(.notConnectedToInternet)) { try await client.devices() }
    }

    @Test func unsupportedCountryIsRejected() {
        #expect(throws: ThinQError.unsupportedCountry) {
            try ThinQClient(credentials: ThinQCredentials(token: "t", country: "XX", clientId: "c"))
        }
    }

    @Test func serverFollowsCountry() async throws {
        let log = RequestLog()
        let client = try ThinQClient(credentials: ThinQCredentials(token: "t", country: "DE", clientId: "c")) { request in
            await log.append(request)
            return (Data(#"{"response": []}"#.utf8), HTTPURLResponse(url: request.url!, statusCode: 200, httpVersion: nil, headerFields: nil)!)
        }
        _ = try await client.devices()
        #expect(await log.requests.first?.url?.host == "api-eic.lgthinq.com")
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `./scripts/test.sh --filter ThinQClientTests`
Expected: FAIL na compilação, com `cannot find 'ThinQClient' in scope`.

- [ ] **Step 3: Implementar o cliente**

O `x-api-key` é a constante pública publicada no SDK oficial (`thinqconnect/const.py`), igual para todos os clientes. Não é segredo do usuário.

```swift
import Foundation

/// Um aparelho da conta LG (item de `GET /devices`).
public struct ThinQDevice: Sendable, Equatable, Identifiable {
    public let id: String
    public let type: String
    public let alias: String
    public let modelName: String

    public init(id: String, type: String, alias: String, modelName: String) {
        self.id = id
        self.type = type
        self.alias = alias
        self.modelName = modelName
    }

    public init?(json: JSONValue) {
        guard let id = json["deviceId"]?.stringValue else { return nil }
        let info = json["deviceInfo"]
        self.init(
            id: id,
            type: info?["deviceType"]?.stringValue ?? "",
            alias: info?["alias"]?.stringValue ?? "",
            modelName: info?["modelName"]?.stringValue ?? "")
    }

    public var isAirConditioner: Bool { type == "DEVICE_AIR_CONDITIONER" }
}

public struct ThinQCredentials: Sendable, Equatable {
    public let token: String
    public let country: String
    public let clientId: String

    public init(token: String, country: String, clientId: String) {
        self.token = token
        self.country = country
        self.clientId = clientId
    }
}

public protocol ThinQClientProtocol: Sendable {
    func devices() async throws -> [ThinQDevice]
    func profile(deviceId: String) async throws -> JSONValue
    func state(deviceId: String) async throws -> JSONValue
    func control(deviceId: String, payload: JSONValue) async throws
}

/// Cliente HTTP da API LG ThinQ Connect.
public struct ThinQClient: ThinQClientProtocol {
    public typealias Transport = @Sendable (URLRequest) async throws -> (Data, URLResponse)

    /// Constante pública do SDK oficial (`thinqconnect/const.py`), a mesma para todos os clientes.
    static let apiKey = "v6GFvkweNo7DK7yD3ylIZ9w52aKBU0eJ7wLXkSR3"

    let credentials: ThinQCredentials
    let baseURL: URL
    let transport: Transport

    public init(
        credentials: ThinQCredentials,
        transport: @escaping Transport = { try await URLSession.shared.data(for: $0) }
    ) throws {
        guard let baseURL = ThinQRegion.baseURL(forCountry: credentials.country) else {
            throw ThinQError.unsupportedCountry
        }
        self.credentials = credentials
        self.baseURL = baseURL
        self.transport = transport
    }

    public func devices() async throws -> [ThinQDevice] {
        try await rawGET("devices").arrayValue?.compactMap(ThinQDevice.init(json:)) ?? []
    }

    public func profile(deviceId: String) async throws -> JSONValue {
        try await rawGET("devices/\(deviceId)/profile")
    }

    public func state(deviceId: String) async throws -> JSONValue {
        try await rawGET("devices/\(deviceId)/state")
    }

    public func control(deviceId: String, payload: JSONValue) async throws {
        _ = try await send("POST", "devices/\(deviceId)/control", body: payload)
    }

    /// GET cru, sem interpretar a resposta. Usado também pela ferramenta `lgac-dump`.
    public func rawGET(_ path: String) async throws -> JSONValue {
        try await send("GET", path)
    }

    func send(_ method: String, _ path: String, body: JSONValue? = nil) async throws -> JSONValue {
        let request = try makeRequest(method, path, body: body)
        let data: Data
        let response: URLResponse
        do {
            (data, response) = try await transport(request)
        } catch let error as URLError {
            throw ThinQError.network(error.code)
        }
        let status = (response as? HTTPURLResponse)?.statusCode ?? 0
        let json = try? JSONDecoder().decode(JSONValue.self, from: data)
        guard (200..<300).contains(status) else {
            let error = json?["error"]
            let code = error?["code"].flatMap { $0.stringValue ?? $0.intValue.map(String.init) }
            throw ThinQError.from(code: code, message: error?["message"]?.stringValue, httpStatus: status)
        }
        return json?["response"] ?? .null
    }

    func makeRequest(_ method: String, _ path: String, body: JSONValue? = nil) throws -> URLRequest {
        var request = URLRequest(url: baseURL.appendingPathComponent(path))
        request.httpMethod = method
        request.timeoutInterval = 15
        request.setValue("Bearer \(credentials.token)", forHTTPHeaderField: "Authorization")
        request.setValue(credentials.country.uppercased(), forHTTPHeaderField: "x-country")
        request.setValue(Self.makeMessageId(), forHTTPHeaderField: "x-message-id")
        request.setValue(credentials.clientId, forHTTPHeaderField: "x-client-id")
        request.setValue(Self.apiKey, forHTTPHeaderField: "x-api-key")
        request.setValue("OP", forHTTPHeaderField: "x-service-phase")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if let body {
            // Só o POST de controle tem corpo, e é ele que leva este header.
            request.setValue("true", forHTTPHeaderField: "x-conditional-control")
            request.httpBody = try JSONEncoder().encode(body)
        }
        return request
    }

    /// UUID em base64url sem padding (22 caracteres), como o SDK oficial.
    static func makeMessageId() -> String {
        let bytes = withUnsafeBytes(of: UUID().uuid) { Data($0) }
        return bytes.base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `./scripts/test.sh --filter ThinQClientTests`
Expected: PASS, 10 testes.

- [ ] **Step 5: Commit**

```bash
git add Sources/LGACCore/ThinQClient.swift Tests/LGACCoreTests/ThinQClientTests.swift
git commit -m "feat: cliente HTTP da API LG ThinQ Connect" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `lgac-dump` e Passo 0 (dados reais do aparelho)

**Files:**
- Modify: `Package.swift` (acrescenta o alvo `lgac-dump`)
- Create: `Sources/lgac-dump/main.swift`

**Interfaces:**
- Consumes: `ThinQClient`, `ThinQCredentials`, `ThinQDevice.init?(json:)`, `rawGET`, `ThinQError.userMessage` (Tasks 2–3).
- Produces: executável `lgac-dump <diretório>` que lê `LGAC_TOKEN` e `LGAC_COUNTRY` (padrão `BR`) e grava `devices.json`, `profile.json` e `state.json`.

Esta task tem um **checkpoint humano**: só a pessoa dona do aparelho tem o token. Quem executa o plano não pede, não recebe e não lê o token.

- [ ] **Step 1: Atualizar o `Package.swift`**

```swift
// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "LGACMenuBar",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "LGACCore"),
        .executableTarget(name: "lgac-dump", dependencies: ["LGACCore"]),
        .testTarget(name: "LGACCoreTests", dependencies: ["LGACCore"]),
    ]
)
```

- [ ] **Step 2: Criar a ferramenta**

```swift
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
```

- [ ] **Step 3: Compilar**

Run: `swift build --product lgac-dump`
Expected: `Build of product 'lgac-dump' complete!`, sem avisos.

- [ ] **Step 4: Conferir o erro sem token**

Run: `env -u LGAC_TOKEN swift run lgac-dump /tmp/lgac-dump-test; echo "exit=$?"`
Expected: `Defina LGAC_TOKEN com o seu Personal Access Token da LG.` e `exit=1`.

- [ ] **Step 5: Commit**

```bash
git add Package.swift Sources/lgac-dump/main.swift
git commit -m "feat: ferramenta lgac-dump para inspecionar o aparelho real" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: CHECKPOINT HUMANO — rodar o dump com o token real**

Pare e peça à pessoa que rode, **no Terminal dela** (o `read -s` não funciona pelo prefixo `!` do Claude Code), dentro da pasta do repositório:

```bash
read -s "LGAC_TOKEN?Token LG: " && export LGAC_TOKEN
swift run lgac-dump dump
unset LGAC_TOKEN
```

Expected: `✓ devices.json`, uma linha `Ar-condicionado: <apelido> (<modelo>)`, `✓ profile.json` e `✓ state.json`. Se aparecer erro da API, pare e relate a mensagem.

- [ ] **Step 7: Comparar `dump/profile.json` e `dump/state.json` com as premissas do spec**

Leia os dois arquivos (estão no `.gitignore`; não os copie para o repositório) e confira cada item:

| Premissa (usada nas Tasks 5–7) | Onde olhar no `profile.json` |
|---|---|
| Power em `operation.airConOperationMode`, valores `POWER_ON`/`POWER_OFF` | `property.operation` |
| Modos em `airConJobMode.currentJobMode` | `property.airConJobMode` |
| Temperatura alvo gravável em um dos 4 candidatos da seção 2 do spec, tipo `range` com `min`/`max`/`step` | `property.temperatureInUnits` (lista) e `property.temperature` |
| Ventilador em `airFlow.windStrength` ou `airFlow.windStrengthDetail` | `property.airFlow` |
| Swing em `windDirection.rotateUpDown` / `rotateLeftRight`, tipo `boolean` | `property.windDirection` |
| Timer em `timer.relativeHourToStop` (ou `sleepTimer`) | `property.timer` / `property.sleepTimer` |
| Luz do visor em `display.light`, enum `ON`/`OFF` ou boolean | `property.display` |
| Estado: `temperatureInUnits` é lista com item `"unit": "C"`; timer ativo tem `relativeStopTimer: "SET"` | `state.json` |

Se **tudo bate**, siga para a Task 5. Se **algo diverge**, pare e relate à pessoa a diferença exata (chave e valor esperados × encontrados) antes de continuar: o ajuste muda as Tasks 5–7. Não inclua no relato o `deviceId` nem outros dados pessoais.

---

### Task 5: Fixtures e `ACCapabilities`

**Files:**
- Modify: `Package.swift` (o alvo de testes passa a copiar `Fixtures`)
- Create: `Tests/LGACCoreTests/Fixtures/profile-full.json`
- Create: `Tests/LGACCoreTests/Fixtures/state-full.json`
- Create: `Tests/LGACCoreTests/Fixtures/profile-basic.json`
- Create: `Tests/LGACCoreTests/Fixtures/state-basic.json`
- Create: `Tests/LGACCoreTests/Fixtures/README.md`
- Create: `Tests/LGACCoreTests/Support/Fixture.swift`
- Create: `Sources/LGACCore/ACCapabilities.swift`
- Test: `Tests/LGACCoreTests/ACCapabilitiesTests.swift`

**Interfaces:**
- Consumes: `JSONValue` (Task 1).
- Produces:
  - `public struct ACCapabilities: Sendable, Equatable` com `init(profile: JSONValue)` e as propriedades `power: Bool`, `jobModes: [String]`, `targetTemp: TemperatureControl?`, `windStrengths: [String]`, `windStrengthKey: String`, `swingUpDown: Bool`, `swingLeftRight: Bool`, `offTimer: OffTimerKind?`, `displayLight: DisplayLightKind?`.
  - `ACCapabilities.TemperatureControl` (`resource`, `key`, `min`, `max`, `step`, `func clamp(_ value: Double) -> Double`).
  - `ACCapabilities.OffTimerKind: String` (`timer`, `sleepTimer`) e `ACCapabilities.DisplayLightKind` (`onOff`, `boolean`).
  - `static let supportedJobModes`.
  - Internos: `static func celsiusEntry(_ resource: JSONValue?) -> JSONValue?`, que também é usado pelo `ACState`.
  - Testes: `Fixture.json(_ name: String) throws -> JSONValue` e `Fixture.capabilities(_ name: String) throws -> ACCapabilities`.

- [ ] **Step 1: Atualizar o `Package.swift`**

```swift
// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "LGACMenuBar",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "LGACCore"),
        .executableTarget(name: "lgac-dump", dependencies: ["LGACCore"]),
        .testTarget(
            name: "LGACCoreTests",
            dependencies: ["LGACCore"],
            resources: [.copy("Fixtures")]
        ),
    ]
)
```

- [ ] **Step 2: Criar as fixtures**

Nenhuma delas vem de um aparelho real. As `full` seguem a estrutura das fixtures de ar-condicionado do Home Assistant (Apache-2.0), com acréscimos. Se a Task 4 revelou um formato diferente, ajuste estas fixtures para refletir o formato real (sem copiar ids ou nomes).

`Tests/LGACCoreTests/Fixtures/profile-full.json`:

```json
{
  "notification": { "push": ["WATER_IS_FULL"] },
  "property": {
    "airConJobMode": {
      "currentJobMode": {
        "mode": ["r", "w"],
        "type": "enum",
        "value": {
          "r": ["AIR_CLEAN", "COOL", "HEAT", "AIR_DRY", "FAN", "AUTO"],
          "w": ["AIR_CLEAN", "COOL", "HEAT", "AIR_DRY", "FAN", "AUTO"]
        }
      }
    },
    "airFlow": {
      "windStrength": {
        "mode": ["r", "w"],
        "type": "enum",
        "value": { "r": ["LOW", "MID", "HIGH", "AUTO"], "w": ["LOW", "MID", "HIGH", "AUTO"] }
      }
    },
    "display": {
      "light": {
        "mode": ["r", "w"],
        "type": "enum",
        "value": { "r": ["ON", "OFF"], "w": ["ON", "OFF"] }
      }
    },
    "operation": {
      "airCleanOperationMode": { "mode": ["w"], "type": "enum", "value": { "w": ["START", "STOP"] } },
      "airConOperationMode": {
        "mode": ["r", "w"],
        "type": "enum",
        "value": { "r": ["POWER_ON", "POWER_OFF"], "w": ["POWER_ON", "POWER_OFF"] }
      }
    },
    "powerSave": {
      "powerSaveEnabled": { "mode": ["r", "w"], "type": "boolean", "value": { "r": [false, true], "w": [false, true] } }
    },
    "temperature": {
      "coolTargetTemperature": { "mode": ["w"], "type": "range", "value": { "w": { "max": 30, "min": 18, "step": 1 } } },
      "currentTemperature": { "mode": ["r"], "type": "number" },
      "targetTemperature": {
        "mode": ["r", "w"],
        "type": "range",
        "value": { "r": { "max": 30, "min": 18, "step": 1 }, "w": { "max": 30, "min": 18, "step": 1 } }
      },
      "unit": { "mode": ["r"], "type": "enum", "value": { "r": ["C", "F"] } }
    },
    "temperatureInUnits": [
      {
        "currentTemperature": { "type": "number", "mode": ["r"] },
        "targetTemperature": { "type": "number", "mode": ["r"] },
        "coolTargetTemperature": { "type": "range", "mode": ["w"], "value": { "w": { "max": 30, "min": 18, "step": 1 } } },
        "unit": "C"
      },
      {
        "currentTemperature": { "type": "number", "mode": ["r"] },
        "targetTemperature": { "type": "number", "mode": ["r"] },
        "coolTargetTemperature": { "type": "range", "mode": ["w"], "value": { "w": { "max": 86, "min": 64, "step": 2 } } },
        "unit": "F"
      }
    ],
    "timer": {
      "relativeHourToStart": { "mode": ["r", "w"], "type": "number" },
      "relativeHourToStop": { "mode": ["r", "w"], "type": "number" },
      "relativeMinuteToStart": { "mode": ["r", "w"], "type": "number" },
      "relativeMinuteToStop": { "mode": ["r", "w"], "type": "number" }
    },
    "windDirection": {
      "rotateUpDown": { "type": "boolean", "mode": ["r", "w"], "value": { "r": [true, false], "w": [true, false] } },
      "rotateLeftRight": { "type": "boolean", "mode": ["r", "w"], "value": { "r": [true, false], "w": [true, false] } }
    }
  }
}
```

`Tests/LGACCoreTests/Fixtures/state-full.json`:

```json
{
  "airConJobMode": { "currentJobMode": "COOL" },
  "airFlow": { "windStrength": "MID" },
  "display": { "light": "ON" },
  "operation": { "airConOperationMode": "POWER_ON" },
  "powerSave": { "powerSaveEnabled": false },
  "sleepTimer": { "relativeStopTimer": "UNSET" },
  "temperature": { "currentTemperature": 25, "targetTemperature": 23, "unit": "C" },
  "temperatureInUnits": [
    { "currentTemperature": 25, "targetTemperature": 23, "unit": "C" },
    { "currentTemperature": 77, "targetTemperature": 74, "unit": "F" }
  ],
  "timer": {
    "relativeStartTimer": "UNSET",
    "relativeStopTimer": "SET",
    "relativeHourToStop": 1,
    "relativeMinuteToStop": 20
  },
  "windDirection": { "rotateUpDown": false, "rotateLeftRight": true }
}
```

`Tests/LGACCoreTests/Fixtures/profile-basic.json`:

```json
{
  "property": {
    "airConJobMode": {
      "currentJobMode": { "mode": ["r", "w"], "type": "enum", "value": { "r": ["COOL", "FAN"], "w": ["COOL", "FAN"] } }
    },
    "airFlow": {
      "windStrengthDetail": { "mode": ["r", "w"], "type": "enum", "value": { "r": ["LOW", "HIGH"], "w": ["LOW", "HIGH"] } }
    },
    "operation": {
      "airConOperationMode": {
        "mode": ["r", "w"],
        "type": "enum",
        "value": { "r": ["POWER_ON", "POWER_OFF"], "w": ["POWER_ON", "POWER_OFF"] }
      }
    },
    "temperatureInUnits": [
      {
        "currentTemperature": { "type": "number", "mode": ["r"] },
        "targetTemperature": { "type": "range", "mode": ["r", "w"], "value": { "w": { "max": 30, "min": 16, "step": 0.5 } } },
        "unit": "C"
      }
    ],
    "windDirection": {
      "rotateUpDown": { "type": "boolean", "mode": ["r", "w"], "value": { "r": [true, false], "w": [true, false] } }
    }
  }
}
```

`Tests/LGACCoreTests/Fixtures/state-basic.json`:

```json
{
  "airConJobMode": { "currentJobMode": "FAN" },
  "airFlow": { "windStrengthDetail": "HIGH" },
  "operation": { "airConOperationMode": "POWER_OFF" },
  "temperatureInUnits": [ { "currentTemperature": 27.5, "targetTemperature": 22.5, "unit": "C" } ],
  "windDirection": { "rotateUpDown": true }
}
```

`Tests/LGACCoreTests/Fixtures/README.md`:

```markdown
# Fixtures

JSON de exemplo da API LG ThinQ Connect usados nos testes. Nenhum arquivo aqui contém dados de um aparelho real.

- `profile-full.json` / `state-full.json` — modelo com todos os recursos que o app usa. Estrutura baseada nas fixtures de ar-condicionado da integração `lg_thinq` do Home Assistant (Apache-2.0), com luz do visor, timer ativo e modos extras acrescentados.
- `profile-basic.json` / `state-basic.json` — modelo fictício "básico": sem swing horizontal, sem luz do visor, sem timer, ventilador em `windStrengthDetail`, temperatura com passo de 0,5°.

Para conferir o formato do seu aparelho, use `swift run lgac-dump ./dump` (veja o README principal). Não copie arquivos de `dump/` para cá sem remover `deviceId`, apelido e modelo.
```

- [ ] **Step 3: Criar o carregador de fixtures**

`Tests/LGACCoreTests/Support/Fixture.swift`:

```swift
import Foundation
@testable import LGACCore

enum Fixture {
    struct Missing: Error { let name: String }

    static func json(_ name: String) throws -> JSONValue {
        guard let url = Bundle.module.url(forResource: name, withExtension: "json", subdirectory: "Fixtures") else {
            throw Missing(name: name)
        }
        return try JSONDecoder().decode(JSONValue.self, from: Data(contentsOf: url))
    }

    static func capabilities(_ name: String) throws -> ACCapabilities {
        ACCapabilities(profile: try json(name))
    }
}
```

- [ ] **Step 4: Escrever os testes**

```swift
import Foundation
import Testing
@testable import LGACCore

@Suite struct ACCapabilitiesTests {
    @Test func fullProfile() throws {
        let caps = try Fixture.capabilities("profile-full")
        #expect(caps.power)
        // AIR_CLEAN fica de fora: o app só exibe os cinco modos suportados.
        #expect(caps.jobModes == ["COOL", "HEAT", "AIR_DRY", "FAN", "AUTO"])
        // targetTemperature em temperatureInUnits é só leitura; o gravável é o de `temperature`.
        #expect(caps.targetTemp == .init(resource: "temperature", key: "targetTemperature", min: 18, max: 30, step: 1))
        #expect(caps.windStrengths == ["LOW", "MID", "HIGH", "AUTO"])
        #expect(caps.windStrengthKey == "windStrength")
        #expect(caps.swingUpDown)
        #expect(caps.swingLeftRight)
        #expect(caps.offTimer == .timer)
        #expect(caps.displayLight == .onOff)
    }

    @Test func basicProfile() throws {
        let caps = try Fixture.capabilities("profile-basic")
        #expect(caps.power)
        #expect(caps.jobModes == ["COOL", "FAN"])
        #expect(caps.targetTemp == .init(resource: "temperatureInUnits", key: "targetTemperature", min: 16, max: 30, step: 0.5))
        #expect(caps.windStrengthKey == "windStrengthDetail")
        #expect(caps.windStrengths == ["LOW", "HIGH"])
        #expect(caps.swingUpDown)
        #expect(!caps.swingLeftRight)
        #expect(caps.offTimer == nil)
        #expect(caps.displayLight == nil)
    }

    @Test func coolTargetIsLastResort() {
        let profile: JSONValue = [
            "property": [
                "temperatureInUnits": [
                    [
                        "unit": "C",
                        "targetTemperature": ["type": "number", "mode": ["r"]],
                        "coolTargetTemperature": [
                            "type": "range", "mode": ["w"], "value": ["w": ["min": 18, "max": 30, "step": 1]],
                        ],
                    ]
                ]
            ]
        ]
        #expect(
            ACCapabilities(profile: profile).targetTemp
                == .init(resource: "temperatureInUnits", key: "coolTargetTemperature", min: 18, max: 30, step: 1))
    }

    @Test func sleepTimerFallback() {
        let profile: JSONValue = [
            "property": ["sleepTimer": ["relativeHourToStop": ["type": "number", "mode": ["r", "w"]]]]
        ]
        #expect(ACCapabilities(profile: profile).offTimer == .sleepTimer)
    }

    @Test func booleanDisplayLight() {
        let profile: JSONValue = [
            "property": ["display": ["light": ["type": "boolean", "mode": ["r", "w"], "value": ["w": [true, false]]]]]
        ]
        #expect(ACCapabilities(profile: profile).displayLight == .boolean)
    }

    @Test func readOnlyControlsAreHidden() {
        let profile: JSONValue = [
            "property": ["windDirection": ["rotateUpDown": ["type": "boolean", "mode": ["r"]]]]
        ]
        #expect(!ACCapabilities(profile: profile).swingUpDown)
    }

    @Test func emptyProfile() {
        let caps = ACCapabilities(profile: [:])
        #expect(!caps.power)
        #expect(caps.jobModes.isEmpty)
        #expect(caps.targetTemp == nil)
        #expect(caps.windStrengths.isEmpty)
        #expect(caps.offTimer == nil)
        #expect(caps.displayLight == nil)
    }

    @Test func clampRoundsToStepAndRange() {
        let temp = ACCapabilities.TemperatureControl(resource: "temperature", key: "targetTemperature", min: 16, max: 30, step: 0.5)
        #expect(temp.clamp(31) == 30)
        #expect(temp.clamp(15) == 16)
        #expect(temp.clamp(23.3) == 23.5)
        #expect(temp.clamp(23.2) == 23)
    }
}
```

- [ ] **Step 5: Rodar e ver falhar**

Run: `./scripts/test.sh --filter ACCapabilitiesTests`
Expected: FAIL na compilação, com `cannot find 'ACCapabilities' in scope`.

- [ ] **Step 6: Implementar `ACCapabilities`**

```swift
import Foundation

/// O que o modelo do aparelho permite controlar, lido do profile (`GET /devices/{id}/profile`).
/// Controle ausente ou só de leitura fica `nil`/`false`/vazio e não aparece na interface.
public struct ACCapabilities: Sendable, Equatable {
    public struct TemperatureControl: Sendable, Equatable {
        /// `temperatureInUnits` ou `temperature`.
        public let resource: String
        /// `targetTemperature` ou `coolTargetTemperature`.
        public let key: String
        public let min: Double
        public let max: Double
        public let step: Double

        /// Arredonda para o passo do modelo e limita à faixa permitida.
        public func clamp(_ value: Double) -> Double {
            let stepped = (value / step).rounded() * step
            return Swift.min(max, Swift.max(min, stepped))
        }
    }

    public enum OffTimerKind: String, Sendable {
        case timer
        case sleepTimer
    }

    public enum DisplayLightKind: Sendable {
        /// Valores `"ON"` / `"OFF"`.
        case onOff
        /// Valores `true` / `false`.
        case boolean
    }

    /// Modos que o app sabe exibir, na ordem do painel.
    public static let supportedJobModes = ["COOL", "HEAT", "AIR_DRY", "FAN", "AUTO"]

    public let power: Bool
    public let jobModes: [String]
    public let targetTemp: TemperatureControl?
    public let windStrengths: [String]
    public let windStrengthKey: String
    public let swingUpDown: Bool
    public let swingLeftRight: Bool
    public let offTimer: OffTimerKind?
    public let displayLight: DisplayLightKind?

    public init(profile: JSONValue) {
        let property = profile["property"] ?? [:]

        power = Self.writableEnum(property["operation"]?["airConOperationMode"]).contains("POWER_ON")

        let modes = Self.writableEnum(property["airConJobMode"]?["currentJobMode"])
        jobModes = Self.supportedJobModes.filter(modes.contains)

        targetTemp = Self.temperatureControl(in: property)

        if let detail = property["airFlow"]?["windStrengthDetail"], Self.isWritable(detail) {
            windStrengthKey = "windStrengthDetail"
            windStrengths = Self.writableEnum(detail)
        } else {
            windStrengthKey = "windStrength"
            windStrengths = Self.writableEnum(property["airFlow"]?["windStrength"])
        }

        swingUpDown = Self.isWritable(property["windDirection"]?["rotateUpDown"])
        swingLeftRight = Self.isWritable(property["windDirection"]?["rotateLeftRight"])

        if Self.isWritable(property["timer"]?["relativeHourToStop"]) {
            offTimer = .timer
        } else if Self.isWritable(property["sleepTimer"]?["relativeHourToStop"]) {
            offTimer = .sleepTimer
        } else {
            offTimer = nil
        }

        let light = property["display"]?["light"]
        if Self.isWritable(light) {
            displayLight = light?["type"]?.stringValue == "boolean" ? .boolean : .onOff
        } else {
            displayLight = nil
        }
    }

    static func isWritable(_ entry: JSONValue?) -> Bool {
        entry?["mode"]?.arrayValue?.contains("w") ?? false
    }

    static func writableEnum(_ entry: JSONValue?) -> [String] {
        guard isWritable(entry) else { return [] }
        return entry?["value"]?["w"]?.arrayValue?.compactMap(\.stringValue) ?? []
    }

    /// Item `"unit": "C"` de `temperatureInUnits` (é uma lista, no profile e no estado).
    static func celsiusEntry(_ resource: JSONValue?) -> JSONValue? {
        resource?.arrayValue?.first { $0["unit"]?.stringValue == "C" }
    }

    /// Primeiro candidato gravável do tipo `range`, na ordem definida no spec.
    static func temperatureControl(in property: JSONValue) -> TemperatureControl? {
        let inUnits = celsiusEntry(property["temperatureInUnits"])
        let legacy = property["temperature"]
        let candidates: [(resource: String, key: String, entry: JSONValue?)] = [
            ("temperatureInUnits", "targetTemperature", inUnits?["targetTemperature"]),
            ("temperature", "targetTemperature", legacy?["targetTemperature"]),
            ("temperatureInUnits", "coolTargetTemperature", inUnits?["coolTargetTemperature"]),
            ("temperature", "coolTargetTemperature", legacy?["coolTargetTemperature"]),
        ]
        for candidate in candidates {
            guard isWritable(candidate.entry), let range = candidate.entry?["value"]?["w"],
                let min = range["min"]?.doubleValue, let max = range["max"]?.doubleValue
            else { continue }
            let step = range["step"]?.doubleValue ?? 1
            return TemperatureControl(
                resource: candidate.resource, key: candidate.key, min: min, max: max, step: step > 0 ? step : 1)
        }
        return nil
    }
}
```

- [ ] **Step 7: Rodar e ver passar**

Run: `./scripts/test.sh --filter ACCapabilitiesTests`
Expected: PASS, 8 testes.

- [ ] **Step 8: Commit**

```bash
git add Package.swift Tests/LGACCoreTests/Fixtures Tests/LGACCoreTests/Support/Fixture.swift Sources/LGACCore/ACCapabilities.swift Tests/LGACCoreTests/ACCapabilitiesTests.swift
git commit -m "feat: capacidades do aparelho lidas do profile" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `ACState`

**Files:**
- Create: `Sources/LGACCore/ACState.swift`
- Test: `Tests/LGACCoreTests/ACStateTests.swift`

**Interfaces:**
- Consumes: `JSONValue` (Task 1); `ACCapabilities.celsiusEntry` (Task 5); `Fixture` (Task 5).
- Produces: `public struct ACState: Sendable, Equatable` com as propriedades `var` opcionais `isOn: Bool?`, `jobMode: String?`, `targetTemp: Double?`, `currentTemp: Double?`, `windStrength: String?`, `swingUpDown: Bool?`, `swingLeftRight: Bool?`, `displayLightOn: Bool?` e `offTimerRemainingMinutes: Int?`. Também `init(isOn:jobMode:targetTemp:currentTemp:windStrength:swingUpDown:swingLeftRight:displayLightOn:offTimerRemainingMinutes:)`, com todos os parâmetros com padrão `nil`, e `init(state: JSONValue)`.

- [ ] **Step 1: Escrever os testes**

`emptyStateHasNoValues` cobre o item 1 do Review Focus.

```swift
import Foundation
import Testing
@testable import LGACCore

@Suite struct ACStateTests {
    @Test func fullState() throws {
        let state = ACState(state: try Fixture.json("state-full"))
        #expect(state == ACState(
            isOn: true, jobMode: "COOL", targetTemp: 23, currentTemp: 25, windStrength: "MID",
            swingUpDown: false, swingLeftRight: true, displayLightOn: true, offTimerRemainingMinutes: 80))
    }

    @Test func basicState() throws {
        let state = ACState(state: try Fixture.json("state-basic"))
        #expect(state == ACState(
            isOn: false, jobMode: "FAN", targetTemp: 22.5, currentTemp: 27.5, windStrength: "HIGH",
            swingUpDown: true))
    }

    @Test func emptyStateHasNoValues() {
        #expect(ACState(state: [:]) == ACState())
    }

    @Test func legacyTemperatureResource() {
        let state = ACState(state: ["temperature": ["currentTemperature": 26, "targetTemperature": 21, "unit": "C"]])
        #expect(state.targetTemp == 21)
        #expect(state.currentTemp == 26)
    }

    @Test func unsetTimerIsIgnored() {
        let state = ACState(state: ["timer": ["relativeStopTimer": "UNSET", "relativeHourToStop": 2]])
        #expect(state.offTimerRemainingMinutes == nil)
    }

    @Test func sleepTimerRemaining() {
        let state = ACState(state: [
            "sleepTimer": ["relativeStopTimer": "SET", "relativeHourToStop": 0, "relativeMinuteToStop": 30]
        ])
        #expect(state.offTimerRemainingMinutes == 30)
    }

    @Test func booleanLight() {
        #expect(ACState(state: ["display": ["light": false]]).displayLightOn == false)
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `./scripts/test.sh --filter ACStateTests`
Expected: FAIL na compilação, com `cannot find 'ACState' in scope`.

- [ ] **Step 3: Implementar `ACState`**

```swift
import Foundation

/// Estado atual do aparelho (`GET /devices/{id}/state`). Campo ausente na resposta fica `nil`.
public struct ACState: Sendable, Equatable {
    public var isOn: Bool?
    public var jobMode: String?
    public var targetTemp: Double?
    public var currentTemp: Double?
    public var windStrength: String?
    public var swingUpDown: Bool?
    public var swingLeftRight: Bool?
    public var displayLightOn: Bool?
    public var offTimerRemainingMinutes: Int?

    public init(
        isOn: Bool? = nil, jobMode: String? = nil, targetTemp: Double? = nil, currentTemp: Double? = nil,
        windStrength: String? = nil, swingUpDown: Bool? = nil, swingLeftRight: Bool? = nil,
        displayLightOn: Bool? = nil, offTimerRemainingMinutes: Int? = nil
    ) {
        self.isOn = isOn
        self.jobMode = jobMode
        self.targetTemp = targetTemp
        self.currentTemp = currentTemp
        self.windStrength = windStrength
        self.swingUpDown = swingUpDown
        self.swingLeftRight = swingLeftRight
        self.displayLightOn = displayLightOn
        self.offTimerRemainingMinutes = offTimerRemainingMinutes
    }

    public init(state: JSONValue) {
        let celsius = ACCapabilities.celsiusEntry(state["temperatureInUnits"])
        let legacy = state["temperature"]
        self.init(
            isOn: state["operation"]?["airConOperationMode"]?.stringValue.map { $0 == "POWER_ON" },
            jobMode: state["airConJobMode"]?["currentJobMode"]?.stringValue,
            targetTemp: celsius?["targetTemperature"]?.doubleValue ?? legacy?["targetTemperature"]?.doubleValue,
            currentTemp: celsius?["currentTemperature"]?.doubleValue ?? legacy?["currentTemperature"]?.doubleValue,
            windStrength: state["airFlow"]?["windStrengthDetail"]?.stringValue
                ?? state["airFlow"]?["windStrength"]?.stringValue,
            swingUpDown: state["windDirection"]?["rotateUpDown"]?.boolValue,
            swingLeftRight: state["windDirection"]?["rotateLeftRight"]?.boolValue,
            displayLightOn: Self.lightIsOn(state["display"]?["light"]),
            offTimerRemainingMinutes: Self.remainingMinutes(state["timer"]) ?? Self.remainingMinutes(state["sleepTimer"]))
    }

    static func lightIsOn(_ value: JSONValue?) -> Bool? {
        switch value {
        case .bool(let on)?: return on
        case .string(let text)?: return text == "ON"
        default: return nil
        }
    }

    /// Tempo restante até desligar, ou `nil` se o timer não está ativo.
    static func remainingMinutes(_ timer: JSONValue?) -> Int? {
        guard let timer, timer["relativeStopTimer"]?.stringValue != "UNSET" else { return nil }
        let total = (timer["relativeHourToStop"]?.intValue ?? 0) * 60 + (timer["relativeMinuteToStop"]?.intValue ?? 0)
        return total > 0 ? total : nil
    }
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `./scripts/test.sh --filter ACStateTests`
Expected: PASS, 7 testes.

- [ ] **Step 5: Commit**

```bash
git add Sources/LGACCore/ACState.swift Tests/LGACCoreTests/ACStateTests.swift
git commit -m "feat: estado do aparelho" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `ACCommand`

**Files:**
- Create: `Sources/LGACCore/ACCommand.swift`
- Test: `Tests/LGACCoreTests/ACCommandTests.swift`

**Interfaces:**
- Consumes: `JSONValue`, `ACCapabilities`, `ACState`, `Fixture` (Tasks 1, 5 e 6).
- Produces: `public enum ACCommand: Sendable, Equatable` com os casos `power(Bool)`, `jobMode(String)`, `targetTemp(Double)`, `windStrength(String)`, `swingUpDown(Bool)`, `swingLeftRight(Bool)`, `offTimer(minutes: Int?)` (`nil` ou `0` cancela) e `displayLight(Bool)`. Métodos: `func payload(using: ACCapabilities) -> JSONValue?` e `func apply(to: ACState) -> ACState`.

- [ ] **Step 1: Escrever os testes**

```swift
import Foundation
import Testing
@testable import LGACCore

@Suite struct ACCommandTests {
    let full: ACCapabilities
    let basic: ACCapabilities

    init() throws {
        full = try Fixture.capabilities("profile-full")
        basic = try Fixture.capabilities("profile-basic")
    }

    @Test func power() {
        #expect(ACCommand.power(true).payload(using: full) == ["operation": ["airConOperationMode": "POWER_ON"]])
        #expect(ACCommand.power(false).payload(using: full) == ["operation": ["airConOperationMode": "POWER_OFF"]])
    }

    @Test func jobMode() {
        #expect(ACCommand.jobMode("HEAT").payload(using: full) == ["airConJobMode": ["currentJobMode": "HEAT"]])
        #expect(ACCommand.jobMode("HEAT").payload(using: basic) == nil)
    }

    @Test func targetTempOnLegacyResource() {
        #expect(ACCommand.targetTemp(24).payload(using: full) == ["temperature": ["targetTemperature": 24]])
    }

    @Test func targetTempInUnitsIsClampedAndCarriesUnit() {
        #expect(ACCommand.targetTemp(22.5).payload(using: basic)
            == ["temperatureInUnits": ["targetTemperature": 22.5, "unit": "C"]])
        #expect(ACCommand.targetTemp(40).payload(using: basic)
            == ["temperatureInUnits": ["targetTemperature": 30, "unit": "C"]])
    }

    @Test func windStrengthUsesProfileKey() {
        #expect(ACCommand.windStrength("HIGH").payload(using: full) == ["airFlow": ["windStrength": "HIGH"]])
        #expect(ACCommand.windStrength("HIGH").payload(using: basic) == ["airFlow": ["windStrengthDetail": "HIGH"]])
        #expect(ACCommand.windStrength("MID").payload(using: basic) == nil)
    }

    @Test func swing() {
        #expect(ACCommand.swingUpDown(true).payload(using: full) == ["windDirection": ["rotateUpDown": true]])
        #expect(ACCommand.swingLeftRight(false).payload(using: full) == ["windDirection": ["rotateLeftRight": false]])
        #expect(ACCommand.swingLeftRight(true).payload(using: basic) == nil)
    }

    @Test func offTimer() {
        #expect(ACCommand.offTimer(minutes: 120).payload(using: full) == ["timer": ["relativeHourToStop": 2]])
        #expect(ACCommand.offTimer(minutes: 90).payload(using: full)
            == ["timer": ["relativeHourToStop": 1, "relativeMinuteToStop": 30]])
        #expect(ACCommand.offTimer(minutes: 30).payload(using: full)
            == ["timer": ["relativeHourToStop": 0, "relativeMinuteToStop": 30]])
        #expect(ACCommand.offTimer(minutes: nil).payload(using: full) == ["timer": ["relativeStopTimer": "UNSET"]])
        #expect(ACCommand.offTimer(minutes: 60).payload(using: basic) == nil)
    }

    @Test func offTimerOnSleepTimerModels() {
        let caps = ACCapabilities(profile: [
            "property": ["sleepTimer": ["relativeHourToStop": ["type": "number", "mode": ["r", "w"]]]]
        ])
        #expect(ACCommand.offTimer(minutes: 60).payload(using: caps) == ["sleepTimer": ["relativeHourToStop": 1]])
    }

    @Test func displayLight() {
        #expect(ACCommand.displayLight(false).payload(using: full) == ["display": ["light": "OFF"]])
        #expect(ACCommand.displayLight(true).payload(using: basic) == nil)
        let booleanCaps = ACCapabilities(profile: [
            "property": ["display": ["light": ["type": "boolean", "mode": ["r", "w"]]]]
        ])
        #expect(ACCommand.displayLight(true).payload(using: booleanCaps) == ["display": ["light": true]])
    }

    @Test func applyUpdatesOnlyTheTouchedField() {
        let before = ACState(isOn: true, jobMode: "COOL", targetTemp: 23, offTimerRemainingMinutes: 45)
        #expect(ACCommand.power(false).apply(to: before).isOn == false)
        #expect(ACCommand.jobMode("FAN").apply(to: before) == ACState(isOn: true, jobMode: "FAN", targetTemp: 23, offTimerRemainingMinutes: 45))
        #expect(ACCommand.targetTemp(25).apply(to: before).targetTemp == 25)
        #expect(ACCommand.offTimer(minutes: nil).apply(to: before).offTimerRemainingMinutes == nil)
        #expect(ACCommand.offTimer(minutes: 120).apply(to: before).offTimerRemainingMinutes == 120)
        #expect(ACCommand.displayLight(false).apply(to: before).displayLightOn == false)
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `./scripts/test.sh --filter ACCommandTests`
Expected: FAIL na compilação, com `cannot find 'ACCommand' in scope`.

- [ ] **Step 3: Implementar `ACCommand`**

```swift
import Foundation

/// Uma ação do usuário no painel.
public enum ACCommand: Sendable, Equatable {
    case power(Bool)
    case jobMode(String)
    case targetTemp(Double)
    case windStrength(String)
    case swingUpDown(Bool)
    case swingLeftRight(Bool)
    /// `nil` ou `0` cancela o timer.
    case offTimer(minutes: Int?)
    case displayLight(Bool)

    /// Corpo do `POST /control`, ou `nil` se o modelo não suporta o comando.
    public func payload(using caps: ACCapabilities) -> JSONValue? {
        switch self {
        case .power(let on):
            guard caps.power else { return nil }
            return ["operation": ["airConOperationMode": .string(on ? "POWER_ON" : "POWER_OFF")]]

        case .jobMode(let mode):
            guard caps.jobModes.contains(mode) else { return nil }
            return ["airConJobMode": ["currentJobMode": .string(mode)]]

        case .targetTemp(let value):
            guard let temp = caps.targetTemp else { return nil }
            var body: [String: JSONValue] = [temp.key: .number(temp.clamp(value))]
            if temp.resource == "temperatureInUnits" { body["unit"] = "C" }
            return [temp.resource: .object(body)]

        case .windStrength(let value):
            guard caps.windStrengths.contains(value) else { return nil }
            return ["airFlow": [caps.windStrengthKey: .string(value)]]

        case .swingUpDown(let on):
            guard caps.swingUpDown else { return nil }
            return ["windDirection": ["rotateUpDown": .bool(on)]]

        case .swingLeftRight(let on):
            guard caps.swingLeftRight else { return nil }
            return ["windDirection": ["rotateLeftRight": .bool(on)]]

        case .offTimer(let minutes):
            guard let kind = caps.offTimer else { return nil }
            guard let minutes, minutes > 0 else {
                return [kind.rawValue: ["relativeStopTimer": "UNSET"]]
            }
            var body: [String: JSONValue] = ["relativeHourToStop": .number(Double(minutes / 60))]
            if minutes % 60 != 0 { body["relativeMinuteToStop"] = .number(Double(minutes % 60)) }
            return [kind.rawValue: .object(body)]

        case .displayLight(let on):
            guard let kind = caps.displayLight else { return nil }
            let value: JSONValue = kind == .boolean ? .bool(on) : .string(on ? "ON" : "OFF")
            return ["display": ["light": value]]
        }
    }

    /// Estado previsto depois do comando, para a interface responder na hora (atualização otimista).
    public func apply(to state: ACState) -> ACState {
        var next = state
        switch self {
        case .power(let on): next.isOn = on
        case .jobMode(let mode): next.jobMode = mode
        case .targetTemp(let value): next.targetTemp = value
        case .windStrength(let value): next.windStrength = value
        case .swingUpDown(let on): next.swingUpDown = on
        case .swingLeftRight(let on): next.swingLeftRight = on
        case .offTimer(let minutes): next.offTimerRemainingMinutes = (minutes ?? 0) > 0 ? minutes : nil
        case .displayLight(let on): next.displayLightOn = on
        }
        return next
    }
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `./scripts/test.sh --filter ACCommandTests`
Expected: PASS, 10 testes.

- [ ] **Step 5: Commit**

```bash
git add Sources/LGACCore/ACCommand.swift Tests/LGACCoreTests/ACCommandTests.swift
git commit -m "feat: comandos e payloads de controle" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `CredentialStore`

**Files:**
- Create: `Sources/LGACCore/CredentialStore.swift`
- Create: `Tests/LGACCoreTests/Support/InMemoryCredentialStore.swift`
- Test: `Tests/LGACCoreTests/CredentialStoreTests.swift`

**Interfaces:**
- Consumes: nada.
- Produces:
  - `public protocol CredentialStoring: AnyObject` com `var token: String? { get }`, `func saveToken(_:) throws`, `func deleteToken()`, `var country: String { get set }` (padrão `"BR"`), `var deviceId: String? { get set }`, `var deviceAlias: String? { get set }` e `var clientId: String { get }`.
  - `public enum CredentialStoreError: Error, Equatable { case keychain(OSStatus) }`.
  - `public final class KeychainCredentialStore: CredentialStoring` com `init(service: String = "com.local.lgac", defaults: UserDefaults = .standard)`.
  - Testes: `InMemoryCredentialStore` com `saveError: Error?` e `static func configured()` (token `"tok"`, deviceId `"ac-1"`, alias `"Ar do Quarto"`, clientId `"lgac-test-client"`).

- [ ] **Step 1: Escrever os testes do Keychain**

Usam um service e uma suíte de `UserDefaults` exclusivos, apagados ao fim, então não tocam nas credenciais reais do app.

```swift
import Foundation
import Testing
@testable import LGACCore

/// Usa um service e uma suíte de UserDefaults exclusivos, apagados no fim de cada teste.
@Suite(.serialized) struct KeychainCredentialStoreTests {
    let suiteName = "lgac-tests-\(UUID().uuidString)"

    private func withStore(_ body: (KeychainCredentialStore) throws -> Void) rethrows {
        let defaults = UserDefaults(suiteName: suiteName)!
        let store = KeychainCredentialStore(service: "com.local.lgac.tests.\(UUID().uuidString)", defaults: defaults)
        defer {
            store.deleteToken()
            defaults.removePersistentDomain(forName: suiteName)
        }
        try body(store)
    }

    @Test func tokenRoundTrip() throws {
        try withStore { store in
            #expect(store.token == nil)
            try store.saveToken("primeiro")
            try store.saveToken("segundo")
            #expect(store.token == "segundo")
            store.deleteToken()
            #expect(store.token == nil)
        }
    }

    @Test func preferences() {
        withStore { store in
            #expect(store.country == "BR")
            store.country = "US"
            store.deviceId = "ac-1"
            store.deviceAlias = "Sala"
            #expect(store.country == "US")
            #expect(store.deviceId == "ac-1")
            #expect(store.deviceAlias == "Sala")
        }
    }

    @Test func clientIdIsStable() {
        withStore { store in
            let first = store.clientId
            #expect(first.hasPrefix("lgac-"))
            #expect(store.clientId == first)
        }
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `./scripts/test.sh --filter KeychainCredentialStoreTests`
Expected: FAIL na compilação, com `cannot find 'KeychainCredentialStore' in scope`.

- [ ] **Step 3: Implementar o store**

```swift
import Foundation
import Security

/// Onde o app guarda o token e as preferências.
public protocol CredentialStoring: AnyObject {
    var token: String? { get }
    func saveToken(_ token: String) throws
    func deleteToken()
    var country: String { get set }
    var deviceId: String? { get set }
    var deviceAlias: String? { get set }
    /// Gerado uma vez e reaproveitado (header `x-client-id`).
    var clientId: String { get }
}

public enum CredentialStoreError: Error, Equatable {
    case keychain(OSStatus)
}

/// Token no Keychain do macOS; o resto em `UserDefaults`.
public final class KeychainCredentialStore: CredentialStoring {
    private let service: String
    private let defaults: UserDefaults

    public init(service: String = "com.local.lgac", defaults: UserDefaults = .standard) {
        self.service = service
        self.defaults = defaults
    }

    public var token: String? {
        var query = baseQuery
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne
        var result: AnyObject?
        guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess,
            let data = result as? Data
        else { return nil }
        return String(data: data, encoding: .utf8)
    }

    public func saveToken(_ token: String) throws {
        deleteToken()
        var query = baseQuery
        query[kSecValueData as String] = Data(token.utf8)
        let status = SecItemAdd(query as CFDictionary, nil)
        guard status == errSecSuccess else { throw CredentialStoreError.keychain(status) }
    }

    public func deleteToken() {
        SecItemDelete(baseQuery as CFDictionary)
    }

    public var country: String {
        get { defaults.string(forKey: Keys.country) ?? "BR" }
        set { defaults.set(newValue, forKey: Keys.country) }
    }

    public var deviceId: String? {
        get { defaults.string(forKey: Keys.deviceId) }
        set { defaults.set(newValue, forKey: Keys.deviceId) }
    }

    public var deviceAlias: String? {
        get { defaults.string(forKey: Keys.deviceAlias) }
        set { defaults.set(newValue, forKey: Keys.deviceAlias) }
    }

    public var clientId: String {
        if let existing = defaults.string(forKey: Keys.clientId) { return existing }
        let created = "lgac-\(UUID().uuidString.lowercased())"
        defaults.set(created, forKey: Keys.clientId)
        return created
    }

    private var baseQuery: [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: "personal-access-token",
        ]
    }

    private enum Keys {
        static let country = "country"
        static let deviceId = "deviceId"
        static let deviceAlias = "deviceAlias"
        static let clientId = "clientId"
    }
}
```

- [ ] **Step 4: Criar o store em memória usado pelos testes do controlador**

`Tests/LGACCoreTests/Support/InMemoryCredentialStore.swift`:

```swift
import Foundation
@testable import LGACCore

final class InMemoryCredentialStore: CredentialStoring {
    var token: String?
    var country = "BR"
    var deviceId: String?
    var deviceAlias: String?
    let clientId = "lgac-test-client"
    var saveError: Error?

    func saveToken(_ token: String) throws {
        if let saveError { throw saveError }
        self.token = token
    }

    func deleteToken() { token = nil }

    /// Store de quem já configurou o app.
    static func configured() -> InMemoryCredentialStore {
        let store = InMemoryCredentialStore()
        store.token = "tok"
        store.deviceId = "ac-1"
        store.deviceAlias = "Ar do Quarto"
        return store
    }
}
```

- [ ] **Step 5: Rodar e ver passar**

Run: `./scripts/test.sh --filter KeychainCredentialStoreTests`
Expected: PASS, 3 testes. Se o macOS mostrar um diálogo pedindo acesso ao Keychain, é a primeira escrita do executor de testes; permita e rode de novo.

- [ ] **Step 6: Commit**

```bash
git add Sources/LGACCore/CredentialStore.swift Tests/LGACCoreTests/Support/InMemoryCredentialStore.swift Tests/LGACCoreTests/CredentialStoreTests.swift
git commit -m "feat: token no Keychain e preferências no UserDefaults" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `ACController`: configuração, estado e erros

**Files:**
- Create: `Sources/LGACCore/ACController.swift`
- Create: `Tests/LGACCoreTests/Support/FakeClient.swift`
- Test: `Tests/LGACCoreTests/ACControllerSetupTests.swift`
- Test: `Tests/LGACCoreTests/ACControllerRefreshTests.swift`

**Interfaces:**
- Consumes: `ThinQClientProtocol`, `ThinQClient`, `ThinQCredentials`, `ThinQDevice`, `ThinQError` (Tasks 2–3); `ACCapabilities` (Task 5); `ACState` (Task 6); `CredentialStoring` (Task 8); `InMemoryCredentialStore`, `Fixture` (testes).
- Produces: `@MainActor @Observable public final class ACController` com:
  - `init(store:makeClient:sleep:now:)`, onde os três últimos têm padrão de produção.
  - Fase: `enum Phase: Equatable { case needsSetup(message: String?), loading, choosingDevice([ThinQDevice]), ready }`.
  - Propriedades observáveis (só leitura de fora): `phase`, `capabilities`, `state`, `deviceAlias`, `lastUpdated`, `statusMessage`, `isDeviceOffline`, `pollInterval`.
  - Métodos: `bootstrap() async`, `connect(token:country:) async`, `selectDevice(_:) async`, `resetCredentials()`, `refresh(force: Bool = false) async`, `startPolling()`, `stopPolling()`.
  - Constantes: `basePollInterval` (300 s), `maxPollInterval` (1800 s); internas `freshness` (5 s), `confirmDelay` (1,5 s), `tempDebounce` (0,6 s).
  - Internos usados pela Task 10: `store`, `client`, `sleep`, `confirmTask`, `tempTask`, `pendingTargetTemp`, `tempBaseline` e `handle(_:)`.
  - Testes: `FakeClient` (actor), `testSleep`, `TestNow`, `CredentialsLog` e `makeController(fake:store:clock:log:)`.

O `sleep` injetado deixa os testes determinísticos. Nos testes, `testSleep` libera na hora os atrasos curtos (debounce, confirmação) e segura o intervalo de polling até a tarefa ser cancelada, para o polling não girar em loop.

- [ ] **Step 1: Criar o cliente falso e os utilitários de teste**

`Tests/LGACCoreTests/Support/FakeClient.swift`:

```swift
import Foundation
@testable import LGACCore

/// Cliente falso para os testes do controlador: respostas configuráveis e registro dos comandos.
actor FakeClient: ThinQClientProtocol {
    private var devicesResult: Result<[ThinQDevice], ThinQError>
    private var profileResult: Result<JSONValue, ThinQError>
    private var stateResult: Result<JSONValue, ThinQError>
    private var controlError: ThinQError?
    private(set) var controls: [JSONValue] = []
    private(set) var stateCalls = 0

    init(profile: JSONValue, state: JSONValue, devices: [ThinQDevice] = []) {
        devicesResult = .success(devices)
        profileResult = .success(profile)
        stateResult = .success(state)
    }

    /// Aparelho do fixture "full" ligado em COOL, 23 °C.
    static func full() throws -> FakeClient {
        FakeClient(profile: try Fixture.json("profile-full"), state: try Fixture.json("state-full"))
    }

    func devices() async throws -> [ThinQDevice] { try devicesResult.get() }
    func profile(deviceId: String) async throws -> JSONValue { try profileResult.get() }

    func state(deviceId: String) async throws -> JSONValue {
        stateCalls += 1
        return try stateResult.get()
    }

    func control(deviceId: String, payload: JSONValue) async throws {
        controls.append(payload)
        if let controlError { throw controlError }
    }

    func setDevices(_ result: Result<[ThinQDevice], ThinQError>) { devicesResult = result }
    func setProfile(_ result: Result<JSONValue, ThinQError>) { profileResult = result }
    func setState(_ result: Result<JSONValue, ThinQError>) { stateResult = result }
    func setControlError(_ error: ThinQError?) { controlError = error }
}

/// Espera falsa: atrasos curtos (debounce, confirmação) passam na hora;
/// o intervalo de polling fica suspenso até a tarefa ser cancelada.
let testSleep: @Sendable (Duration) async throws -> Void = { duration in
    try Task.checkCancellation()
    if duration >= ACController.basePollInterval {
        try await Task.sleep(for: .seconds(3600))
    }
    await Task.yield()
}

/// Relógio controlável para testar a regra dos 5 s.
final class TestNow: @unchecked Sendable {
    var date = Date(timeIntervalSince1970: 1_000_000)
}

/// Guarda as credenciais que o controlador usou para criar clientes.
final class CredentialsLog: @unchecked Sendable {
    var all: [ThinQCredentials] = []
}

@MainActor
func makeController(
    fake: FakeClient,
    store: InMemoryCredentialStore = .configured(),
    clock: TestNow = TestNow(),
    log: CredentialsLog = CredentialsLog()
) -> ACController {
    ACController(
        store: store,
        makeClient: { credentials in
            log.all.append(credentials)
            return fake
        },
        sleep: testSleep,
        now: { clock.date })
}
```

- [ ] **Step 2: Escrever os testes de configuração**

`connectTrimsTokenAndSavesAfterSuccess` cobre o item 4 do Review Focus.

```swift
import Foundation
import Testing
@testable import LGACCore

private let ac1 = ThinQDevice(id: "ac-1", type: "DEVICE_AIR_CONDITIONER", alias: "Quarto", modelName: "M1")
private let ac2 = ThinQDevice(id: "ac-2", type: "DEVICE_AIR_CONDITIONER", alias: "Sala", modelName: "M2")
private let washer = ThinQDevice(id: "w-1", type: "DEVICE_WASHER", alias: "Lavadora", modelName: "W1")

@MainActor @Suite struct ACControllerSetupTests {
    @Test func withoutTokenNeedsSetup() async throws {
        let controller = makeController(fake: try .full(), store: InMemoryCredentialStore())
        await controller.bootstrap()
        #expect(controller.phase == .needsSetup(message: nil))
    }

    @Test func bootstrapLoadsCapabilitiesAndState() async throws {
        let controller = makeController(fake: try .full())
        await controller.bootstrap()
        #expect(controller.phase == .ready)
        #expect(controller.capabilities?.power == true)
        #expect(controller.state?.targetTemp == 23)
        #expect(controller.deviceAlias == "Ar do Quarto")
        #expect(controller.lastUpdated != nil)
    }

    @Test func connectTrimsTokenAndSavesAfterSuccess() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.success([ac1, washer]))
        let store = InMemoryCredentialStore()
        let log = CredentialsLog()
        let controller = makeController(fake: fake, store: store, log: log)

        await controller.connect(token: "  tok-123\n", country: "BR")

        #expect(log.all.first == ThinQCredentials(token: "tok-123", country: "BR", clientId: "lgac-test-client"))
        #expect(store.token == "tok-123")
        #expect(store.deviceId == "ac-1")
        #expect(store.deviceAlias == "Quarto")
        #expect(controller.phase == .ready)
    }

    @Test func blankTokenIsRejected() async throws {
        let store = InMemoryCredentialStore()
        let controller = makeController(fake: try .full(), store: store)
        await controller.connect(token: "   ", country: "BR")
        #expect(controller.phase == .needsSetup(message: "Cole o token"))
        #expect(store.token == nil)
    }

    @Test func invalidTokenIsNotSaved() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.failure(.invalidToken))
        let store = InMemoryCredentialStore()
        let controller = makeController(fake: fake, store: store)

        await controller.connect(token: "ruim", country: "BR")

        #expect(store.token == nil)
        #expect(controller.phase == .needsSetup(message: "Token inválido, gere um novo"))
    }

    @Test func accountWithoutAirConditioner() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.success([washer]))
        let controller = makeController(fake: fake, store: InMemoryCredentialStore())
        await controller.connect(token: "tok", country: "BR")
        #expect(controller.phase == .needsSetup(message: "Nenhum ar-condicionado encontrado nesta conta"))
    }

    @Test func twoAirConditionersAskWhichOne() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.success([ac1, ac2]))
        let store = InMemoryCredentialStore()
        let controller = makeController(fake: fake, store: store)

        await controller.connect(token: "tok", country: "BR")
        #expect(controller.phase == .choosingDevice([ac1, ac2]))
        #expect(store.token == nil)

        await controller.selectDevice(ac2)
        #expect(store.token == "tok")
        #expect(store.deviceId == "ac-2")
        #expect(controller.deviceAlias == "Sala")
        #expect(controller.phase == .ready)
    }

    @Test func keychainFailureIsReported() async throws {
        let fake = try FakeClient.full()
        await fake.setDevices(.success([ac1]))
        let store = InMemoryCredentialStore()
        store.saveError = CredentialStoreError.keychain(-25300)
        let controller = makeController(fake: fake, store: store)

        await controller.connect(token: "tok", country: "BR")

        #expect(controller.phase == .needsSetup(message: "Não foi possível salvar o token no Keychain"))
    }

    @Test func resetCredentialsReturnsToSetup() async throws {
        let store = InMemoryCredentialStore.configured()
        let controller = makeController(fake: try .full(), store: store)
        await controller.bootstrap()

        controller.resetCredentials()

        #expect(controller.phase == .needsSetup(message: nil))
        #expect(store.token == nil)
        #expect(controller.capabilities == nil)
        #expect(controller.state == nil)
    }
}
```

- [ ] **Step 3: Escrever os testes de atualização e erros**

```swift
import Foundation
import Testing
@testable import LGACCore

@MainActor @Suite struct ACControllerRefreshTests {
    @Test func networkErrorKeepsLastState() async throws {
        let fake = try FakeClient.full()
        let controller = makeController(fake: fake)
        await controller.bootstrap()

        await fake.setState(.failure(.network(.notConnectedToInternet)))
        await controller.refresh(force: true)

        #expect(controller.state?.targetTemp == 23)
        #expect(controller.statusMessage == "Sem conexão")
        #expect(controller.phase == .ready)
    }

    @Test func noNetworkAtStartupStaysLoadingThenRecovers() async throws {
        let fake = try FakeClient.full()
        await fake.setProfile(.failure(.network(.notConnectedToInternet)))
        let controller = makeController(fake: fake)

        await controller.bootstrap()
        #expect(controller.phase == .loading)
        #expect(controller.statusMessage == "Sem conexão")

        await fake.setProfile(.success(try Fixture.json("profile-full")))
        await controller.refresh(force: true)
        #expect(controller.phase == .ready)
        #expect(controller.statusMessage == nil)
    }

    @Test func invalidTokenDuringRefreshReturnsToSetup() async throws {
        let fake = try FakeClient.full()
        let store = InMemoryCredentialStore.configured()
        let controller = makeController(fake: fake, store: store)
        await controller.bootstrap()

        await fake.setState(.failure(.invalidToken))
        await controller.refresh(force: true)

        #expect(controller.phase == .needsSetup(message: "Token inválido, gere um novo"))
        #expect(store.token == nil)
    }

    @Test func deviceOfflineFlagClearsOnSuccess() async throws {
        let fake = try FakeClient.full()
        let controller = makeController(fake: fake)
        await controller.bootstrap()

        await fake.setState(.failure(.deviceOffline))
        await controller.refresh(force: true)
        #expect(controller.isDeviceOffline)
        #expect(controller.statusMessage == "Ar-condicionado offline")

        await fake.setState(.success(try Fixture.json("state-full")))
        await controller.refresh(force: true)
        #expect(!controller.isDeviceOffline)
    }

    @Test func rateLimitBacksOffUpToThirtyMinutesAndResets() async throws {
        let fake = try FakeClient.full()
        let controller = makeController(fake: fake)
        await controller.bootstrap()
        await fake.setState(.failure(.rateLimited))

        var intervals: [Duration] = []
        for _ in 0..<4 {
            await controller.refresh(force: true)
            intervals.append(controller.pollInterval)
        }
        #expect(intervals == [.seconds(600), .seconds(1200), .seconds(1800), .seconds(1800)])

        await fake.setState(.success(try Fixture.json("state-full")))
        await controller.refresh(force: true)
        #expect(controller.pollInterval == .seconds(300))
    }

    @Test func refreshWithinFiveSecondsIsSkipped() async throws {
        let fake = try FakeClient.full()
        let clock = TestNow()
        let controller = makeController(fake: fake, clock: clock)
        await controller.bootstrap()
        #expect(await fake.stateCalls == 1)

        clock.date += 3
        await controller.refresh()
        #expect(await fake.stateCalls == 1)

        clock.date += 3
        await controller.refresh()
        #expect(await fake.stateCalls == 2)
    }
}
```

- [ ] **Step 4: Rodar e ver falhar**

Run: `./scripts/test.sh --filter "ACControllerSetupTests|ACControllerRefreshTests"`
Expected: FAIL na compilação, com `cannot find 'ACController' in scope`.

- [ ] **Step 5: Implementar o controlador**

```swift
import Foundation
import Observation

/// O "cérebro" observado pela interface: configuração, estado, polling e tratamento de erros.
/// Os comandos (otimistas, com debounce) ficam em `ACController+Commands.swift`.
@MainActor @Observable
public final class ACController {
    public enum Phase: Equatable {
        case needsSetup(message: String?)
        case loading
        case choosingDevice([ThinQDevice])
        case ready
    }

    nonisolated public static let basePollInterval: Duration = .seconds(300)
    nonisolated public static let maxPollInterval: Duration = .seconds(1800)
    nonisolated static let freshness: TimeInterval = 5
    nonisolated static let confirmDelay: Duration = .milliseconds(1500)
    nonisolated static let tempDebounce: Duration = .milliseconds(600)

    public internal(set) var phase: Phase = .loading
    public internal(set) var capabilities: ACCapabilities?
    public internal(set) var state: ACState?
    public internal(set) var deviceAlias = ""
    public internal(set) var lastUpdated: Date?
    public internal(set) var statusMessage: String?
    public internal(set) var isDeviceOffline = false
    public internal(set) var pollInterval: Duration = ACController.basePollInterval

    @ObservationIgnored let store: any CredentialStoring
    @ObservationIgnored let makeClient: @Sendable (ThinQCredentials) throws -> any ThinQClientProtocol
    @ObservationIgnored let sleep: @Sendable (Duration) async throws -> Void
    @ObservationIgnored let now: @Sendable () -> Date
    @ObservationIgnored var client: (any ThinQClientProtocol)?
    @ObservationIgnored var pendingSetup: (client: any ThinQClientProtocol, token: String)?
    @ObservationIgnored var pollTask: Task<Void, Never>?
    // Usados pelos comandos (ACController+Commands.swift).
    @ObservationIgnored var confirmTask: Task<Void, Never>?
    @ObservationIgnored var tempTask: Task<Void, Never>?
    @ObservationIgnored var pendingTargetTemp: Double?
    @ObservationIgnored var tempBaseline: ACState?

    public init(
        store: any CredentialStoring,
        makeClient: @escaping @Sendable (ThinQCredentials) throws -> any ThinQClientProtocol = {
            try ThinQClient(credentials: $0)
        },
        sleep: @escaping @Sendable (Duration) async throws -> Void = { try await Task.sleep(for: $0) },
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.store = store
        self.makeClient = makeClient
        self.sleep = sleep
        self.now = now
    }

    // MARK: - Configuração

    /// Chamado uma vez ao abrir o app.
    public func bootstrap() async {
        guard let token = store.token, store.deviceId != nil else {
            phase = .needsSetup(message: nil)
            return
        }
        do {
            client = try makeClient(ThinQCredentials(token: token, country: store.country, clientId: store.clientId))
        } catch {
            phase = .needsSetup(message: Self.message(for: error))
            return
        }
        deviceAlias = store.deviceAlias ?? ""
        await load()
    }

    /// Valida o token listando os aparelhos. O token só é salvo depois de escolhido o aparelho.
    public func connect(token rawToken: String, country: String) async {
        let token = rawToken.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !token.isEmpty else {
            phase = .needsSetup(message: "Cole o token")
            return
        }
        phase = .loading
        store.country = country
        do {
            let client = try makeClient(ThinQCredentials(token: token, country: country, clientId: store.clientId))
            let airConditioners = try await client.devices().filter(\.isAirConditioner)
            pendingSetup = (client, token)
            switch airConditioners.count {
            case 0: phase = .needsSetup(message: "Nenhum ar-condicionado encontrado nesta conta")
            case 1: await selectDevice(airConditioners[0])
            default: phase = .choosingDevice(airConditioners)
            }
        } catch {
            phase = .needsSetup(message: Self.message(for: error))
        }
    }

    public func selectDevice(_ device: ThinQDevice) async {
        guard let pendingSetup else { return }
        do {
            try store.saveToken(pendingSetup.token)
        } catch {
            phase = .needsSetup(message: "Não foi possível salvar o token no Keychain")
            return
        }
        self.pendingSetup = nil
        store.deviceId = device.id
        store.deviceAlias = device.alias
        deviceAlias = device.alias
        client = pendingSetup.client
        await load()
    }

    /// "Trocar token…": apaga o token e volta para a configuração.
    public func resetCredentials() {
        stopPolling()
        store.deleteToken()
        client = nil
        capabilities = nil
        state = nil
        lastUpdated = nil
        statusMessage = nil
        phase = .needsSetup(message: nil)
    }

    /// Carrega profile e estado. Sem rede, fica em `.loading` e o polling tenta de novo.
    func load() async {
        phase = .loading
        capabilities = nil
        await refresh(force: true)
        if case .needsSetup = phase { return }
        startPolling()
    }

    // MARK: - Estado

    /// Busca o estado. Sem `force`, ignora se a última leitura tem menos de 5 s.
    public func refresh(force: Bool = false) async {
        guard let client, let deviceId = store.deviceId else { return }
        if !force, let lastUpdated, now().timeIntervalSince(lastUpdated) < Self.freshness { return }
        do {
            if capabilities == nil {
                capabilities = ACCapabilities(profile: try await client.profile(deviceId: deviceId))
            }
            var fresh = ACState(state: try await client.state(deviceId: deviceId))
            // Um −/+ ainda não enviado vence o valor antigo do servidor.
            if let pendingTargetTemp { fresh.targetTemp = pendingTargetTemp }
            state = fresh
            lastUpdated = now()
            statusMessage = nil
            isDeviceOffline = false
            pollInterval = Self.basePollInterval
            phase = .ready
        } catch {
            handle(error)
        }
    }

    public func startPolling() {
        pollTask?.cancel()
        pollTask = Task { [weak self] in
            while let self, !Task.isCancelled {
                do { try await self.sleep(self.pollInterval) } catch { return }
                await self.refresh(force: true)
            }
        }
    }

    public func stopPolling() {
        pollTask?.cancel()
        pollTask = nil
    }

    // MARK: - Erros

    func handle(_ error: Error) {
        if error is CancellationError { return }
        guard let error = error as? ThinQError else {
            statusMessage = "Erro: \(error.localizedDescription)"
            return
        }
        switch error {
        case .invalidToken:
            stopPolling()
            store.deleteToken()
            client = nil
            phase = .needsSetup(message: error.userMessage)
        case .rateLimited:
            pollInterval = min(pollInterval * 2, Self.maxPollInterval)
            statusMessage = error.userMessage
        case .deviceOffline:
            isDeviceOffline = true
            statusMessage = error.userMessage
        default:
            statusMessage = error.userMessage
        }
    }

    static func message(for error: Error) -> String {
        (error as? ThinQError)?.userMessage ?? error.localizedDescription
    }
}
```

- [ ] **Step 6: Rodar e ver passar**

Run: `./scripts/test.sh --filter "ACControllerSetupTests|ACControllerRefreshTests"`
Expected: PASS, 15 testes.

- [ ] **Step 7: Rodar a suíte inteira**

Run: `./scripts/test.sh`
Expected: PASS em todos os testes, sem avisos de compilação.

- [ ] **Step 8: Commit**

```bash
git add Sources/LGACCore/ACController.swift Tests/LGACCoreTests/Support/FakeClient.swift Tests/LGACCoreTests/ACControllerSetupTests.swift Tests/LGACCoreTests/ACControllerRefreshTests.swift
git commit -m "feat: controlador com configuração, polling e tratamento de erros" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: `ACController`: comandos otimistas e debounce

**Files:**
- Create: `Sources/LGACCore/ACController+Commands.swift`
- Test: `Tests/LGACCoreTests/ACControllerCommandTests.swift`

**Interfaces:**
- Consumes: os internos do `ACController` listados na Task 9; `ACCommand` (Task 7); `FakeClient`, `makeController` (Task 9).
- Produces: `public func send(_ command: ACCommand) async` e `public func stepTargetTemp(by steps: Int)`. Internos: `flushTargetTemp()`, `deliver(_:rollbackTo:)` e `scheduleConfirmation()`.

Regras:
- `send` aplica o comando na tela na hora. Se a LG recusar, volta ao estado anterior.
- `stepTargetTemp` acumula cliques e envia **um** comando 0,6 s depois do último. Se a LG recusar, volta ao valor de antes do **primeiro** clique.
- Um `refresh` no meio do debounce não apaga o valor pendente. Isso é feito pelo `pendingTargetTemp` dentro de `refresh`, já escrito na Task 9.

- [ ] **Step 1: Escrever os testes**

`stepStopsAtTheLimit` e `rejectedBurstRollsBackToValueBeforeFirstClick` cobrem os itens 2 e 3 do Review Focus.

```swift
import Foundation
import Testing
@testable import LGACCore

@MainActor @Suite struct ACControllerCommandTests {
    private func readyController(_ fake: FakeClient) async -> ACController {
        let controller = makeController(fake: fake)
        await controller.bootstrap()
        return controller
    }

    @Test func sendIsOptimisticThenConfirms() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)

        await controller.send(.windStrength("HIGH"))

        #expect(controller.state?.windStrength == "HIGH")
        #expect(await fake.controls == [["airFlow": ["windStrength": "HIGH"]]])
        await controller.confirmTask?.value
        #expect(await fake.stateCalls == 2)
    }

    @Test func rejectedCommandRollsBack() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)
        await fake.setControlError(.commandRejected(code: "2305", message: "Modo não permite"))

        await controller.send(.jobMode("HEAT"))

        #expect(controller.state?.jobMode == "COOL")
        #expect(controller.statusMessage == "Comando recusado: Modo não permite")
    }

    @Test func unsupportedCommandIsIgnored() async throws {
        let fake = FakeClient(profile: try Fixture.json("profile-basic"), state: try Fixture.json("state-basic"))
        let controller = await readyController(fake)
        let before = controller.state

        await controller.send(.displayLight(false))

        #expect(await fake.controls.isEmpty)
        #expect(controller.state == before)
    }

    @Test func rapidClicksSendOneCommand() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)

        controller.stepTargetTemp(by: 1)
        controller.stepTargetTemp(by: 1)
        controller.stepTargetTemp(by: 1)
        #expect(controller.state?.targetTemp == 26)

        await controller.tempTask?.value
        #expect(await fake.controls == [["temperature": ["targetTemperature": 26]]])
    }

    @Test func stepStopsAtTheLimit() async throws {
        let fake = try FakeClient.full()
        await fake.setState(.success([
            "operation": ["airConOperationMode": "POWER_ON"],
            "temperature": ["targetTemperature": 30, "currentTemperature": 31, "unit": "C"],
        ]))
        let controller = await readyController(fake)

        controller.stepTargetTemp(by: 1)

        #expect(controller.state?.targetTemp == 30)
        #expect(controller.tempTask == nil)
        #expect(await fake.controls.isEmpty)
    }

    @Test func rejectedBurstRollsBackToValueBeforeFirstClick() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)
        await fake.setControlError(.commandRejected(code: "2304", message: ""))

        controller.stepTargetTemp(by: 1)
        controller.stepTargetTemp(by: 1)
        await controller.tempTask?.value

        #expect(controller.state?.targetTemp == 23)
        #expect(controller.statusMessage == "Comando recusado pelo aparelho")
    }

    @Test func refreshDuringDebounceKeepsPendingValue() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)

        controller.stepTargetTemp(by: 1)
        await controller.refresh(force: true)
        #expect(controller.state?.targetTemp == 24)

        await controller.tempTask?.value
        #expect(await fake.controls == [["temperature": ["targetTemperature": 24]]])
    }

    @Test func offTimerCancelUpdatesState() async throws {
        let fake = try FakeClient.full()
        let controller = await readyController(fake)
        #expect(controller.state?.offTimerRemainingMinutes == 80)

        await controller.send(.offTimer(minutes: nil))

        #expect(controller.state?.offTimerRemainingMinutes == nil)
        #expect(await fake.controls == [["timer": ["relativeStopTimer": "UNSET"]]])
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `./scripts/test.sh --filter ACControllerCommandTests`
Expected: FAIL na compilação, com `value of type 'ACController' has no member 'send'`.

- [ ] **Step 3: Implementar os comandos**

```swift
import Foundation

extension ACController {
    /// Aplica o comando na tela na hora e envia; se a LG recusar, volta ao estado anterior.
    public func send(_ command: ACCommand) async {
        guard let caps = capabilities, let current = state, let payload = command.payload(using: caps) else { return }
        state = command.apply(to: current)
        await deliver(payload, rollbackTo: current)
    }

    /// `−`/`+`: muda na tela na hora e envia um único comando 0,6 s depois do último clique.
    public func stepTargetTemp(by steps: Int) {
        guard let temp = capabilities?.targetTemp, let current = state,
            let value = pendingTargetTemp ?? current.targetTemp
        else { return }
        let next = temp.clamp(value + Double(steps) * temp.step)
        guard next != value else { return }
        if tempBaseline == nil { tempBaseline = current }
        pendingTargetTemp = next
        state = ACCommand.targetTemp(next).apply(to: current)
        tempTask?.cancel()
        tempTask = Task { [weak self] in
            guard let self else { return }
            do { try await self.sleep(Self.tempDebounce) } catch { return }
            await self.flushTargetTemp()
        }
    }

    func flushTargetTemp() async {
        guard let target = pendingTargetTemp, let baseline = tempBaseline else { return }
        pendingTargetTemp = nil
        tempBaseline = nil
        guard let caps = capabilities, let payload = ACCommand.targetTemp(target).payload(using: caps) else { return }
        await deliver(payload, rollbackTo: baseline)
    }

    func deliver(_ payload: JSONValue, rollbackTo previous: ACState) async {
        guard let client, let deviceId = store.deviceId else { return }
        do {
            try await client.control(deviceId: deviceId, payload: payload)
            statusMessage = nil
            scheduleConfirmation()
        } catch {
            if !(error is CancellationError) { state = previous }
            handle(error)
        }
    }

    /// Relê o estado ~1,5 s depois do comando, para confirmar que o aparelho aplicou.
    func scheduleConfirmation() {
        confirmTask?.cancel()
        confirmTask = Task { [weak self] in
            guard let self else { return }
            do { try await self.sleep(Self.confirmDelay) } catch { return }
            await self.refresh(force: true)
        }
    }
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `./scripts/test.sh --filter ACControllerCommandTests`
Expected: PASS, 8 testes.

- [ ] **Step 5: Conferir estabilidade**

Estes testes envolvem tarefas assíncronas. Rode a suíte inteira 5 vezes seguidas:

Run: `for i in 1 2 3 4 5; do ./scripts/test.sh 2>&1 | grep -E "✘|Test run"; done`
Expected: 5 linhas `✔ Test run with … tests … passed`, nenhuma `✘`.

- [ ] **Step 6: Commit**

```bash
git add Sources/LGACCore/ACController+Commands.swift Tests/LGACCoreTests/ACControllerCommandTests.swift
git commit -m "feat: comandos otimistas com rollback e debounce de temperatura" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: `ACLabels` e a casca do app

**Files:**
- Create: `Sources/LGACCore/ACLabels.swift`
- Test: `Tests/LGACCoreTests/ACLabelsTests.swift`
- Modify: `Package.swift` (acrescenta o alvo `LGACApp`)
- Create: `Sources/LGACApp/LGACApp.swift`
- Create: `Sources/LGACApp/RootView.swift`
- Create: `Sources/LGACApp/SetupView.swift`
- Create: `Sources/LGACApp/MenuBarIcon.swift`

**Interfaces:**
- Consumes: `ACController` com `phase`, `state`, `isDeviceOffline`, `statusMessage`, `deviceAlias`, `bootstrap`, `connect`, `selectDevice` e `refresh` (Task 9); `KeychainCredentialStore` (Task 8); `ThinQRegion.supportedCountries` e `ThinQDevice` (Tasks 2–3).
- Produces:
  - `public enum ACLabels` com `jobMode(_:) -> String`, `symbol(forJobMode: String?) -> String`, `windStrength(_:) -> String`, `duration(minutes:) -> String` e `temperature(_:locale:) -> String`.
  - Views: `RootView(controller:)`, `LoadingView`, `SetupView(controller:message:)`, `DeviceChoiceView(controller:devices:)` e `MenuBarIcon(controller:)`, além de `StatusIcon.image(symbol:dimmed:) -> NSImage`.

A interface não tem testes automatizados. A lógica dela está no core, que já está testado, e a verificação da UI é manual (Task 13).

- [ ] **Step 1: Escrever os testes dos rótulos**

```swift
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
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `./scripts/test.sh --filter ACLabelsTests`
Expected: FAIL na compilação, com `cannot find 'ACLabels' in scope`.

- [ ] **Step 3: Implementar `ACLabels`**

```swift
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
    public static func temperature(_ value: Double, locale: Locale = .current) -> String {
        value.formatted(.number.precision(.fractionLength(0...1)).locale(locale)) + "°"
    }

    /// `ENERGY_SAVING` → "Energy saving".
    static func humanize(_ raw: String) -> String {
        let words = raw.lowercased().split(separator: "_").joined(separator: " ")
        return words.prefix(1).uppercased() + words.dropFirst()
    }
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `./scripts/test.sh --filter ACLabelsTests`
Expected: PASS, 5 testes.

- [ ] **Step 5: Atualizar o `Package.swift`**

```swift
// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "LGACMenuBar",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "LGACCore"),
        .executableTarget(name: "LGACApp", dependencies: ["LGACCore"]),
        .executableTarget(name: "lgac-dump", dependencies: ["LGACCore"]),
        .testTarget(
            name: "LGACCoreTests",
            dependencies: ["LGACCore"],
            resources: [.copy("Fixtures")]
        ),
    ]
)
```

- [ ] **Step 6: Criar o app e o `AppDelegate`**

`Sources/LGACApp/LGACApp.swift`:

```swift
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
                Task { await controller.refresh(force: true) }
            }
        }
        Task { await controller.bootstrap() }
    }
}
```

- [ ] **Step 7: Criar o `RootView`**

A fase `.ready` mostra um texto provisório, que a Task 12 troca pelo painel.

`Sources/LGACApp/RootView.swift`:

```swift
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
```

- [ ] **Step 8: Criar a tela de configuração**

`Sources/LGACApp/SetupView.swift`:

```swift
import AppKit
import LGACCore
import SwiftUI

/// Primeira configuração: token + país.
struct SetupView: View {
    let controller: ACController
    let message: String?
    @State private var token = ""
    @State private var country = "BR"

    private static let countries: [(code: String, name: String)] = ThinQRegion.supportedCountries
        .map { (code: $0, name: Locale.current.localizedString(forRegionCode: $0) ?? $0) }
        .sorted { $0.name.localizedCompare($1.name) == .orderedAscending }

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
            Text("Escolha o ar-condicionado").font(.headline)
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
```

- [ ] **Step 9: Criar o ícone da barra**

`Sources/LGACApp/MenuBarIcon.swift`:

```swift
import AppKit
import LGACCore
import SwiftUI

/// Ícone da barra: símbolo do modo atual; esmaecido se desligado, offline ou sem estado.
struct MenuBarIcon: View {
    let controller: ACController

    var body: some View {
        let active = controller.state?.isOn == true && !controller.isDeviceOffline
        Image(nsImage: StatusIcon.image(symbol: ACLabels.symbol(forJobMode: controller.state?.jobMode), dimmed: !active))
    }
}

enum StatusIcon {
    /// A barra de menu ignora `.opacity` em imagens template, então o esmaecido é desenhado com alpha.
    static func image(symbol: String, dimmed: Bool) -> NSImage {
        let config = NSImage.SymbolConfiguration(pointSize: 14, weight: .medium)
        guard
            let symbolImage = NSImage(systemSymbolName: symbol, accessibilityDescription: "Ar-condicionado")?
                .withSymbolConfiguration(config)
        else { return NSImage() }
        guard dimmed else {
            symbolImage.isTemplate = true
            return symbolImage
        }
        let size = symbolImage.size
        var rect = CGRect(x: 0, y: 0, width: size.width * 2, height: size.height * 2)
        guard let source = symbolImage.cgImage(forProposedRect: &rect, context: nil, hints: nil),
            let context = CGContext(
                data: nil, width: Int(rect.width), height: Int(rect.height), bitsPerComponent: 8, bytesPerRow: 0,
                space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
        else { return symbolImage }
        context.setAlpha(0.35)
        context.draw(source, in: rect)
        guard let faded = context.makeImage() else { return symbolImage }
        let result = NSImage(cgImage: faded, size: size)
        result.isTemplate = true
        return result
    }
}
```

- [ ] **Step 10: Compilar tudo do zero**

Run: `rm -rf .build && swift build 2>&1 | grep -E "error|warning: |Build complete"`
Expected: só `Build complete!`, sem nenhuma linha `error` ou `warning:`.

- [ ] **Step 11: Conferir que o app abre**

Run: `swift run LGACApp & sleep 6; pgrep -x LGACApp && echo "rodando"; pkill -x LGACApp`
Expected: `rodando`. Durante esses segundos o ícone ❄ (esmaecido) aparece na barra de menu. Se houver uma pessoa acompanhando, ela pode clicar no ícone e ver a tela "Conectar ao LG ThinQ".

- [ ] **Step 12: Commit**

```bash
git add Sources/LGACCore/ACLabels.swift Tests/LGACCoreTests/ACLabelsTests.swift Package.swift Sources/LGACApp
git commit -m "feat: app de barra de menu com configuração e ícone" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Painel de controle e menu ⚙

**Files:**
- Create: `Sources/LGACApp/ControlPanelView.swift`
- Create: `Sources/LGACApp/SettingsMenu.swift`
- Modify: `Sources/LGACApp/RootView.swift` (troca o texto provisório pelo painel)

**Interfaces:**
- Consumes: `ACController` com `capabilities`, `state`, `deviceAlias`, `statusMessage`, `lastUpdated`, `send`, `stepTargetTemp`, `refresh` e `resetCredentials` (Tasks 9–10); `ACCommand` (Task 7); `ACLabels` (Task 11).
- Produces: `ControlPanelView(controller:)`, `ChoiceGrid(options:selected:title:symbol:choose:)` e `SettingsMenu(controller:)`.

Regras de interface do spec (seção 4) refletidas no código:
- Com o ar desligado, os controles abaixo do power ficam esmaecidos e desabilitados.
- Linhas sem suporte não aparecem.
- O timer mostra "Desliga em 1h 20min" quando está ativo.
- O rodapé mostra "Atualizado há …" ou a mensagem de erro.

- [ ] **Step 1: Criar o painel**

As chaves (`Toggle`) recebem uma closure `@Sendable` que monta o `ACCommand`, e não uma closure que captura a view. O `Binding` do SwiftUI exige `@Sendable` no Swift 6; do contrário, o build emite aviso de concorrência.

`Sources/LGACApp/ControlPanelView.swift`:

```swift
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
                Text(state.offTimerRemainingMinutes.map { "Desliga em \(ACLabels.duration(minutes: $0))" } ?? "Desativado")
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
```

- [ ] **Step 2: Criar o menu ⚙**

`Sources/LGACApp/SettingsMenu.swift`:

```swift
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
```

- [ ] **Step 3: Ligar o painel no `RootView`**

Em `Sources/LGACApp/RootView.swift`, substitua:

```swift
                Text("Conectado: \(controller.deviceAlias)").padding()  // trocado na Task 12
```

por:

```swift
                ControlPanelView(controller: controller)
```

- [ ] **Step 4: Compilar tudo do zero**

Run: `rm -rf .build && swift build 2>&1 | grep -E "error|warning: |Build complete"`
Expected: só `Build complete!`, sem nenhuma linha `error` ou `warning:`.

- [ ] **Step 5: Rodar a suíte**

Run: `./scripts/test.sh 2>&1 | grep -E "✘|Test run"`
Expected: `✔ Test run with 83 tests in 12 suites passed`.

- [ ] **Step 6: Commit**

```bash
git add Sources/LGACApp/ControlPanelView.swift Sources/LGACApp/SettingsMenu.swift Sources/LGACApp/RootView.swift
git commit -m "feat: painel de controle e menu de ajustes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Empacotamento, README e verificação no aparelho real

**Files:**
- Create: `Resources/Info.plist`
- Create: `scripts/make-icon.swift`
- Create: `Resources/AppIcon.icns` (gerado)
- Create: `build-app.sh`
- Modify: `README.md`

**Interfaces:**
- Consumes: produto `LGACApp` (Task 11).
- Produces: `./build-app.sh`, que gera `dist/LG AC.app`, instala em `/Applications` e abre o app; com `--no-install`, só gera o bundle. Também `swift scripts/make-icon.swift`, que regera `Resources/AppIcon.icns`.

- [ ] **Step 1: Criar o `Info.plist`**

`Resources/Info.plist`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>pt-BR</string>
    <key>CFBundleExecutable</key>
    <string>LGACApp</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleIdentifier</key>
    <string>com.local.lgac</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>LG AC</string>
    <key>CFBundleDisplayName</key>
    <string>LG AC</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>14.0</string>
    <key>LSUIElement</key>
    <true/>
    <key>NSHumanReadableCopyright</key>
    <string>Projeto independente, sem afiliação com a LG Electronics.</string>
</dict>
</plist>
```

Run: `plutil -lint Resources/Info.plist`
Expected: `Resources/Info.plist: OK`

- [ ] **Step 2: Criar o gerador de ícone e gerar o `.icns`**

`scripts/make-icon.swift`:

```swift
// Gera Resources/AppIcon.icns: floco de neve branco sobre um quadrado arredondado azul.
// Uso: swift scripts/make-icon.swift   (precisa de `iconutil`, que já vem no macOS)
import AppKit

let root = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
let iconset = root.appendingPathComponent(".build/AppIcon.iconset")
try? FileManager.default.removeItem(at: iconset)
try FileManager.default.createDirectory(at: iconset, withIntermediateDirectories: true)

func render(pixels: Int) -> Data {
    let size = CGFloat(pixels)
    let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels, bitsPerSample: 8, samplesPerPixel: 4,
        hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    NSGraphicsContext.saveGraphicsState()
    NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)

    let inset = size * 0.1
    let tile = NSRect(x: inset, y: inset, width: size - 2 * inset, height: size - 2 * inset)
    let path = NSBezierPath(roundedRect: tile, xRadius: tile.width * 0.225, yRadius: tile.width * 0.225)
    NSGradient(
        starting: NSColor(calibratedRed: 0.25, green: 0.62, blue: 1.0, alpha: 1),
        ending: NSColor(calibratedRed: 0.05, green: 0.36, blue: 0.85, alpha: 1))!
        .draw(in: path, angle: -90)

    let config = NSImage.SymbolConfiguration(pointSize: tile.width * 0.55, weight: .medium)
        .applying(NSImage.SymbolConfiguration(paletteColors: [.white]))
    if let symbol = NSImage(systemSymbolName: "snowflake", accessibilityDescription: nil)?
        .withSymbolConfiguration(config)
    {
        let origin = NSPoint(x: tile.midX - symbol.size.width / 2, y: tile.midY - symbol.size.height / 2)
        symbol.draw(at: origin, from: .zero, operation: .sourceOver, fraction: 1)
    }

    NSGraphicsContext.restoreGraphicsState()
    return rep.representation(using: .png, properties: [:])!
}

for points in [16, 32, 128, 256, 512] {
    try render(pixels: points).write(to: iconset.appendingPathComponent("icon_\(points)x\(points).png"))
    try render(pixels: points * 2).write(to: iconset.appendingPathComponent("icon_\(points)x\(points)@2x.png"))
}

let output = root.appendingPathComponent("Resources/AppIcon.icns")
let iconutil = Process()
iconutil.executableURL = URL(fileURLWithPath: "/usr/bin/iconutil")
iconutil.arguments = ["-c", "icns", iconset.path, "-o", output.path]
try iconutil.run()
iconutil.waitUntilExit()
guard iconutil.terminationStatus == 0 else { fatalError("iconutil falhou") }
print("✓ \(output.path)")
```

Run: `swift scripts/make-icon.swift`
Expected: `✓ …/Resources/AppIcon.icns`. Abra `.build/AppIcon.iconset/icon_256x256.png` e confira o floco branco sobre um quadrado azul arredondado.

- [ ] **Step 3: Criar o `build-app.sh`**

```bash
#!/usr/bin/env bash
# Compila, monta "LG AC.app", assina ad-hoc e instala em /Applications.
# Uso: ./build-app.sh            (compila e instala)
#      ./build-app.sh --no-install  (só gera dist/LG AC.app)
set -euo pipefail
cd "$(dirname "$0")"

app="dist/LG AC.app"

swift build -c release --product LGACApp
bin_dir="$(swift build -c release --show-bin-path)"

rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp "$bin_dir/LGACApp" "$app/Contents/MacOS/LGACApp"
cp Resources/Info.plist "$app/Contents/Info.plist"
cp Resources/AppIcon.icns "$app/Contents/Resources/AppIcon.icns"

codesign --force --sign - "$app"
echo "✓ $app"

if [[ "${1:-}" == "--no-install" ]]; then
  exit 0
fi

# Fecha a versão antiga, se estiver aberta, antes de substituir.
pkill -x LGACApp 2>/dev/null || true
rm -rf "/Applications/LG AC.app"
cp -R "$app" "/Applications/LG AC.app"
echo "✓ Instalado em /Applications/LG AC.app"
open "/Applications/LG AC.app"
```

Run: `chmod +x build-app.sh && ./build-app.sh --no-install && codesign -dv "dist/LG AC.app" 2>&1 | grep -E "Identifier|Signature"`
Expected: `✓ dist/LG AC.app`, `Identifier=com.local.lgac` e `Signature=adhoc`.

- [ ] **Step 4: Atualizar o README**

Em `README.md`:

1. Apague o bloco de status (a linha que começa com `> **Status: em desenvolvimento.**` e a linha em branco depois dela).
2. Substitua:

```markdown
4. copia para `/Applications`.

Depois, abra **LG AC** em `/Applications` (ou pelo Spotlight).
```

por:

```markdown
4. copia para `/Applications` e abre o app.

Para só gerar o bundle em `dist/LG AC.app`, sem instalar: `./build-app.sh --no-install`.
```

3. Substitua:

````markdown
### Rodar o app sem empacotar

```bash
swift run LGACApp
```
````

por:

````markdown
### Rodar o app sem empacotar

```bash
swift run LGACApp
```

Com `swift run`, a opção "Abrir ao iniciar sessão" não funciona (ela exige o `.app` instalado).

### Regerar o ícone

```bash
swift scripts/make-icon.swift
```
````

- [ ] **Step 5: Verificação final automática**

Run: `rm -rf .build && swift build 2>&1 | grep -E "error|warning: |Build complete" && ./scripts/test.sh 2>&1 | grep -E "✘|Test run"`
Expected: `Build complete!` sem avisos e `✔ Test run with 83 tests in 12 suites passed`.

- [ ] **Step 6: Commit**

```bash
git add Resources/Info.plist Resources/AppIcon.icns scripts/make-icon.swift build-app.sh README.md
git commit -m "feat: empacotamento do LG AC.app e README final" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 7: CHECKPOINT HUMANO — instalar e testar no aparelho real**

Peça à pessoa que rode `./build-app.sh` (instala e abre o app), configure o token pela tela do app e percorra o checklist abaixo, conferindo no aparelho e no app ThinQ do celular. Relate cada item que falhar, com a mensagem do rodapé.

- [ ] Ícone aparece na barra; esmaecido com o ar desligado, cheio com ele ligado
- [ ] Botão ⏻ liga e desliga
- [ ] `−`/`+` mudam a temperatura; vários cliques rápidos chegam como um único ajuste
- [ ] Cada modo disponível muda o modo no aparelho, e o ícone da barra acompanha
- [ ] Cada velocidade do ventilador funciona
- [ ] Swing vertical e horizontal (se o modelo tiver) ligam e desligam
- [ ] Timer: definir 30 min mostra "Desliga em 30 min"; "Desativado" cancela
- [ ] Luz do visor liga e desliga
- [ ] Mudança feita no app ThinQ do celular aparece ao reabrir o painel
- [ ] Com o Wi-Fi do Mac desligado, ⟳ mostra "Sem conexão"; ao religar, volta ao normal
- [ ] ⚙ → "Abrir ao iniciar sessão" fica marcado e o app abre após sair e entrar na sessão
- [ ] ⚙ → "Trocar token…" volta para a tela de configuração

Se algum item falhar, use superpowers:systematic-debugging antes de corrigir. Os comandos mais prováveis de precisar de ajuste são o cancelamento do timer e a luz do visor, cujos formatos variam por modelo.

- [ ] **Step 8: Push**

Com o checklist aprovado pela pessoa:

```bash
git push
```
