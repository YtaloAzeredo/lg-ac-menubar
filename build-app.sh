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
