# LG AC Menu Bar — Design

**Data:** 2026-10-13
**Status:** aprovado em conversa, aguardando revisão do spec escrito

## 1. Objetivo

App para macOS que vive na barra de menu superior (ao lado de Wi-Fi, som, bateria) e controla **um** ar-condicionado LG via Wi-Fi (nuvem LG ThinQ), sem precisar abrir o celular.

### Critérios de sucesso

- Ligar/desligar ou mudar a temperatura em no máximo 2 cliques a partir da barra.
- O estado exibido reflete o estado real do aparelho em poucos segundos após um comando.
- Funciona sem Xcode, sem dependências de terceiros, só com Swift 6.2 + Command Line Tools.

### O que o usuário pediu

- App de barra de menu no macOS.
- Um único aparelho, já cadastrado e funcionando no app LG ThinQ.
- Controles: liga/desliga, temperatura alvo, modo de operação, velocidade do ventilador, swing (vertical/horizontal), timer de desligar, luz do visor.
- Na barra: **só o ícone**, cheio quando ligado e esmaecido quando desligado.
- Abordagem: app nativo Swift/SwiftUI.

### Suposições (confirmadas na conversa)

- Uso pessoal: sem App Store, sem notarização, sem distribuição.
- Painel ao clicar no estilo Central de Controle (não menu de texto).
- Controle passa pela nuvem LG; aparelhos LG não oferecem API local. Requer internet.
- O app abre automaticamente ao iniciar sessão (configurável).

## 2. API LG ThinQ Connect

Fonte: SDK oficial `thinqconnect` (github.com/thinq-connect/pythinqconnect, Apache-2.0).

### Autenticação

Personal Access Token (PAT) gerado em https://connect-pat.lgthinq.com com os escopos: listar dispositivos, ver status, controlar dispositivo.

### Base URL

`https://api-{região}.lgthinq.com/` — região derivada do país. `BR` → `aic` → `https://api-aic.lgthinq.com/`. Regiões: `kic`, `aic`, `eic`. O app inclui a tabela país→região do SDK.

### Headers (toda requisição)

| Header | Valor |
|---|---|
| `Authorization` | `Bearer <PAT>` |
| `x-country` | código do país, ex. `BR` |
| `x-message-id` | id único por requisição (UUID em base64url, 22 caracteres, como o SDK) |
| `x-client-id` | UUID gerado uma vez na instalação e persistido |
| `x-api-key` | `v6GFvkweNo7DK7yD3ylIZ9w52aKBU0eJ7wLXkSR3` (constante pública do SDK) |
| `x-service-phase` | `OP` |
| `Content-Type` | `application/json` |

Em `POST /control`, adicionar também `x-conditional-control: true`.

### Endpoints

| Operação | Método | Caminho |
|---|---|---|
| Listar aparelhos | GET | `devices` |
| Profile (capacidades) | GET | `devices/{deviceId}/profile` |
| Estado | GET | `devices/{deviceId}/state` |
| Comando | POST | `devices/{deviceId}/control` |

Resposta de sucesso: `{"response": ...}`. Erro: `{"error": {"code": "1103", "message": "..."}}`.

### Recursos do ar-condicionado usados

| Controle | Recurso → chave | Exemplo de payload de comando |
|---|---|---|
| Liga/desliga | `operation.airConOperationMode` | `{"operation": {"airConOperationMode": "POWER_ON"}}` |
| Modo | `airConJobMode.currentJobMode` | `{"airConJobMode": {"currentJobMode": "COOL"}}` |
| Temperatura alvo | `temperatureInUnits.targetTemperature*` + `unit` | `{"temperatureInUnits": {"targetTemperature": 23, "unit": "C"}}` |
| Temperatura ambiente (só leitura) | `temperatureInUnits.currentTemperatureC` | — |
| Ventilador | `airFlow.windStrength` (ou `windStrengthDetail`, conforme profile) | `{"airFlow": {"windStrength": "HIGH"}}` |
| Swing vertical | `windDirection.rotateUpDown` | `{"windDirection": {"rotateUpDown": true}}` |
| Swing horizontal | `windDirection.rotateLeftRight` | `{"windDirection": {"rotateLeftRight": false}}` |
| Timer de desligar | `timer.relativeHourToStop` / `relativeMinuteToStop` | `{"timer": {"relativeHourToStop": 2}}` |
| Luz do visor | `display.light` | `{"display": {"light": "OFF"}}` |

