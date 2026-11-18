# LG AC

Controle o seu ar-condicionado LG direto da barra de menu do macOS, da bandeja do Windows ou da bandeja do Linux, sem pegar o celular.

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

O desenho mostra o painel no macOS; no Windows e no Linux ele tem os mesmos controles e abre ao clicar no ícone da bandeja (no Windows, ao lado do relógio; no Linux, no canto da tela onde fica a barra do sistema).

## Sumário

- [Recursos](#recursos)
- [Como funciona](#como-funciona)
- [Requisitos](#requisitos)
- [1. Gerar o token da LG](#1-gerar-o-token-da-lg)
- [2. Instalar](#2-instalar)
  - [macOS: compilar e instalar](#macos-compilar-e-instalar)
  - [Windows: baixar o executável](#windows-baixar-o-executável)
  - [Windows: compilar e instalar](#windows-compilar-e-instalar)
  - [Linux: baixar o executável](#linux-baixar-o-executável)
  - [Linux: compilar e instalar](#linux-compilar-e-instalar)
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

- **Ícone na barra de menu (macOS) ou na bandeja (Windows e Linux)** que acompanha o modo (❄ frio, ☀ quente, 💧 desumidificar, 🌀 ventilar) — cheio quando ligado, esmaecido quando desligado ou offline.
- **Painel no estilo Central de Controle** ao clicar no ícone.
- **Liga/desliga.**
- **Temperatura alvo** com `−`/`+`, respeitando a faixa e o passo do seu modelo (0,5° ou 1°). Cliques rápidos viram um único comando.
- **Temperatura ambiente** medida pelo aparelho (se o modelo informar).
- **Modo de operação**: frio, seco, ventilar, auto, quente — só os que o seu modelo suporta.
- **Velocidade do ventilador**: baixa, média, alta, auto — conforme o modelo.
- **Swing vertical e horizontal.**
- **Timer para desligar**: 30 min, 1 h, 2 h, 3 h, 4 h, 6 h, 8 h, com contagem regressiva.
- **Luz do visor** liga/desliga.
- **Abre ao iniciar sessão** (opcional; no Windows, "Iniciar com o Windows"; no Linux, "Iniciar com o sistema").
- **Nativo e leve**: no macOS, Swift + SwiftUI, sem ícone no Dock; no Windows, C# + WPF (.NET 10); no Linux, C# + Avalonia (.NET 10). Os apps de macOS e Windows não usam dependências de terceiros; o de Linux usa Avalonia e Tmds.DBus.Protocol (veja [Dependências do app de Linux](#dependências-do-app-de-linux)).

O app lê as capacidades do aparelho na API da LG. Controles que o seu modelo não tem simplesmente não aparecem.

## Como funciona

Os ar-condicionados LG com Wi-Fi não aceitam controle direto pela rede local: tudo passa pela nuvem **LG ThinQ**. O app usa a **API oficial LG ThinQ Connect** — a mesma usada pela integração oficial do Home Assistant — autenticada com um *Personal Access Token* (PAT) que você gera no site da LG.

```
 ┌──────────────┐   HTTPS (REST)    ┌────────────────────────┐    Wi-Fi    ┌──────────────┐
 │    LG AC     │ ────────────────▶ │  LG ThinQ Connect API  │ ──────────▶ │  Ar-condic.  │
 │(mac/win/lin) │ ◀──────────────── │  api-aic.lgthinq.com   │ ◀────────── │     LG       │
 └──────────────┘   estado / ok     └────────────────────────┘             └──────────────┘
```

Consequência prática: **o computador precisa estar com internet** para controlar o ar, mesmo que os dois estejam na mesma rede.

## Requisitos

Em qualquer sistema:

- Ar-condicionado LG com Wi-Fi, **já cadastrado e funcionando no app LG ThinQ** do celular.
- Conta LG ThinQ (a mesma do app do celular).

No macOS:

- macOS 14 (Sonoma) ou mais novo.
- Swift 6 — basta as Command Line Tools (`xcode-select --install`); **o Xcode não é necessário**.

No Windows:

- Windows 11 (x64 ou ARM64). O Windows 10 deve funcionar, mas sem garantia.
- Para usar o executável pronto, nada além disso: **não é preciso instalar o .NET**.
- Para compilar você mesmo: Git e o SDK do .NET 10 (a versão mínima está em `windows/global.json`; baixe em [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)).

No Linux:

- Uma distribuição de desktop com glibc, em x64 ou ARM64: Ubuntu 22.04 ou mais novo, Debian 12 ou mais novo, Fedora, Arch e derivadas.
- Uma sessão gráfica X11 ou Wayland (no Wayland o app roda via XWayland). Não funciona sem ambiente gráfico nem em distribuições com musl, como o Alpine.
- Para usar o executável pronto, nada além das bibliotecas de um desktop comum: **não é preciso instalar o .NET**.
- Para compilar você mesmo: Git e o SDK do .NET 10 (a versão mínima está em `linux/global.json`; baixe em [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)).

## 1. Gerar o token da LG

1. Acesse **https://connect-pat.lgthinq.com** e entre com a mesma conta LG do app ThinQ.
2. Clique em **ADD NEW TOKEN** (ou "Criar novo token").
3. Dê um nome, por exemplo `mac-menubar`, `windows-tray` ou `linux-tray`.
4. Marque as permissões:
   - **Permission to view all devices** (listar dispositivos)
   - **Permission to view all device statuses** (ver estado)
   - **Permission to control all devices** (controlar)
5. Crie o token e **copie o valor**. Guarde-o em local seguro — trate como uma senha.

> Nunca cole o token em issues, commits, chats ou screenshots. Se ele vazar, revogue-o no mesmo site e gere outro.

## 2. Instalar

### macOS: compilar e instalar

```bash
git clone https://github.com/YtaloAzeredo/lg-ac-menubar.git
cd lg-ac-menubar
./macos/build-app.sh
```

O script:

1. compila em modo release (`swift build -c release`);
2. monta o bundle `LG AC.app` com `Info.plist` (`LSUIElement`, sem ícone no Dock) e ícone;
3. assina com o certificado local `LG AC Dev`, se ele existir (veja abaixo), ou ad-hoc (`codesign --sign -`);
4. move para `/Applications` (não sobra cópia em `dist/`) e abre o app.

O app não abre janela nem aparece no Dock: ele existe só como um ícone na barra de menu, no alto da tela (veja o passo 3).

Para só gerar o bundle em `dist/LG AC.app`, sem instalar: `./macos/build-app.sh --no-install`.

**Recomendado se você recompila com frequência:** crie uma vez o certificado local de assinatura.

```bash
./macos/scripts/make-signing-cert.sh   # pede a senha do Mac uma vez
./macos/build-app.sh
```

Com a assinatura ad-hoc, cada build parece um app diferente para o Keychain, que volta a pedir a senha do Mac depois de cada reinstalação. Assinado sempre com o mesmo certificado, o app é reconhecido entre builds. Depois de criar o certificado, o Keychain ainda pergunta uma última vez (clique em **Sempre Permitir**). O certificado é autoassinado e só vale neste Mac. Para removê-lo: `security delete-identity -c "LG AC Dev"`.

> Na primeira execução o macOS pode avisar que o app é de um desenvolvedor não identificado. Abra **Ajustes do Sistema → Privacidade e Segurança** e clique em **Abrir Mesmo Assim**.

### Windows: baixar o executável

1. Abra a [página de Releases do repositório](https://github.com/YtaloAzeredo/lg-ac-menubar/releases) e, na versão mais recente, baixe o arquivo da sua arquitetura:
   - `LG-AC-windows-x64.exe` para PCs com processador Intel ou AMD (a maioria);
   - `LG-AC-windows-arm64.exe` para PCs com processador ARM (por exemplo, Copilot+ PCs com Snapdragon). Para conferir, abra **Configurações → Sistema → Sobre** e veja "Tipo de sistema".
2. Guarde o arquivo numa pasta que você não vá apagar (por exemplo, `Documentos\LG AC`) e dê dois cliques nele. O executável roda de onde estiver. Se você ativar "Iniciar com o Windows", essa opção aponta para o caminho atual do arquivo; se você o mover, o app corrige o caminho sozinho na próxima vez que for aberto.
3. O executável **não é assinado**, então o SmartScreen pode mostrar "O Windows protegeu o computador". Clique em **Mais informações** e depois em **Executar assim mesmo**.

O primeiro arranque é um pouco mais lento: o executável extrai bibliotecas para uma pasta temporária. Os seguintes são mais rápidos.

### Windows: compilar e instalar

Num PowerShell, com o Git e o SDK do .NET 10 instalados:

```powershell
git clone https://github.com/YtaloAzeredo/lg-ac-menubar.git
cd lg-ac-menubar
.\windows\build-app.ps1
```

O script:

1. compila e publica o app em modo release, como um único executável autocontido, em `windows\dist\LG-AC-windows-x64.exe` (ou `-arm64`);
2. fecha o LG AC instalado, se estiver aberto (se estiver usando um executável baixado, feche-o antes pelo ⚙ → **Sair**);
3. copia o executável para `%LOCALAPPDATA%\Programs\LG AC\LG AC.exe`;
4. cria o atalho **LG AC** no Menu Iniciar;
5. abre o app.

Parâmetros:

| Parâmetro | Efeito |
|---|---|
| `-NoInstall` | só gera o executável em `windows\dist\`, sem instalar nem abrir |
| `-Runtime win-x64` ou `-Runtime win-arm64` | escolhe a arquitetura do executável; o padrão é a do seu PC. Só é possível instalar a arquitetura do próprio PC, então outra exige `-NoInstall` |

Se o PowerShell recusar rodar o script ("a execução de scripts foi desabilitada neste sistema"), rode-o ignorando a política só neste comando:

```powershell
powershell -ExecutionPolicy Bypass -File .\windows\build-app.ps1
```

ou permita scripts locais para o seu usuário (uma vez):

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

### Linux: baixar o executável

1. Descubra a arquitetura do seu computador com `uname -m`: `x86_64` é x64; `aarch64` (ou `arm64`) é ARM64.
2. Abra a [página de Releases do repositório](https://github.com/YtaloAzeredo/lg-ac-menubar/releases) e, na versão mais recente, baixe o arquivo da sua arquitetura:
   - `LG-AC-linux-x64` para processadores Intel ou AMD (a maioria);
   - `LG-AC-linux-arm64` para processadores ARM.
3. Dê permissão de execução e abra o app (os arquivos baixados vêm sem ela):

```bash
chmod +x LG-AC-linux-x64
./LG-AC-linux-x64
```

O executável roda de qualquer pasta; uma sugestão é guardá-lo em `~/.local/bin`. Se você ativar "Iniciar com o sistema", essa opção aponta para o caminho em uso; se você mover o arquivo, o app corrige o caminho sozinho na próxima vez que for aberto.

Para conferir a versão sem abrir a interface: `./LG-AC-linux-x64 --version`.

### Linux: compilar e instalar

Com o Git e o SDK do .NET 10 instalados:

```bash
git clone https://github.com/YtaloAzeredo/lg-ac-menubar.git
cd lg-ac-menubar
./linux/build-app.sh
```

O script:

1. compila e publica o app em modo release, como um único executável autocontido, em `linux/dist/LG-AC-linux-x64` (ou `-arm64`);
2. fecha o LG AC instalado, se estiver aberto;
3. instala o executável em `~/.local/bin/lg-ac` e o ícone em `~/.local/share/icons/hicolor/256x256/apps/lg-ac.png` (respeitando `XDG_BIN_HOME` e `XDG_DATA_HOME`, se definidas);
4. cria o lançador **LG AC** em `~/.local/share/applications/lg-ac.desktop`, que aparece no menu de aplicativos;
5. abre o app.

Opções:

| Opção | Efeito |
|---|---|
| `--no-install` | só gera o executável em `linux/dist/`, sem instalar nem abrir (também funciona fora do Linux) |
| `--runtime linux-x64` ou `--runtime linux-arm64` | escolhe a arquitetura do executável; o padrão é a do seu computador. Só é possível instalar a arquitetura do próprio computador, então outra exige `--no-install` |

## 3. Primeira configuração

### macOS

1. Clique no ícone de termômetro esmaecido na barra de menu (quando o ar está ligado, ele passa a mostrar o ícone do modo atual). Ícones novos entram **à esquerda** dos que já estão na barra. Se não encontrar, veja "O ícone não aparece na barra de menu" em [Solução de problemas](#solução-de-problemas).
2. Cole o token no campo **Token**.
3. Escolha o país (padrão: **Brasil**). O país define o servidor da LG usado (Brasil → região AIC).
4. Clique em **Conectar**. O app encontra o seu ar-condicionado automaticamente e passa a mostrar os controles. Se a conta tiver mais de um ar-condicionado, aparece uma lista para você escolher qual controlar.

O token fica guardado no **Keychain** do macOS. Para trocar, use **⚙ → Trocar token…**. Para controlar outro ar-condicionado da mesma conta, clique na seta **‹** no canto superior esquerdo do painel (ou **⚙ → Trocar aparelho…**). A troca não pede o token nem a senha do Mac.

### Windows

1. Abra o app. O ícone aparece na bandeja do sistema, ao lado do relógio. O Windows 11 costuma esconder ícones novos no menu **^** (ícones ocultos); veja como deixá-lo sempre visível em "O ícone não aparece" em [Solução de problemas](#solução-de-problemas).
2. Clique com o botão esquerdo no ícone para abrir o painel e cole o token no campo **Token**.
3. Escolha o país (padrão: **Brasil**). O país define o servidor da LG usado (Brasil → região AIC).
4. Clique em **Conectar**. O app encontra o seu ar-condicionado automaticamente e passa a mostrar os controles. Se a conta tiver mais de um ar-condicionado, aparece a lista **Escolha o ar-condicionado** para você escolher qual controlar.

O token fica guardado no **Gerenciador de Credenciais** do Windows. Para trocar, use **⚙ → Trocar token…**. Para controlar outro ar-condicionado da mesma conta, clique na seta **‹** no canto superior esquerdo do painel (ou **⚙ → Trocar aparelho…**) e, se mudar de ideia, **Cancelar** volta ao aparelho atual.

### Linux

1. Abra o app. Com bandeja (KDE, XFCE, Cinnamon, Ubuntu), o ícone aparece na bandeja do sistema; sem bandeja (GNOME puro), o app abre como uma janela normal. Veja [Particularidades do Linux](#particularidades-do-linux).
2. Clique no ícone (ou use a janela) e cole o token no campo **Token**.
3. Escolha o país (padrão: **Brasil**). O país define o servidor da LG usado (Brasil → região AIC).
4. Clique em **Conectar**. O app encontra o seu ar-condicionado automaticamente e passa a mostrar os controles. Se a conta tiver mais de um ar-condicionado, aparece a lista **Escolha o ar-condicionado** para você escolher qual controlar.

O token fica guardado no serviço de segredos do sistema (GNOME Keyring ou KWallet). Para trocar, use **⚙ → Trocar token…**. Para controlar outro ar-condicionado da mesma conta, clique na seta **‹** no canto superior esquerdo do painel (ou **⚙ → Trocar aparelho…**) e, se mudar de ideia, **Cancelar** volta ao aparelho atual.

## Uso

Os controles são os mesmos nos três sistemas.

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
| Trocar de aparelho | Seta **‹** no canto superior esquerdo (ou **⚙ → Trocar aparelho…**) e escolha na lista ("Cancelar" volta ao atual) |
| Abrir ao iniciar sessão / sair | Menu ⚙ no rodapé |

Com o ar desligado, os controles ficam esmaecidos — só o botão ⏻ funciona.

A tela muda na hora em que você clica; se a LG recusar o comando, o valor volta ao anterior e a mensagem aparece no rodapé.

### Particularidades do Windows

- O ícone da bandeja mostra o modo atual; passe o mouse para ver o nome do aparelho.
- **Clique esquerdo** no ícone abre e fecha o painel. O painel também fecha com **Esc** ou ao clicar fora dele.
- **Clique direito** no ícone abre um menu com **Sair**.
- O menu **⚙** tem **Trocar aparelho…** (com o aparelho conectado), **Trocar token…**, **Iniciar com o Windows** (marcável) e **Sair**.
- Abrir o app de novo com ele já em execução não cria um segundo ícone.

### Particularidades do Linux

O app tem dois modos, escolhidos sozinho ao abrir, conforme o seu ambiente gráfico ofereça ou não uma bandeja.

**Com bandeja** (KDE, XFCE, Cinnamon, Ubuntu):

- O ícone da bandeja mostra o modo atual; passe o mouse para ver o nome do aparelho (no KDE e no Cinnamon).
- No KDE, XFCE e Cinnamon, um **clique** no ícone abre e fecha o painel; no Ubuntu e no GNOME com a extensão, um clique abre o menu (use **Abrir painel**) e o **clique duplo** abre o painel. O painel também fecha com **Esc** ou ao clicar fora dele.
- O menu do ícone (botão direito, ou como o seu ambiente abrir menus) tem **Abrir painel** e **Sair**.
- O painel abre no canto da tela onde fica a barra do sistema, e não colado ao ícone: o Linux não informa a posição do ícone ao app.
- No KDE e no XFCE, ao abrir junto com a sessão, o app espera até 15 s pela bandeja antes de abrir como janela.

**Sem bandeja** (GNOME puro, como no Fedora Workstation e no Debian):

- O app abre como uma janela normal, com entrada na barra de tarefas. **Fechar a janela encerra o app.**
- Para ter o ícone na bandeja do GNOME, instale e ative a extensão **AppIndicator and KStatusNotifierItem Support** (pacote `gnome-shell-extension-appindicator` na maioria das distribuições) e abra o app de novo. O app confere a bandeja ao abrir, então a extensão precisa estar ativa antes.

Nos dois modos:

- O menu **⚙** tem **Trocar aparelho…** (com o aparelho conectado), **Trocar token…**, **Iniciar com o sistema** (marcável) e **Sair**.
- Abrir o app de novo com ele já em execução mostra o painel da instância existente, sem criar outra.
- O app segue o tema claro ou escuro do sistema (quando o ambiente informa o tema pelo portal do desktop, como GNOME e KDE).

## Atualização do estado

A API da LG é consultada (não há push em tempo real). O app atualiza o estado:

- ao abrir o painel (se a última leitura tiver mais de 5 s);
- ~1,5 s depois de cada comando, para confirmar;
- a cada **5 minutos** em segundo plano, para manter o ícone correto;
- quando o computador acorda do repouso (no Windows e no Linux, cerca de 5 s depois de retomar);
- ao clicar em ⟳.

Mudanças feitas pelo controle remoto ou pelo app do celular aparecem em até 5 minutos, ou imediatamente ao abrir o painel.

## Solução de problemas

### macOS

| Mensagem / sintoma | Causa provável | O que fazer |
|---|---|---|
| O ícone não aparece na barra de menu | A barra está cheia. Em MacBooks com notch, os ícones que não cabem ficam escondidos atrás da câmera. Com mais de um monitor, o ícone pode estar visível só na barra do outro monitor | Segure **⌘** e arraste o ícone para a direita, perto do relógio (os ícones da direita têm prioridade), ou esconda ícones que você não usa em **Ajustes do Sistema → Barra de Menus**. Para conferir se o app está rodando: `pgrep -lx LGACApp` |
| "Token inválido, gere um novo" | Token revogado, expirado ou colado errado (códigos LG `1103`, `1218`, `1302`), ou falha passageira da LG | Gere outro token (passo 1) e cole na tela de configuração, que já aparece no painel. O token antigo continua guardado até você colar o novo: se foi só uma falha da LG, basta sair e abrir o app de novo |
| "Sem conexão" | Mac sem internet ou API da LG fora do ar | Verifique a internet; o app tenta de novo sozinho |
| "Ar-condicionado offline" | O aparelho perdeu o Wi-Fi (código `1222`) | Confira se ele aparece online no app ThinQ do celular |
| "Limite de chamadas atingido" | Muitas requisições à API (código `1306`) | Nada — o app espaça as consultas automaticamente (até 30 min) e volta ao normal sozinho |
| Comando volta ao valor anterior | O aparelho recusou o comando no estado atual (códigos `2205`, `2207`, `2301`–`2305`) | Ex.: alguns modos não aceitam mudar a temperatura ou o ventilador; veja a mensagem no rodapé |
| "Nenhum ar-condicionado encontrado" | Token sem permissão ou país errado | Confira as permissões do token e o país escolhido |
| Apareceu o aparelho errado ou offline | A conta tem mais de um ar-condicionado e foi escolhido o outro | Seta **‹** no canto superior esquerdo (também na tela de carregamento) e escolha o outro na lista |
| Um controle não aparece | O seu modelo não suporta esse recurso | Esperado — o app só mostra o que o profile do aparelho declara |
| macOS pede acesso ao Keychain depois de reinstalar | A assinatura ad-hoc muda a cada build, e o Keychain trata como outro app | Digite a senha do Mac e clique em **Sempre Permitir**. Para não acontecer de novo, rode uma vez `./macos/scripts/make-signing-cert.sh` (veja [macOS: compilar e instalar](#macos-compilar-e-instalar)) |

### Windows

As mensagens da LG que não aparecem abaixo ("Ar-condicionado offline", "Limite de chamadas atingido", comando recusado, "Nenhum ar-condicionado encontrado", aparelho errado, controle ausente) têm a mesma causa e a mesma solução da tabela do macOS.

| Mensagem / sintoma | Causa provável | O que fazer |
|---|---|---|
| O Windows bloqueou a abertura ("O Windows protegeu o computador") | O executável não é assinado e o SmartScreen não o conhece | Clique em **Mais informações** e depois em **Executar assim mesmo** |
| O PowerShell recusa rodar `build-app.ps1` | A política de execução do PowerShell bloqueia scripts | Rode `powershell -ExecutionPolicy Bypass -File .\windows\build-app.ps1` ou, uma vez, `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` |
| O ícone não aparece na bandeja | O Windows 11 esconde ícones novos no menu **^** (ícones ocultos) | Clique no **^** perto do relógio e arraste o ícone do LG AC para a barra, ou abra **Configurações → Personalização → Barra de tarefas → Outros ícones da bandeja do sistema** e ligue **LG AC** |
| "Token inválido, gere um novo" | Token revogado, expirado ou colado errado (códigos LG `1103`, `1218`, `1302`), ou falha passageira da LG | Gere outro token (passo 1) e cole na tela de configuração, que já aparece no painel. O token antigo continua guardado até você colar o novo: se foi só uma falha da LG, basta sair e abrir o app de novo |
| "Sem conexão" | PC sem internet ou API da LG fora do ar | Verifique a internet; o app tenta de novo sozinho |
| "Iniciar com o Windows" está marcado, mas o app não abre no login | O Windows ignora a opção do app se ele foi desativado na lista de inicialização | Reative o **LG AC** em **Configurações → Aplicativos → Inicialização** ou no **Gerenciador de Tarefas → Aplicativos de inicialização** |
| Abri o app duas vezes e só aparece um ícone | Esperado: o app permite uma única instância por usuário | Nada a fazer |

### Linux

As mensagens da LG que não aparecem abaixo ("Token inválido, gere um novo", "Sem conexão", "Ar-condicionado offline", "Limite de chamadas atingido", comando recusado, "Nenhum ar-condicionado encontrado", aparelho errado, controle ausente) têm a mesma causa e a mesma solução da tabela do macOS.

| Mensagem / sintoma | Causa provável | O que fazer |
|---|---|---|
| "Permissão negada" ao executar o arquivo baixado | O arquivo não tem permissão de execução | `chmod +x LG-AC-linux-x64` (ou `-arm64`) e execute de novo |
| O ícone não aparece (abre uma janela) | O ambiente não tem bandeja, como o GNOME puro sem extensão. O app percebe isso ao abrir e usa o modo janela | Use a janela, ou instale e ative a extensão **AppIndicator and KStatusNotifierItem Support** (`gnome-shell-extension-appindicator`) e abra o app de novo |
| O sistema pede uma senha ao conectar | O cofre de senhas (chaveiro) está trancado e o serviço pede a senha para guardar o token | Digite a senha do chaveiro. Se cancelar, aparece "Não foi possível salvar o token" e o token não é guardado: conecte de novo |
| "Não foi possível salvar o token" | O cofre recusou a gravação (pedido de senha cancelado ou serviço com problema) | Conecte de novo e informe a senha do chaveiro quando pedida |
| Não há GNOME Keyring nem KWallet (ou o serviço não tem chaveiro padrão) | Sem serviço de segredos, o app não tem onde guardar o token com segurança | Esperado: o token fica no arquivo `~/.config/lg-ac/token`, com permissão 600 (só o seu usuário lê). Instale um serviço de segredos se preferir o cofre |
| O painel abre num canto da tela, longe do ícone | O Linux não informa a posição do ícone da bandeja | Esperado: o painel abre no canto da tela onde fica a barra do sistema |
| O app não abre no login, e **Iniciar com o sistema** aparece desmarcado | A entrada de início automático foi desligada fora do app (por exemplo, no GNOME Tweaks ou nas Configurações). O app respeita essa escolha | Marque **Iniciar com o sistema** de novo no menu ⚙ |
| O app não abre em Alpine ou outra distribuição com musl | O executável exige glibc | Não suportado; use uma distribuição com glibc |
| O app não abre numa máquina sem ambiente gráfico | O app precisa de uma sessão X11 ou Wayland | Não suportado sem ambiente gráfico |
| Abri o app duas vezes e só aparece um ícone (ou uma janela) | Esperado: o app permite uma única instância por usuário; a segunda abertura só mostra o painel da primeira | Nada a fazer |

Para ver mensagens de erro de inicialização, abra o app por um terminal: elas saem no `stderr`.

## Privacidade e segurança

- No macOS, o token é salvo **somente no Keychain**; no Windows, **somente no Gerenciador de Credenciais**. Em nenhum dos dois ele é escrito em log ou arquivo. No Linux, ele fica no serviço de segredos do sistema (ou, se não houver um, num arquivo só seu; veja [No Linux](#no-linux)) e nunca em log.
- O app conversa apenas com `api-*.lgthinq.com` via HTTPS. Nenhum dado é enviado a terceiros.
- Configurações não sensíveis (id do aparelho, país, client-id) ficam no `UserDefaults` (macOS), no arquivo `%APPDATA%\LG AC\settings.json` (Windows) ou no arquivo `~/.config/lg-ac/settings.json` (Linux), que nunca contém o token.
- As fixtures de teste no repositório não contêm dados de nenhum aparelho real (ids, nomes e modelos são fictícios ou anonimizados).

### No Windows

- O token é uma credencial genérica chamada **LG AC**, visível em **Painel de Controle → Gerenciador de Credenciais → Credenciais do Windows**.
- `%APPDATA%\LG AC\settings.json` guarda só país, aparelho escolhido (id e apelido) e o identificador do app.
- **Para remover tudo:**
  1. Abra o menu **⚙** e desmarque **Iniciar com o Windows**; depois escolha **Sair**. Se o ⚙ não aparecer (tela de configuração), rode `reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v "LG AC" /f`.
  2. Apague a credencial **LG AC** no Gerenciador de Credenciais (ou use **⚙ → Trocar token…**, que também apaga o token guardado).
  3. Apague a pasta `%APPDATA%\LG AC`.
  4. Apague o executável baixado, ou, se instalou com o script, a pasta `%LOCALAPPDATA%\Programs\LG AC` e o atalho **LG AC** do Menu Iniciar.
  5. Se quiser, revogue o token em https://connect-pat.lgthinq.com.

### No Linux

- O token fica no serviço de segredos do sistema (Secret Service: GNOME Keyring ou KWallet), num item rotulado **LG AC**. Para ver, abra o **Senhas e Chaves** (Seahorse) no GNOME ou o **KWalletManager** no KDE.
- Se o sistema não tiver serviço de segredos, ou se o serviço não tiver um chaveiro padrão (comum em i3/sway), o token fica no arquivo `~/.config/lg-ac/token`, com permissão 600 (e a pasta com 700). Quando há um chaveiro disponível, o token vai para ele e esse arquivo é apagado na próxima vez que você conectar.
- `~/.config/lg-ac/settings.json` guarda só país, aparelho escolhido (id e apelido) e o identificador do app.
- A opção "Iniciar com o sistema" cria `~/.config/autostart/lg-ac.desktop`.
- Se `XDG_CONFIG_HOME` estiver definida, ela substitui `~/.config` nos três caminhos acima.
- O app não faz nenhuma comunicação além de `api-*.lgthinq.com`.
- **Para remover tudo:**
  1. Abra o menu **⚙** e desmarque **Iniciar com o sistema**; depois escolha **Sair**. Se o ⚙ não aparecer (tela de configuração), basta apagar o arquivo `~/.config/autostart/lg-ac.desktop`.
  2. Apague o item **LG AC** no Senhas e Chaves ou no KWalletManager (ou use **⚙ → Trocar token…**, que também apaga o token guardado).
  3. Apague a pasta `~/.config/lg-ac`.
  4. Apague o executável baixado, ou, se instalou com o script, o executável `~/.local/bin/lg-ac`, o lançador `~/.local/share/applications/lg-ac.desktop` e o ícone `~/.local/share/icons/hicolor/256x256/apps/lg-ac.png`.
  5. Se quiser, revogue o token em https://connect-pat.lgthinq.com.

## Desenvolvimento

O código de cada sistema fica na sua pasta (`macos/`, `windows/` e `linux/`).

### macOS

Os comandos `swift` abaixo devem ser rodados dentro de `macos/`; os scripts funcionam a partir de qualquer pasta.

#### Rodar os testes

```bash
./macos/scripts/test.sh
```

Os testes usam **Swift Testing** e **nunca chamam a API real**: o cliente HTTP é testado com um transporte stub e o controlador com um cliente falso.

Para os testes rodarem também **antes de cada push**, ative o hook do repositório (uma vez por clone):

```bash
git config core.hooksPath .githooks
```

Se algum teste falhar, o push é cancelado e os testes com erro aparecem no terminal. Numa emergência, `git push --no-verify` pula o hook. O hook roda só os testes do macOS.

> Com só as Command Line Tools instaladas (sem Xcode), `swift test` puro falha com `no such module 'Testing'`. O `macos/scripts/test.sh` detecta isso e passa os caminhos do framework ao compilador. Com o Xcode instalado, `swift test` funciona direto.

#### Rodar o app sem empacotar

```bash
cd macos
swift run LGACApp
```

Com `swift run`, a opção "Abrir ao iniciar sessão" não funciona (ela exige o `.app` instalado).

#### Regerar o ícone

```bash
cd macos
swift scripts/make-icon.swift
```

#### `lgac-dump`: inspecionar o seu aparelho

Ferramenta de desenvolvimento que salva em JSON a lista de aparelhos, o profile (capacidades) e o estado atual do seu ar-condicionado — útil para entender quais valores o seu modelo aceita e para gerar fixtures de teste.

```bash
# lê o token sem ecoar na tela e sem deixar no histórico do shell
read -s "LGAC_TOKEN?Token LG: " && export LGAC_TOKEN
export LGAC_COUNTRY=BR
cd macos
swift run lgac-dump ./dump
ls ./dump   # devices.json  profile.json  state.json
```

> Os arquivos de `./dump` contêm o id real do aparelho. Não faça commit deles sem anonimizar.

### Windows

Precisa do SDK do .NET 10 (veja [Requisitos](#requisitos)).

#### Rodar os testes

No Windows, roda os testes do núcleo e do app:

```powershell
.\windows\scripts\test.ps1
```

Os testes do núcleo (`LGAC.Core`) rodam em qualquer sistema (macOS, Linux ou Windows):

```bash
dotnet test windows/tests/LGAC.Core.Tests
```

Os testes **nunca chamam a API real**: o cliente HTTP é testado com um transporte stub e o controlador com um cliente falso. Os testes do app (`LGAC.App.Tests`) dependem do Windows (WPF, registro e Gerenciador de Credenciais).

#### Compilar o app fora do Windows

Só para conferir que compila (não dá para executar o WPF fora do Windows):

```bash
dotnet build windows/src/LGAC.App -p:EnableWindowsTargeting=true
```

### Linux

Precisa do SDK do .NET 10 (veja [Requisitos](#requisitos)). Os testes rodam em Linux e em macOS.

#### Rodar os testes

```bash
./linux/scripts/test.sh
```

O script roda `dotnet test` na solução do Linux (o núcleo `LGAC.Core` tem os próprios testes: `dotnet test windows/tests/LGAC.Core.Tests`). Os testes **nunca chamam a API real**. Os testes de integração com o D-Bus (serviço de segredos, bandeja, retomada de energia) só executam em Linux com um barramento de sessão; nos outros ambientes ficam como inconclusivos (`Inconclusive`), não como falha.

Os testes de tela rodam sem monitor, com o Avalonia Headless, e salvam capturas PNG das telas na pasta `captures/` ao lado dos binários de teste (`linux/tests/LGAC.Linux.Tests/bin/<configuração>/net10.0/captures`).

#### Rodar o app sem empacotar

```bash
dotnet run --project linux/src/LGAC.Linux
dotnet run --project linux/src/LGAC.Linux -- --version
dotnet run --project linux/src/LGAC.Linux -- --smoke
```

`--version` imprime a versão e sai. `--smoke` inicia a interface com dados de exemplo, sem acessar a rede nem o cofre, e sai com código 0 se tudo subiu (é o que o CI usa para provar que o executável inicia em cada distribuição). O `--smoke` precisa de uma sessão gráfica ou de um servidor X virtual (`xvfb-run`).

#### Testar o executável em várias distribuições

Precisa do Docker. Gere o executável e rode o script, que instala as dependências de um desktop mínimo em cada imagem (Ubuntu 22.04 e 24.04, Debian 12, Fedora e Arch) e roda `--version` e `--smoke` sem privilégios:

```bash
./linux/build-app.sh --no-install
./linux/scripts/smoke-distros.sh linux/dist/LG-AC-linux-x64
```

Para testar só algumas imagens, repita `--image`: `./linux/scripts/smoke-distros.sh --image fedora:latest linux/dist/LG-AC-linux-x64`.

### CI e releases

A cada push na `main` e a cada pull request, o GitHub Actions roda os testes e empacota o app dos três sistemas, em jobs separados: `test` (macOS), `windows` e `linux` (`.github/workflows/tests.yml`). O job `linux` roda os testes com um D-Bus de sessão e um chaveiro de verdade, gera o executável e o inicia em cada distribuição de teste.

Ao publicar uma tag `v*` (por exemplo, `v1.0.0`), o workflow `.github/workflows/release.yml` roda os testes no Windows e no Linux, gera `LG-AC-windows-x64.exe`, `LG-AC-windows-arm64.exe`, `LG-AC-linux-x64` e `LG-AC-linux-arm64` e cria a release no GitHub com os quatro arquivos anexados. Tags com hífen (por exemplo, `v1.0.0-rc1`) saem marcadas como pré-lançamento.

## Arquitetura

O repositório é organizado por sistema operacional: cada sistema tem a própria pasta, com código, testes e scripts. A lógica foi escrita duas vezes, uma em Swift e outra em C# espelhando o Swift, sem ponte entre as linguagens. O que garante que os dois se comportem igual são as fixtures de `shared/`, em especial `commands.json` (cada ação do usuário e o JSON que ela envia), testado dos dois lados. O app de Linux reaproveita o núcleo em C# do Windows (`windows/src/LGAC.Core`) por referência de projeto, sem cópia; a lógica específica de tela e de sistema é própria de cada app.

```
lg-ac-menubar/
├── macos/
│   ├── Package.swift
│   ├── Sources/
│   │   ├── LGACCore/
│   │   ├── LGACApp/
│   │   └── lgac-dump/
│   ├── Tests/LGACCoreTests/
│   ├── Resources/            # Info.plist, AppIcon.icns
│   ├── scripts/              # test.sh, make-icon.swift, make-signing-cert.sh
│   └── build-app.sh
├── windows/
│   ├── LGAC.slnx
│   ├── global.json           # versão do SDK do .NET
│   ├── src/
│   │   ├── LGAC.Core/        # cliente da API, modelos, regras (sem UI)
│   │   └── LGAC.App/         # WPF: painel, ícone da bandeja, integração com o Windows
│   ├── tests/
│   │   ├── LGAC.Core.Tests/
│   │   └── LGAC.App.Tests/
│   ├── scripts/              # test.ps1
│   └── build-app.ps1
├── linux/
│   ├── LGAC.Linux.slnx
│   ├── global.json           # versão do SDK do .NET
│   ├── src/
│   │   └── LGAC.Linux/       # Avalonia: Views/ (painel), Tray/ (bandeja), Platform/ (segredos, autostart, instância única), Theme/
│   ├── tests/
│   │   └── LGAC.Linux.Tests/
│   ├── scripts/              # test.sh, smoke-distros.sh
│   └── build-app.sh
├── shared/
│   └── fixtures/             # JSONs de exemplo e contrato commands.json, usados nos testes dos dois lados
├── .github/workflows/        # CI (testes e empacotamento) e release do Windows e do Linux
└── .githooks/                # pre-push: roda os testes do macOS antes do push
```

### macOS

Um pacote Swift sem dependências externas, dividido em três alvos:

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

### Windows

Uma solução .NET 10 com dois projetos de código e dois de teste:

| Projeto | Tipo | Responsabilidade |
|---|---|---|
| `LGAC.Core` | biblioteca | Espelho em C# do `LGACCore`: cliente da API, modelos, regras, `ACController` — sem UI, roda em qualquer sistema |
| `LGAC.App` | executável WPF | Painel, ícone da bandeja, tema claro/escuro, token no Gerenciador de Credenciais, "Iniciar com o Windows" |
| `LGAC.Core.Tests` | testes | Testes do núcleo, incluindo o contrato `commands.json` |
| `LGAC.App.Tests` | testes | Testes das partes do app que dependem do Windows |

O app é publicado como um único executável autocontido (`PublishSingleFile`), para x64 e ARM64.

### Linux

Uma solução .NET 10 com um projeto de código e um de teste. O núcleo (`LGAC.Core`) vem do projeto do Windows.

| Projeto | Tipo | Responsabilidade |
|---|---|---|
| `LGAC.Linux` | executável Avalonia | Painel (`Views/`), ícone da bandeja (`Tray/`), integração com o sistema (`Platform/`: serviço de segredos via D-Bus, arquivo do token, "Iniciar com o sistema", instância única, retomada de energia) e tema (`Theme/`) |
| `LGAC.Linux.Tests` | testes | Testes das telas (Avalonia Headless) e das partes do app que dependem do Linux |

O app é publicado como um único executável autocontido e reduzido (`PublishSingleFile` e `PublishTrimmed`), para x64 e ARM64.

#### Dependências do app de Linux

O app de Linux usa duas bibliotecas de terceiros, ambas com licença MIT: [Avalonia](https://avaloniaui.net) (interface) e [Tmds.DBus.Protocol](https://github.com/tmds/Tmds.DBus) (comunicação com o D-Bus). O executável também inclui SkiaSharp e HarfBuzzSharp (MIT), as bibliotecas nativas Skia (BSD-3-Clause) e HarfBuzz (MIT) e a fonte Inter (SIL Open Font License 1.1). Os apps de macOS e Windows continuam sem dependências de terceiros.

## Fora do escopo

- Controlar vários aparelhos ao mesmo tempo (dá para alternar entre eles pela seta **‹** do painel).
- Atualização em tempo real via MQTT (exige registro de certificados na AWS IoT da LG).
- Widget/Controle dentro da Central de Controle (exige Xcode e perfil de desenvolvedor Apple).
- Distribuição pela App Store / notarização.
- No Windows: executável assinado digitalmente (por isso o aviso do SmartScreen), instalador (MSI/MSIX) e atualização automática. Para atualizar, baixe o executável da versão nova ou rode o script de novo.
- No Linux: Flatpak, Snap, AppImage, pacotes `.deb` e `.rpm`, distribuições com musl (como o Alpine), Wayland nativo (o app usa o XWayland) e painel colado ao ícone da bandeja. Para atualizar, baixe o executável da versão nova ou rode o script de novo.
- Agendamentos, histórico de consumo e modos especiais (jet, economia de energia, purificação).

## Referências

- [LG ThinQ Connect — gerar Personal Access Token](https://connect-pat.lgthinq.com)
- [pythinqconnect — SDK oficial da LG (Apache-2.0)](https://github.com/thinq-connect/pythinqconnect), usado como referência dos endpoints, headers e payloads
- [Integração LG ThinQ do Home Assistant](https://www.home-assistant.io/integrations/lg_thinq)

---

Projeto independente e **não oficial**, sem afiliação, patrocínio ou endosso da LG Electronics. "LG" e "ThinQ" são marcas da LG Electronics.
