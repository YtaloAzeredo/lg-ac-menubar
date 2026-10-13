# LG AC Menu Bar

Controle o seu ar-condicionado LG direto da barra de menu do macOS, sem pegar o celular.

> **Status: em desenvolvimento.** O design está definido em [`docs/superpowers/specs/2026-10-13-lg-ac-menubar-design.md`](docs/superpowers/specs/2026-10-13-lg-ac-menubar-design.md). As instruções de build e uso abaixo descrevem como o app vai funcionar quando a implementação estiver pronta.

```
  🔋  📶  🔊   ❄   seg 12:30
               │
               ▼
┌────────────────────────────────────┐
│  Ar do Quarto               ( ⏻ )  │
│  Ambiente 26°                      │
├────────────────────────────────────┤
│         ( − )    23°    ( + )      │
├────────────────────────────────────┤
│  Modo                              │
│  [❄ Frio][💧 Seco][🌀 Vent][A Auto]  │
│  Ventilador                        │
│  [Baixa][Média][Alta][Auto]        │
├────────────────────────────────────┤
│  ↕  Swing vertical         [ ●  ]  │
│  ↔  Swing horizontal       [  ○ ]  │
│  💡 Luz do visor           [ ●  ]  │
│  ⏲  Desligar em     [ 2 h      ▾]  │
├────────────────────────────────────┤
│  Atualizado há 12s    ⟳        ⚙  │
└────────────────────────────────────┘
```

## Sumário

