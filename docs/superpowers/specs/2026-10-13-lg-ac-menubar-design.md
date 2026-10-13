# LG AC Menu Bar — Design

**Data:** 2026-10-13
**Status:** aprovado

## 1. Objetivo

App para macOS que vive na barra de menu superior (ao lado de Wi-Fi, som, bateria) e controla **um** ar-condicionado LG via Wi-Fi (nuvem LG ThinQ), sem precisar abrir o celular.

### Critérios de sucesso

- Ligar/desligar ou mudar a temperatura em no máximo 2 cliques a partir da barra.
- O estado exibido reflete o estado real do aparelho em poucos segundos após um comando.
- Funciona sem Xcode, sem dependências de terceiros, só com Swift 6.2 + Command Line Tools.

### Requisitos

- App de barra de menu no macOS.
- Um único aparelho, já cadastrado e funcionando no app LG ThinQ.
- Controles: liga/desliga, temperatura alvo, modo de operação, velocidade do ventilador, swing (vertical/horizontal), timer de desligar, luz do visor.
- Na barra: **só o ícone**, cheio quando ligado e esmaecido quando desligado.
- App nativo Swift/SwiftUI.

### Premissas

- Uso individual: sem App Store, sem notarização; cada pessoa compila e usa com o próprio token.
- Painel ao clicar no estilo Central de Controle (não menu de texto).
- Controle passa pela nuvem LG; aparelhos LG não oferecem API local. Requer internet.
- O app pode abrir automaticamente ao iniciar sessão (configurável).

## 2. API LG ThinQ Connect

