# Fixtures

JSON de exemplo da API LG ThinQ Connect usados nos testes. Nenhum arquivo aqui contém dados de um aparelho real.

- `profile-full.json` / `state-full.json` — modelo com todos os recursos que o app usa. Estrutura baseada nas fixtures de ar-condicionado da integração `lg_thinq` do Home Assistant (Apache-2.0), com luz do visor, timer ativo e modos extras acrescentados.
- `profile-basic.json` / `state-basic.json` — modelo fictício "básico": sem swing horizontal, sem luz do visor, sem timer, ventilador em `windStrengthDetail`, temperatura com passo de 0,5°.

Para conferir o formato do seu aparelho, use `swift run lgac-dump ./dump` (veja o README principal). Não copie arquivos de `dump/` para cá sem remover `deviceId`, apelido e modelo.