- [Recursos](#recursos)
- [Como funciona](#como-funciona)
- [Requisitos](#requisitos)
- [1. Gerar o token da LG](#1-gerar-o-token-da-lg)
- [2. Compilar e instalar](#2-compilar-e-instalar)
- [3. Primeira configuração](#3-primeira-configuração)
- [Uso](#uso)
- [Atualização do estado](#atualização-do-estado)
- [Solução de problemas](#solução-de-problemas)
- [Privacidade e segurança](#privacidade-e-segurança)
- [Desenvolvimento](#desenvolvimento)
- [Arquitetura](#arquitetura)
- [Fora do escopo](#fora-do-escopo)
- [Referências](#referências)

## Recursos

- **Ícone na barra de menu** que acompanha o modo (❄ frio, ☀ quente, 💧 desumidificar, 🌀 ventilar) — cheio quando ligado, esmaecido quando desligado ou offline.
- **Painel no estilo Central de Controle** ao clicar no ícone.
- **Liga/desliga.**
- **Temperatura alvo** com `−`/`+`, respeitando a faixa e o passo do seu modelo (0,5° ou 1°). Cliques rápidos viram um único comando.
- **Temperatura ambiente** medida pelo aparelho (se o modelo informar).
- **Modo de operação**: frio, seco, ventilar, auto, quente — só os que o seu modelo suporta.
- **Velocidade do ventilador**: baixa, média, alta, auto — conforme o modelo.
- **Swing vertical e horizontal.**
- **Timer para desligar**: 30 min, 1 h, 2 h, 3 h, 4 h, 6 h, 8 h, com contagem regressiva.
- **Luz do visor** liga/desliga.
- **Abre ao iniciar sessão** (opcional).
- **Nativo e leve**: Swift + SwiftUI, sem dependências de terceiros, sem ícone no Dock.

O app lê as capacidades do aparelho na API da LG. Controles que o seu modelo não tem simplesmente não aparecem.

## Como funciona

Os ar-condicionados LG com Wi-Fi não aceitam controle direto pela rede local: tudo passa pela nuvem **LG ThinQ**. O app usa a **API oficial LG ThinQ Connect** — a mesma usada pela integração oficial do Home Assistant — autenticada com um *Personal Access Token* (PAT) que você gera no site da LG.

```
 ┌──────────────┐   HTTPS (REST)    ┌────────────────────────┐    Wi-Fi    ┌──────────────┐
 │  LG AC.app   │ ────────────────▶ │  LG ThinQ Connect API  │ ──────────▶ │  Ar-condic.  │
 │ (barra menu) │ ◀──────────────── │  api-aic.lgthinq.com   │ ◀────────── │     LG       │
 └──────────────┘   estado / ok     └────────────────────────┘             └──────────────┘
```

Consequência prática: **o Mac precisa estar com internet** para controlar o ar, mesmo que os dois estejam na mesma rede.

## Requisitos

- macOS 14 (Sonoma) ou mais novo.
- Swift 6 — basta as Command Line Tools (`xcode-select --install`); **o Xcode não é necessário**.
- Ar-condicionado LG com Wi-Fi, **já cadastrado e funcionando no app LG ThinQ** do celular.
- Conta LG ThinQ (a mesma do app do celular).

## 1. Gerar o token da LG

1. Acesse **https://connect-pat.lgthinq.com** e entre com a mesma conta LG do app ThinQ.
2. Clique em **ADD NEW TOKEN** (ou "Criar novo token").
3. Dê um nome, por exemplo `mac-menubar`.
4. Marque as permissões:
   - **Permission to view all devices** (listar dispositivos)
   - **Permission to view all device statuses** (ver estado)
   - **Permission to control all devices** (controlar)
5. Crie o token e **copie o valor**. Guarde-o em local seguro — trate como uma senha.

> Nunca cole o token em issues, commits, chats ou screenshots. Se ele vazar, revogue-o no mesmo site e gere outro.

## 2. Compilar e instalar

```bash
git clone https://github.com/YtaloAzeredo/lg-ac-menubar.git
cd lg-ac-menubar
./build-app.sh
```

O script:

1. compila em modo release (`swift build -c release`);
2. monta o bundle `LG AC.app` com `Info.plist` (`LSUIElement`, sem ícone no Dock) e ícone;
3. assina ad-hoc (`codesign --sign -`), suficiente para uso pessoal;
4. copia para `/Applications`.

Depois, abra **LG AC** em `/Applications` (ou pelo Spotlight).

> Na primeira execução o macOS pode avisar que o app é de um desenvolvedor não identificado. Abra **Ajustes do Sistema → Privacidade e Segurança** e clique em **Abrir Mesmo Assim**.

## 3. Primeira configuração

1. Clique no ícone ❄ na barra de menu.
2. Cole o token no campo **Token**.
3. Escolha o país (padrão: **Brasil**). O país define o servidor da LG usado (Brasil → região AIC).
4. Clique em **Conectar**. O app encontra o seu ar-condicionado automaticamente e passa a mostrar os controles.

O token fica guardado no **Keychain** do macOS. Para trocar, use **⚙ → Trocar token…**.

## Uso

| Ação | Como |
|---|---|
| Ligar/desligar | Botão ⏻ no topo do painel |
| Mudar temperatura | `−` / `+` (o comando é enviado 0,6 s após o último clique) |
| Mudar modo | Botões de modo |
| Velocidade do ventilador | Botões de velocidade |
| Swing | Chaves "Swing vertical" / "Swing horizontal" |
| Luz do visor | Chave "Luz do visor" |
| Timer para desligar | Menu "Desligar em" (escolha "Desativado" para cancelar) |
| Atualizar agora | Botão ⟳ no rodapé |
| Abrir ao iniciar sessão / sair | Menu ⚙ no rodapé |

Com o ar desligado, os controles ficam esmaecidos — só o botão ⏻ funciona.

A tela muda na hora em que você clica; se a LG recusar o comando, o valor volta ao anterior e a mensagem aparece no rodapé.

## Atualização do estado

A API da LG é consultada (não há push em tempo real). O app atualiza o estado:

- ao abrir o painel (se a última leitura tiver mais de 5 s);
- ~1,5 s depois de cada comando, para confirmar;
- a cada **5 minutos** em segundo plano, para manter o ícone correto;
- quando o Mac acorda do repouso;
- ao clicar em ⟳.

Mudanças feitas pelo controle remoto ou pelo app do celular aparecem em até 5 minutos, ou imediatamente ao abrir o painel.

## Solução de problemas

| Mensagem / sintoma | Causa provável | O que fazer |
|---|---|---|
| "Token inválido, gere um novo" | Token revogado, expirado ou colado errado (códigos LG `1103`, `1218`, `1302`) | Gere outro token (passo 1) e use **⚙ → Trocar token…** |
| "Sem conexão" | Mac sem internet ou API da LG fora do ar | Verifique a internet; o app tenta de novo sozinho |
| "Ar-condicionado offline" | O aparelho perdeu o Wi-Fi (código `1222`) | Confira se ele aparece online no app ThinQ do celular |
| "Limite de chamadas atingido" | Muitas requisições à API (código `1306`) | Nada — o app espaça as consultas automaticamente (até 30 min) e volta ao normal sozinho |
| Comando volta ao valor anterior | O aparelho recusou o comando no estado atual (códigos `2205`, `2207`, `2301`–`2305`) | Ex.: alguns modos não aceitam mudar a temperatura ou o ventilador; veja a mensagem no rodapé |
| "Nenhum ar-condicionado encontrado" | Token sem permissão ou país errado | Confira as permissões do token e o país escolhido |
| Um controle não aparece | O seu modelo não suporta esse recurso | Esperado — o app só mostra o que o profile do aparelho declara |

## Privacidade e segurança

- O token é salvo **somente no Keychain** do macOS e nunca é escrito em log ou arquivo.
- O app conversa apenas com `api-*.lgthinq.com` via HTTPS. Nenhum dado é enviado a terceiros.
- Configurações não sensíveis (id do aparelho, país, client-id) ficam no `UserDefaults`.
- As fixtures de teste no repositório têm id de aparelho e nome anonimizados.

## Desenvolvimento

### Rodar os testes

```bash
swift test
```

Os testes usam **Swift Testing** (incluído nas Command Line Tools) e **nunca chamam a API real**: o cliente HTTP é testado com stub de rede e o controlador com um cliente falso.

### Rodar o app sem empacotar

```bash
swift run LGACApp
```

### `lgac-dump`: inspecionar o seu aparelho

Ferramenta de desenvolvimento que salva em JSON a lista de aparelhos, o profile (capacidades) e o estado atual do seu ar-condicionado — útil para entender quais valores o seu modelo aceita e para gerar fixtures de teste.

```bash
# lê o token sem ecoar na tela e sem deixar no histórico do shell
read -s "LGAC_TOKEN?Token LG: " && export LGAC_TOKEN
export LGAC_COUNTRY=BR
swift run lgac-dump ./dump
ls ./dump   # devices.json  profile.json  state.json
```

> Os arquivos de `./dump` contêm o id real do aparelho. Não faça commit deles sem anonimizar.

## Arquitetura

Pacote Swift sem dependências externas, dividido em três alvos:

| Alvo | Tipo | Responsabilidade |
|---|---|---|
| `LGACCore` | biblioteca | Cliente da API, modelos, regras — sem UI, 100% testável |
| `LGACApp` | executável | Interface SwiftUI (`MenuBarExtra`) |
| `lgac-dump` | executável (dev) | Despeja JSON real do aparelho |

Principais componentes de `LGACCore`:

- **`ThinQClient`** — chamadas HTTP à API (`GET /devices`, `GET /devices/{id}/profile`, `GET /devices/{id}/state`, `POST /devices/{id}/control`), headers de autenticação e tradução dos códigos de erro da LG em `ThinQError`.
- **`ThinQRegion`** — país → servidor (`kic`, `aic`, `eic`).
- **`ACCapabilities`** — interpreta o profile: modos, velocidades, faixa de temperatura, swing, timer, luz do visor.
- **`ACState`** — estado atual do aparelho.
- **`ACCommand`** — cada ação do usuário e o JSON correspondente. Ex.: luz do visor → `{"display": {"light": "OFF"}}`.
- **`ACController`** — o "cérebro" observado pela UI: atualização otimista com rollback, debounce da temperatura, polling e backoff.
- **`CredentialStore`** — token no Keychain, preferências no `UserDefaults`.

```
lg-ac-menubar/
├── Package.swift
├── Sources/
│   ├── LGACCore/
│   ├── LGACApp/
│   └── lgac-dump/
├── Tests/LGACCoreTests/
│   └── Fixtures/
├── Resources/
├── build-app.sh
└── docs/superpowers/specs/   # design detalhado
```

O design completo, com payloads, formato do profile e tabela de erros, está em [`docs/superpowers/specs/2026-10-13-lg-ac-menubar-design.md`](docs/superpowers/specs/2026-10-13-lg-ac-menubar-design.md).

## Fora do escopo

- Controlar vários aparelhos.
- Atualização em tempo real via MQTT (exige registro de certificados na AWS IoT da LG).
- Widget/Controle dentro da Central de Controle (exige Xcode e perfil de desenvolvedor Apple).
- Distribuição pela App Store / notarização.
- Agendamentos, histórico de consumo e modos especiais (jet, economia de energia, purificação).

## Referências

- [LG ThinQ Connect — gerar Personal Access Token](https://connect-pat.lgthinq.com)
- [pythinqconnect — SDK oficial da LG (Apache-2.0)](https://github.com/thinq-connect/pythinqconnect), usado como referência dos endpoints, headers e payloads
- [Integração LG ThinQ do Home Assistant](https://www.home-assistant.io/integrations/lg_thinq)

---

Projeto pessoal, sem afiliação com a LG Electronics. "LG" e "ThinQ" são marcas da LG Electronics.