Fonte: SDK oficial `thinqconnect` (https://github.com/thinq-connect/pythinqconnect, Apache-2.0) e as fixtures de ar-condicionado da integração `lg_thinq` do Home Assistant (Apache-2.0).

### Autenticação

Personal Access Token (PAT) gerado em https://connect-pat.lgthinq.com com os escopos: listar dispositivos, ver status, controlar dispositivo.

### Base URL

`https://api-{região}.lgthinq.com/` — região derivada do país. `BR` → `aic` → `https://api-aic.lgthinq.com/`. Regiões: `kic`, `aic`, `eic`. O app inclui a tabela país→região do SDK (`thinqconnect/country.py`).

### Headers (toda requisição)

| Header | Valor |
|---|---|
| `Authorization` | `Bearer <PAT>` |
| `x-country` | código do país, ex. `BR` |
| `x-message-id` | id único por requisição (UUID em base64url sem padding, 22 caracteres, como o SDK) |
| `x-client-id` | identificador gerado uma vez na instalação e persistido (`lgac-<uuid>`) |
| `x-api-key` | constante `API_KEY` publicada no SDK oficial (`thinqconnect/const.py`), a mesma para todos os clientes |
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

Item da lista de aparelhos: `{"deviceId": "...", "deviceInfo": {"deviceType": "DEVICE_AIR_CONDITIONER", "modelName": "...", "alias": "...", "reportable": true}}`.

### Recursos do ar-condicionado usados

| Controle | Recurso → chave | Exemplo de payload de comando |
|---|---|---|
| Liga/desliga | `operation.airConOperationMode` (`POWER_ON`/`POWER_OFF`) | `{"operation": {"airConOperationMode": "POWER_ON"}}` |
| Modo | `airConJobMode.currentJobMode` | `{"airConJobMode": {"currentJobMode": "COOL"}}` |
| Temperatura alvo | ver "Temperatura" abaixo | `{"temperature": {"targetTemperature": 23}}` |
| Temperatura ambiente (só leitura) | `temperatureInUnits[unit=C].currentTemperature`, senão `temperature.currentTemperature` | — |
| Ventilador | `airFlow.windStrength` (ou `windStrengthDetail`, conforme profile) | `{"airFlow": {"windStrength": "HIGH"}}` |
| Swing vertical | `windDirection.rotateUpDown` (boolean) | `{"windDirection": {"rotateUpDown": true}}` |
| Swing horizontal | `windDirection.rotateLeftRight` (boolean) | `{"windDirection": {"rotateLeftRight": false}}` |
| Timer de desligar — definir | `timer.relativeHourToStop` / `relativeMinuteToStop` | `{"timer": {"relativeHourToStop": 2}}` |
| Timer de desligar — cancelar | `timer.relativeStopTimer` | `{"timer": {"relativeStopTimer": "UNSET"}}` |
| Luz do visor | `display.light` (enum `ON`/`OFF` ou boolean) | `{"display": {"light": "OFF"}}` |

**Formato do profile:** `response.property.<recurso>.<chave>` é um objeto `{"type": "enum"|"range"|"boolean"|"number", "mode": ["r","w"], "value": {"r": ..., "w": ...}}`. Para `range`, `value.w` é `{"min", "max", "step"}`. Para `enum`/`boolean`, `value.w` é a lista de valores aceitos. Uma chave é gravável se `mode` contém `"w"`. O recurso `temperatureInUnits` é uma **lista**, um item por unidade (`"unit": "C"` / `"F"`); o app usa só o item `C`.

**Temperatura:** modelos diferentes expõem a temperatura alvo gravável em lugares diferentes. O app escolhe o primeiro candidato gravável do tipo `range`, nesta ordem:

1. `temperatureInUnits[unit=C].targetTemperature` → payload `{"temperatureInUnits": {"targetTemperature": v, "unit": "C"}}`
2. `temperature.targetTemperature` → payload `{"temperature": {"targetTemperature": v}}`
3. `temperatureInUnits[unit=C].coolTargetTemperature` → payload `{"temperatureInUnits": {"coolTargetTemperature": v, "unit": "C"}}`
4. `temperature.coolTargetTemperature` → payload `{"temperature": {"coolTargetTemperature": v}}`

A leitura da temperatura alvo vem de `temperatureInUnits[unit=C].targetTemperature`, senão de `temperature.targetTemperature`.

**Timer:** o app usa `timer` quando `timer.relativeHourToStop` é gravável; senão `sleepTimer` quando `sleepTimer.relativeHourToStop` é gravável; senão não mostra o timer. O estado informa `relativeStopTimer` (`SET`/`UNSET`) e, quando `SET`, `relativeHourToStop`/`relativeMinuteToStop` com o tempo restante.

**Valores exatos dependem do modelo.** Eles são confirmados no Passo 0 (seção 7) com o profile real do aparelho, antes de escrever a interface. O app nunca hard-coda listas de velocidades: lê do profile. Modos são os do profile, filtrados aos cinco suportados pelo app (`COOL`, `HEAT`, `AIR_DRY`, `FAN`, `AUTO`).

## 3. Arquitetura

Pacote Swift (`Package.swift`, `swift-tools-version: 6.0`, plataforma `.macOS(.v14)`), sem dependências externas.

```
lg-ac-menubar/
├── Package.swift
├── .gitignore               # .build/, *.app, dump/ (dados reais do aparelho)
├── Sources/
│   ├── LGACCore/            # lógica, sem UI, testável
│   │   ├── JSONValue.swift
│   │   ├── ThinQRegion.swift
│   │   ├── ThinQError.swift
│   │   ├── ThinQClient.swift
│   │   ├── ACCapabilities.swift
│   │   ├── ACState.swift
│   │   ├── ACCommand.swift
│   │   ├── ACLabels.swift
│   │   ├── CredentialStore.swift
│   │   └── ACController.swift
│   ├── LGACApp/             # executável SwiftUI (menu bar)
│   │   ├── LGACApp.swift
│   │   ├── MenuBarIcon.swift
│   │   ├── ControlPanelView.swift
│   │   ├── SetupView.swift
│   │   └── SettingsMenu.swift
│   └── lgac-dump/           # ferramenta de dev: salva JSON real do aparelho
│       └── main.swift
├── Tests/LGACCoreTests/
│   ├── Fixtures/            # profile/state anonimizados
│   └── *.swift
├── Resources/               # Info.plist, AppIcon.icns
├── scripts/                 # test.sh, make-icon.swift
├── build-app.sh
└── README.md
```

### Componentes de `LGACCore`

- **`JSONValue`** — enum `Codable`, `Sendable`, `Equatable` (`object`, `array`, `string`, `number`, `bool`, `null`) com literais e acessores. Representa respostas cruas de profile/state e payloads de comando.
- **`ThinQRegion`** — tabela país→região do SDK; `baseURL(forCountry:)`; lista de países suportados.
- **`ThinQError`** — enum, mapeado da tabela `ThinQAPIErrorCodes` do SDK:
  - `.invalidToken` — `1103`, `1218`, `1302`, ou HTTP 401
  - `.rateLimited` — `1306`
  - `.deviceOffline` — `1222`
  - `.commandRejected(code, message)` — `1102`, `2205`, `2207`, `2208`, `2301`–`2305`
  - `.network(URLError.Code)` — falha de transporte
  - `.unexpected(code, message)` — qualquer outro código ou resposta ilegível
  - `.unsupportedCountry`
- **`ThinQClient`** — protocolo `ThinQClientProtocol` + implementação. Métodos: `devices()`, `profile(deviceId:)`, `state(deviceId:)`, `control(deviceId:payload:)`. Monta URL e headers, decodifica `response`, converte `error` em `ThinQError`. O transporte HTTP é uma closure injetável (padrão: `URLSession.shared.data(for:)`), substituída por um stub nos testes. `Sendable`.
- **`ACCapabilities`** — parse do profile: `power`, `jobModes`, `targetTemp` (recurso, chave, min, max, step), `windStrengths` + `windStrengthKey`, `swingUpDown`, `swingLeftRight`, `offTimer` (`timer` / `sleepTimer` / `nil`), `displayLight` (`onOff` / `boolean` / `nil`). Controle ausente ou não gravável → `nil`/`false` → não aparece na UI.
- **`ACState`** — `isOn`, `jobMode`, `targetTemp`, `currentTemp`, `windStrength`, `swingUpDown`, `swingLeftRight`, `displayLightOn`, `offTimerRemainingMinutes`. Parse tolerante: chave ausente vira `nil`.
- **`ACCommand`** — enum: `.power(Bool)`, `.jobMode(String)`, `.targetTemp(Double)`, `.windStrength(String)`, `.swingUpDown(Bool)`, `.swingLeftRight(Bool)`, `.offTimer(minutes: Int?)` (`nil` = cancelar), `.displayLight(Bool)`. Função pura `payload(using: ACCapabilities) -> JSONValue?` (`nil` se o modelo não suporta) e `apply(to: ACState) -> ACState` para atualização otimista.
- **`ACLabels`** — rótulos em português e SF Symbols por modo/velocidade.
- **`CredentialStore`** — protocolo `CredentialStoring` + `KeychainCredentialStore`: PAT no Keychain (`kSecClassGenericPassword`, service `com.local.lgac`); `deviceId`, `country`, `clientId` em `UserDefaults`.
- **`ACController`** — `@MainActor @Observable`. Estado: `phase` (`.needsSetup(message:)`, `.loading`, `.choosingDevice([ThinQDevice])`, `.ready`), `capabilities`, `state`, `deviceAlias`, `lastUpdated`, `statusMessage`, `isDeviceOffline`, `pollInterval`. Métodos: `bootstrap()`, `connect(token:country:)`, `selectDevice(_:)`, `refresh(force:)`, `send(_:)`, `stepTargetTemp(by:)`, `resetCredentials()`, `startPolling()`, `stopPolling()`. Recebe o store, uma fábrica de clientes e uma função de espera (`sleep`) por injeção, para testes determinísticos.

### Componentes de `LGACApp`

- **`LGACApp`** — `@main`, `MenuBarExtra { ControlPanelView } label: { MenuBarIcon }` com `.menuBarExtraStyle(.window)`. Um `AppDelegate` cria o único `ACController`, chama `bootstrap()` e observa `NSWorkspace.didWakeNotification`.
- **`MenuBarIcon`** — SF Symbol por modo: `snowflake` (COOL), `sun.max` (HEAT), `drop` (AIR_DRY), `fan` (FAN), `thermometer.medium` (AUTO e outros). Ligado: opacidade normal; desligado / offline / sem estado: esmaecido (imagem template desenhada com alpha reduzido via `NSImage`, já que o status bar ignora `.opacity` em templates).
- **`ControlPanelView`** — painel ~320pt (layout na seção 4).
- **`SetupView`** — campo do token (SecureField), país (Picker, padrão Brasil), botão "Conectar"; lista de escolha quando a conta tem mais de um ar-condicionado.
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
- Rótulos em português para valores conhecidos (`COOL` → Frio, `AIR_DRY` → Seco, `FAN` → Ventilar, `AUTO` → Auto, `HEAT` → Quente, `LOW` → Baixa, `MID` → Média, `HIGH` → Alta). Velocidades desconhecidas exibem o nome cru, capitalizado.
- Rodapé: "Atualizado há N s", ou a mensagem de erro atual, + botão ⟳ + ⚙.
- Primeira execução / token inválido: painel mostra `SetupView` no lugar dos controles.

## 5. Fluxo de dados

1. **Bootstrap:** carrega credenciais. Sem token ou `deviceId` → `.needsSetup`. Com credenciais → busca profile (uma vez por execução) e estado, e inicia o polling.
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
| Token inválido (`1103`, `1218`, `1302` ou HTTP 401) | Apaga o token; `phase = .needsSetup` com mensagem "Token inválido, gere um novo". |
| Limite de chamadas (`1306`) | Rodapé com aviso. Intervalo de polling dobra a cada ocorrência (5 → 10 → 20 → 30 min, teto 30). Volta a 5 min no primeiro sucesso. |
| Aparelho offline (`1222`) | Rodapé "Ar-condicionado offline". Ícone esmaecido. |
| Comando recusado (`1102`, `2205`, `2207`, `2208`, `2301`–`2305`) | Rollback do estado otimista + mensagem da LG no rodapé. |

O token nunca é logado nem exibido depois de salvo.

## 7. Build, testes e instalação

### Passo 0 — dados reais

Antes da UI: implementar `ThinQClient` + `lgac-dump`, rodar com um token real e conferir o JSON contra as fixtures. Ajustar `ACCapabilities`/`ACCommand` aos valores reais (modos, velocidades, booleanos, cancelamento de timer, luz do visor). O token nunca é commitado; fixtures derivadas de um aparelho real têm `deviceId`, alias e modelo anonimizados.

### Testes automatizados (Swift Testing)

Com só as Command Line Tools, `swift test` não encontra o módulo `Testing` sozinho; `scripts/test.sh` adiciona os caminhos do framework quando necessário.

- `JSONValue`: decode/encode de ida e volta.
- `ThinQError`: cada código mapeia para o caso certo.
- `ThinQClient`: URL por país, todos os headers, `x-conditional-control` só no control, decodificação de `response`, mapeamento de erros. Usa transporte stub; nunca chama a LG.
- `ACCapabilities` / `ACState`: parse de uma fixture completa e de uma fixture "modelo básico" (sem swing horizontal, sem luz do visor, sem timer).
- `ACCommand`: cada caso gera o payload esperado, incluindo os candidatos de temperatura, `windStrengthDetail` vs `windStrength`, `timer` vs `sleepTimer`, luz enum vs boolean.
- `ACController`: otimista + rollback, debounce de temperatura (vários cliques → um comando), backoff de polling e reset, transição para `.needsSetup` em token inválido, escolha de aparelho. Usa cliente falso, store em memória e `sleep` controlado.

### Teste manual (aparelho real)

Checklist: power on/off; `−`/`+` (incluindo cliques rápidos); cada modo; cada velocidade; swing vertical e horizontal; timer (definir, ver contagem, cancelar); luz do visor; ícone muda com modo e power; mudança feita no app ThinQ aparece ao reabrir o painel; desconectar Wi-Fi do Mac mostra "Sem conexão".

### Empacotamento

`build-app.sh`:

1. `swift build -c release --product LGACApp`
2. monta `dist/LG AC.app/Contents/{MacOS,Resources}` com `Resources/Info.plist` (`CFBundleIdentifier com.local.lgac`, `LSUIElement = true`, `LSMinimumSystemVersion 14.0`) e `Resources/AppIcon.icns`
3. `codesign --force --sign - "dist/LG AC.app"` (ad-hoc)
4. copia para `/Applications`

`AppIcon.icns` é gerado por `scripts/make-icon.swift` (SF Symbol sobre fundo arredondado) + `iconutil`, e commitado.

"Abrir ao iniciar sessão" via `SMAppService.mainApp.register()`/`unregister()`.

### README

Como gerar o PAT (link, escopos), como compilar/instalar, como rodar os testes, como usar `lgac-dump`, solução de problemas.

## 8. Fora do escopo

- Múltiplos aparelhos controlados ao mesmo tempo.
- Eventos em tempo real (MQTT / AWS IoT).
- Widget/Controle da Central de Controle (WidgetKit — exige Xcode e perfil de desenvolvedor).
- App Store, notarização, distribuição de binários.
- Agendamentos, histórico de consumo, modos especiais (jet, energy saving, air clean).