Formato do profile: `response.property.<recurso>.<chave>` é um objeto `{"type": "enum"|"range"|"boolean"|"number", "mode": ["r","w"], "value": {"r": ..., "w": ...}}`. Para `range`, `value.w` é `{"min", "max", "step"}`. Para `enum`, `value.w` é a lista de valores aceitos. Uma chave é gravável se `mode` contém `"w"`.

**Valores exatos dependem do modelo** (nomes de modos, velocidades, formato de booleanos, como cancelar o timer, se `timer` ou `sleepTimer` está disponível). Eles são confirmados no Passo 0 (seção 7) com o profile real do aparelho, antes de escrever a interface. O app nunca hard-coda listas de modos/velocidades: lê do profile.

## 3. Arquitetura

Pacote Swift (`Package.swift`, `swift-tools-version: 6.0`, plataforma `.macOS(.v14)`), sem dependências externas.

```
lg-ac/
├── Package.swift
├── .gitignore               # .build/, *.app, fixtures brutas com dados pessoais
├── Sources/
│   ├── LGACCore/            # lógica, sem UI, testável
│   │   ├── ThinQClient.swift
│   │   ├── ThinQRegion.swift
│   │   ├── ThinQError.swift
│   │   ├── ACCapabilities.swift
│   │   ├── ACState.swift
│   │   ├── ACCommand.swift
│   │   ├── ACController.swift
│   │   └── CredentialStore.swift
│   ├── LGACApp/             # executável SwiftUI (menu bar)
│   │   ├── LGACApp.swift
│   │   ├── MenuBarIcon.swift
│   │   ├── ControlPanelView.swift
│   │   ├── SetupView.swift
│   │   └── SettingsMenu.swift
│   └── lgac-dump/           # ferramenta de dev: salva JSON real do aparelho
│       └── main.swift
├── Tests/LGACCoreTests/
│   ├── Fixtures/            # profile/state reais + sintéticos
│   └── *.swift
├── Resources/               # Info.plist template, AppIcon.icns
├── build-app.sh
└── README.md
```

### Componentes de `LGACCore`

- **`ThinQClient`** — protocolo `ThinQClientProtocol` + implementação com `URLSession` async/await. Métodos: `devices()`, `profile(deviceId:)`, `state(deviceId:)`, `control(deviceId:payload:)`. Monta URL e headers, decodifica `response`, converte `error` em `ThinQError`. `Sendable`.
- **`ThinQRegion`** — tabela país→região copiada do SDK; `baseURL(for country:)`.
- **`ThinQError`** — enum, mapeado da tabela `ThinQAPIErrorCodes` do SDK:
  - `.invalidToken` — `1103`, `1218`, `1302`, ou HTTP 401
  - `.rateLimited` — `1306`
  - `.deviceOffline` — `1222`
  - `.commandRejected(code, message)` — `1102`, `2205`, `2207`, `2208`, `2301`–`2305`
  - `.network(URLError)` — falha de transporte
  - `.unexpected(code, message)` — qualquer outro código
- **`ACCapabilities`** — parse do profile: `powerSupported`, `jobModes: [String]`, `targetTemp: (min, max, step, unit)`, `windStrengths: [String]`, `windStrengthKey` (`windStrength` ou `windStrengthDetail`), `swingUpDown: Bool`, `swingLeftRight: Bool`, `offTimer: OffTimerKind?` (`timer` / `sleepTimer` / nenhum), `displayLight: [String]?` (valores aceitos). Controle ausente ou não gravável → não aparece na UI.
- **`ACState`** — `isOn`, `jobMode`, `targetTemp`, `currentTemp`, `windStrength`, `swingUpDown`, `swingLeftRight`, `displayLightOn`, `offTimerRemaining: Duration?`. Parse tolerante: chave ausente vira `nil`.
- **`ACCommand`** — enum: `.power(Bool)`, `.jobMode(String)`, `.targetTemp(Double)`, `.windStrength(String)`, `.swingUpDown(Bool)`, `.swingLeftRight(Bool)`, `.offTimer(minutes: Int?)` (`nil` = cancelar), `.displayLight(Bool)`. Função pura `payload(using: ACCapabilities) -> JSONValue`, onde `JSONValue` é um enum `Codable`, `Sendable` e `Equatable` (`object`, `array`, `string`, `number`, `bool`, `null`) definido em `LGACCore`; o mesmo tipo representa as respostas cruas de profile/state. Também `apply(to: ACState) -> ACState` para atualização otimista.
- **`ACController`** — `@MainActor @Observable`. Estado: `capabilities`, `state`, `lastUpdated`, `statusMessage`, `phase` (`.needsSetup`, `.loading`, `.ready`, `.error`). Métodos: `bootstrap()`, `refresh(force:)`, `send(_ command:)`, `setTargetTempDebounced(_:)`, `configure(token:country:)`. Gerencia polling, debounce e backoff. Recebe `ThinQClientProtocol` e um relógio (`Clock`) por injeção, para testes.
- **`CredentialStore`** — PAT no Keychain (`kSecClassGenericPassword`, service `com.local.lgac`). `deviceId`, `country`, `clientId` em `UserDefaults`.

