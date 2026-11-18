# Fixtures

JSON de exemplo da API LG ThinQ Connect, compartilhados pelos testes dos dois apps (`macos/` e `windows/`). Os dois lados leem esta pasta direto, a partir da raiz do repositório. Nenhum arquivo aqui contém dados de um aparelho real.

- `profile-full.json` / `state-full.json` — modelo com todos os recursos que o app usa. Estrutura baseada nas fixtures de ar-condicionado da integração `lg_thinq` do Home Assistant (Apache-2.0), com luz do visor, timer ativo e modos extras acrescentados.
- `profile-basic.json` / `state-basic.json` — modelo fictício "básico": sem swing horizontal, sem luz do visor, sem timer, ventilador em `windStrengthDetail`, temperatura com passo de 0,5°.
- `commands.json` — contrato de comandos: array de casos `{ name, profile, command: { type, value }, payload }`. `profile` é o nome de uma fixture de perfil (`profile-full` ou `profile-basic`); `type` é um de `power`, `jobMode`, `targetTemp`, `windStrength`, `swingUpDown`, `swingLeftRight`, `offTimer`, `displayLight`; `value` é bool, string, número ou `null` (só em `offTimer`, que significa cancelar o timer); `payload` é o corpo esperado do `POST /control` ou `null` quando o modelo não suporta o comando. Cada implementação monta o comando, gera o payload com o perfil indicado e compara com o esperado.

Para conferir o formato do seu aparelho, use `swift run lgac-dump ./dump` dentro de `macos/` (veja o README principal). Não copie arquivos de `dump/` para cá sem remover `deviceId`, apelido e modelo.
