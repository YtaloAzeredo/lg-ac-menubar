#!/usr/bin/env bash
set -euo pipefail

export AVALONIA_TELEMETRY_OPTOUT=1

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

usage() {
  echo "Uso: ./build-app.sh [--no-install] [--runtime linux-x64|linux-arm64]" >&2
}

no_install=0
runtime=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-install)
      no_install=1
      shift
      ;;
    --runtime)
      if [[ $# -lt 2 ]]; then
        usage
        exit 64
      fi
      runtime="$2"
      shift 2
      ;;
    *)
      usage
      exit 64
      ;;
  esac
done

case "$runtime" in
  ""|linux-x64|linux-arm64) ;;
  *)
    usage
    exit 64
    ;;
esac

case "$(uname -m)" in
  x86_64) machine_runtime="linux-x64" ;;
  aarch64|arm64) machine_runtime="linux-arm64" ;;
  *) machine_runtime="" ;;
esac

if [[ -z "$runtime" ]]; then
  if [[ -z "$machine_runtime" ]]; then
    echo "Arquitetura $(uname -m) sem suporte. Use --runtime linux-x64 ou --runtime linux-arm64." >&2
    exit 1
  fi
  runtime="$machine_runtime"
fi

if [[ "$no_install" -eq 0 ]]; then
  if [[ "$(uname -s)" != "Linux" ]]; then
    echo "A instalação só funciona em Linux. Use --no-install para apenas gerar o executável." >&2
    exit 1
  fi
  if [[ "$runtime" != "$machine_runtime" ]]; then
    echo "Só é possível instalar o app da própria arquitetura ($machine_runtime). Use --no-install para gerar $runtime." >&2
    exit 1
  fi
fi

arch="${runtime#linux-}"
dist="$script_dir/dist"
artifact="$dist/LG-AC-linux-$arch"

mkdir -p "$dist"
publish_dir="$(mktemp -d "$dist/.publish-XXXXXX")"
cleanup() {
  rm -rf "$publish_dir"
}
trap cleanup EXIT

echo "Compilando o app ($runtime)..."
cd "$script_dir"
dotnet publish src/LGAC.Linux \
  -c Release \
  -r "$runtime" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none \
  -p:PublishTrimmed=true \
  -o "$publish_dir"

if [[ ! -f "$publish_dir/lg-ac" ]]; then
  echo "Executável não encontrado em $publish_dir/lg-ac." >&2
  exit 1
fi

rm -f "$artifact"
cp "$publish_dir/lg-ac" "$artifact"
chmod 755 "$artifact"
echo "OK: $artifact"

if [[ "$no_install" -eq 1 ]]; then
  exit 0
fi

escape_exec() {
  local input="$1" output='"' char i
  for ((i = 0; i < ${#input}; i++)); do
    char="${input:i:1}"
    case "$char" in
      '\') output+='\\\\' ;;
      '"' | '`' | '$') output+="\\\\$char" ;;
      '%') output+='%%' ;;
      $'\n') output+='\n' ;;
      $'\r') output+='\r' ;;
      $'\t') output+='\t' ;;
      *) output+="$char" ;;
    esac
  done
  output+='"'
  printf '%s' "$output"
}

bin_dir="${XDG_BIN_HOME:-$HOME/.local/bin}"
data_home="${XDG_DATA_HOME:-$HOME/.local/share}"
installed="$bin_dir/lg-ac"
icon_dir="$data_home/icons/hicolor/256x256/apps"
applications_dir="$data_home/applications"

echo "Fechando o app em execução..."
pkill -u "$(id -u)" -x lg-ac || true
for _ in $(seq 1 50); do
  pgrep -u "$(id -u)" -x lg-ac >/dev/null || break
  sleep 0.1
done

mkdir -p "$bin_dir" "$icon_dir" "$applications_dir"
install -m 755 "$artifact" "$installed"
install -m 644 "$script_dir/src/LGAC.Linux/Assets/lg-ac.png" "$icon_dir/lg-ac.png"

desktop_file="$applications_dir/lg-ac.desktop"
{
  printf '[Desktop Entry]\n'
  printf 'Type=Application\n'
  printf 'Name=LG AC\n'
  printf 'Comment=Controle do ar-condicionado LG\n'
  printf 'Exec=%s\n' "$(escape_exec "$installed")"
  printf 'Icon=lg-ac\n'
  printf 'Terminal=false\n'
  printf 'Categories=Utility;\n'
  printf 'StartupNotify=false\n'
} > "$desktop_file"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$applications_dir" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -q -t -f "$data_home/icons/hicolor" >/dev/null 2>&1 || true
fi

echo "Instalado em $installed"
echo "Atalho criado em $desktop_file"

case ":$PATH:" in
  *":$bin_dir:"*) ;;
  *) echo "Aviso: $bin_dir não está no PATH; abra o app pelo menu de aplicativos." ;;
esac

if command -v setsid >/dev/null 2>&1; then
  setsid -f "$installed" >/dev/null 2>&1 < /dev/null
else
  nohup "$installed" >/dev/null 2>&1 < /dev/null &
  disown
fi