### Componentes de `LGACApp`

- **`LGACApp`** — `@main`, `MenuBarExtra { ControlPanelView } label: { MenuBarIcon }` com `.menuBarExtraStyle(.window)`. Cria um único `ACController`.
- **`MenuBarIcon`** — SF Symbol por modo: `snowflake` (COOL), `sun.max` (HEAT), `drop` (AIR_DRY), `fan` (FAN), `thermometer.medium` (AUTO e outros). Ligado: opacidade normal; desligado / offline / sem estado: esmaecido (imagem template renderizada com alpha reduzido via `NSImage`, já que o status bar ignora `.opacity` em templates).
- **`ControlPanelView`** — painel ~300pt (layout na seção 4).
- **`SetupView`** — campo do token (SecureField), país (Picker, padrão Brasil), botão "Conectar". Lista aparelhos, filtra `deviceType == "DEVICE_AIR_CONDITIONER"`; se houver exatamente um, seleciona; se houver mais de um, mostra Picker; se nenhum, erro.
- **`SettingsMenu`** — botão ⚙: "Trocar token…", "Abrir ao iniciar sessão" (toggle via `SMAppService.mainApp`), "Sair".

### `lgac-dump`

Executável de desenvolvimento. Lê `LGAC_TOKEN` e `LGAC_COUNTRY` do ambiente, usa `ThinQClient`, grava `devices.json`, `profile.json`, `state.json` em um diretório informado por argumento. Não faz parte do `.app`.

## 4. Interface

### Barra

Só o ícone, como descrito em `MenuBarIcon`.

### Painel

```
┌────────────────────────────────────┐
│  Ar do Quarto               ( ⏻ )  │  alias do ThinQ + botão power
│  Ambiente 26°                      │
├────────────────────────────────────┤
│         ( − )    23°    ( + )      │  temperatura alvo
├────────────────────────────────────┤
│  Modo                              │
│  [❄ Frio][💧 Seco][🌀 Vent][A Auto]  │  modos do profile
│  Ventilador                        │
│  [Baixa][Média][Alta][Auto]        │  velocidades do profile
├────────────────────────────────────┤
│  ↕  Swing vertical         [ ●  ]  │
│  ↔  Swing horizontal       [  ○ ]  │
│  💡 Luz do visor           [ ●  ]  │
│  ⏲  Desligar em     [ 2 h      ▾]  │
├────────────────────────────────────┤
│  Atualizado há 12s    ⟳        ⚙  │
└────────────────────────────────────┘
```

Regras:

- Ar desligado: tudo abaixo do power fica esmaecido e desabilitado.
- `−`/`+` respeitam `min`/`max`/`step` do profile. Cliques rápidos são agrupados: o comando sai 0,6s após o último clique, com o valor final.
- Linhas sem suporte no profile não são exibidas.
- Timer: opções Desativado, 30 min, 1 h, 2 h, 3 h, 4 h, 6 h, 8 h. Com timer ativo, mostra "Desliga em 1h 20min".
- Rótulos em português para valores conhecidos (`COOL` → Frio, `AIR_DRY` → Seco, `FAN` → Ventilar, `AUTO` → Auto, `HEAT` → Quente, `LOW` → Baixa, `MID` → Média, `HIGH` → Alta). Valores desconhecidos exibem o nome cru, capitalizado.
- Rodapé: "Atualizado há Ns", ou a mensagem de erro atual, + botão ⟳ + ⚙.
- Primeira execução / token inválido: painel mostra `SetupView` no lugar dos controles.

## 5. Fluxo de dados

