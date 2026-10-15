#!/usr/bin/env bash
# Compila, monta "LG AC.app", assina ad-hoc e instala em /Applications.
# Uso: ./build-app.sh            (compila e move para /Applications; não sobra cópia em dist/)
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

# Com o certificado local (scripts/make-signing-cert.sh) a assinatura é a mesma a cada build e o
# Keychain reconhece o app depois de reinstalar. Sem ele, ad-hoc: o Keychain pede a senha de novo.
identity="LG AC Dev"
security find-identity -p codesigning | grep -F "\"$identity\"" >/dev/null || identity="-"
codesign --force --sign "$identity" "$app"
echo "✓ $app (assinatura: $([[ "$identity" == "-" ]] && echo ad-hoc || echo "$identity"))"

if [[ "${1:-}" == "--no-install" ]]; then
  exit 0
fi

# Fecha a versão antiga, se estiver aberta, antes de substituir.
pkill -x LGACApp 2>/dev/null || true
rm -rf "/Applications/LG AC.app"
# Move em vez de copiar: uma cópia em dist/ apareceria no Spotlight/Launchpad
# com o mesmo bundle id e poderia ser aberta (ou registrada no login) por engano.
mv "$app" "/Applications/LG AC.app"
rmdir dist 2>/dev/null || true
echo "✓ Instalado em /Applications/LG AC.app"
open "/Applications/LG AC.app"