1. **Bootstrap:** carrega credenciais. Sem token ou `deviceId` → `.needsSetup`. Com credenciais → busca profile (uma vez por execução) e estado.
2. **Atualização de estado** (`GET state`):
   - ao abrir o painel, se a última leitura tiver mais de 5s;
   - ~1,5s após cada comando bem-sucedido (confirmação);
   - a cada 5 min em segundo plano (intervalo base);
   - ao acordar do repouso (`NSWorkspace.didWakeNotification`);
   - no botão ⟳.
3. **Comando:** `send(cmd)` → aplica `cmd.apply(to: state)` imediatamente (otimista) → `POST control` → sucesso: agenda confirmação; falha: restaura o estado anterior e mostra o erro no rodapé.
4. **Sem tempo real:** eventos MQTT (AWS IoT) ficam fora; mudanças feitas pelo controle remoto aparecem em até 5 min ou ao abrir o painel.

## 6. Tratamento de erros

| Situação | Comportamento |
|---|---|
| Sem internet / timeout (`URLError`) | Rodapé "Sem conexão". Mantém o último estado; tenta de novo no próximo ciclo. |
| Token inválido (`1103`, `1218`, `1302` ou HTTP 401) | `phase = .needsSetup` com mensagem "Token inválido, gere um novo". |
| Limite de chamadas (`1306`) | Rodapé com aviso. Intervalo de polling dobra a cada ocorrência (5 → 10 → 20 → 30 min, teto 30). Volta a 5 min no primeiro sucesso. |
| Aparelho offline (`1222`) | Rodapé "Ar-condicionado offline". Ícone esmaecido. |
| Comando recusado (`1102`, `2205`, `2207`, `2208`, `2301`–`2305`) | Rollback do estado otimista + mensagem da LG no rodapé. |

O token nunca é logado nem exibido depois de salvo.

## 7. Build, testes e instalação

### Passo 0 — dados reais

Antes da UI: implementar `ThinQClient` + `lgac-dump`, rodar com o token do usuário e salvar o JSON real em `Tests/LGACCoreTests/Fixtures/`. Ajustar `ACCapabilities`/`ACCommand` aos valores reais (modos, velocidades, booleanos, cancelamento de timer, luz do visor). O token não é commitado; as fixtures têm `deviceId` e alias anonimizados.

### Testes automatizados (Swift Testing, `swift test`)

- `ACCommand`: cada caso gera o payload esperado, incluindo `windStrengthDetail` vs `windStrength` e `timer` vs `sleepTimer`.
- `ACCapabilities` / `ACState`: parse da fixture real e de uma fixture sintética "modelo básico" (sem swing horizontal, sem luz do visor, sem timer).
- `ThinQClient`: URL por país, todos os headers, `x-conditional-control` só no control, mapeamento de erros. Usa `URLProtocol` stub; nunca chama a LG.
- `ACController`: otimista + rollback, debounce de temperatura (vários cliques → um comando), backoff de polling e reset, transição para `.needsSetup` em token inválido. Usa cliente falso e relógio controlável.

### Teste manual (aparelho real)

Checklist: power on/off; `−`/`+` (incluindo cliques rápidos); cada modo; cada velocidade; swing vertical e horizontal; timer (definir, ver contagem, cancelar); luz do visor; ícone muda com modo e power; mudança feita no app ThinQ aparece ao reabrir o painel; desconectar Wi-Fi do Mac mostra "Sem conexão".

### Empacotamento

`build-app.sh`:

1. `swift build -c release --product LGACApp`
2. monta `LG AC.app/Contents/{MacOS,Resources}` com `Info.plist` (`CFBundleIdentifier com.local.lgac`, `LSUIElement = true`, `LSMinimumSystemVersion 14.0`) e `AppIcon.icns`
3. `codesign --force --sign - "LG AC.app"` (ad-hoc)
4. copia para `/Applications`

"Abrir ao iniciar sessão" via `SMAppService.mainApp.register()`/`unregister()`.

### README

Como gerar o PAT (link, escopos), como compilar/instalar, como rodar os testes, como usar `lgac-dump`.

## 8. Fora do escopo

- Múltiplos aparelhos.
- Eventos em tempo real (MQTT / AWS IoT).
- Widget/Controle da Central de Controle (WidgetKit — exige Xcode e perfil de desenvolvedor).
- App Store, notarização, distribuição.
- Agendamentos, histórico de consumo, modos especiais (jet, energy saving, air clean).
