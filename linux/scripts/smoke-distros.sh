#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Uso: ./smoke-distros.sh [--image <nome>]... <binário>" >&2
}

all_images=(ubuntu:22.04 ubuntu:24.04 debian:12 fedora:latest archlinux:latest)

apt_packages=(
  xvfb xauth libx11-6 libice6 libsm6 libxext6 libxi6 libxcursor1 libxrandr2 libxfixes3
  libfontconfig1 fontconfig fonts-dejavu-core ca-certificates openssl util-linux
)
dnf_packages=(
  xorg-x11-server-Xvfb xorg-x11-xauth libX11 libICE libSM libXext libXi libXcursor libXrandr libXfixes
  fontconfig dejavu-sans-fonts ca-certificates openssl-libs util-linux
)
pacman_packages=(
  xorg-server-xvfb xorg-xauth libx11 libice libsm libxext libxi libxcursor libxrandr libxfixes
  fontconfig ttf-dejavu ca-certificates openssl util-linux
)

images=()
binary=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --image)
      if [[ $# -lt 2 ]]; then
        usage
        exit 64
      fi
      images+=("$2")
      shift 2
      ;;
    -*)
      usage
      exit 64
      ;;
    *)
      if [[ -n "$binary" ]]; then
        usage
        exit 64
      fi
      binary="$1"
      shift
      ;;
  esac
done

if [[ -z "$binary" ]]; then
  usage
  exit 64
fi
if [[ ${#images[@]} -eq 0 ]]; then
  images=("${all_images[@]}")
fi

if [[ ! -f "$binary" ]]; then
  echo "Binário não encontrado: $binary" >&2
  exit 1
fi
binary="$(cd "$(dirname "$binary")" && pwd)/$(basename "$binary")"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker não encontrado. Instale o Docker para rodar este teste." >&2
  exit 1
fi
if ! docker info >/dev/null 2>&1; then
  echo "O daemon do Docker não responde. Inicie o Docker e tente de novo." >&2
  exit 1
fi

install_command() {
  case "$1" in
    ubuntu:*|debian:*)
      echo "apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq --no-install-recommends ${apt_packages[*]}"
      ;;
    fedora:*)
      echo "dnf install -y -q --setopt=install_weak_deps=False ${dnf_packages[*]}"
      ;;
    archlinux:*)
      echo "pacman -Syu --noconfirm --needed ${pacman_packages[*]}"
      ;;
    *)
      return 1
      ;;
  esac
}

container_script() {
  local install="$1"
  cat <<EOF
set -e
$install
extract_dir=/tmp/lgac-home/extract
mkdir -p /tmp/lgac-home/run "\$extract_dir"
chmod 700 /tmp/lgac-home/run
chown -R 65534:65534 /tmp/lgac-home
run_as_user() {
  setpriv --reuid=65534 --regid=65534 --clear-groups env HOME=/tmp/lgac-home XDG_RUNTIME_DIR=/tmp/lgac-home/run DOTNET_BUNDLE_EXTRACT_BASE_DIR=\$extract_dir "\$@"
}
version_status=0
smoke_status=0
echo "--- --version"
run_as_user timeout 60 /usr/local/bin/lg-ac --version || version_status=\$?
echo "--- --smoke"
run_as_user timeout 60 xvfb-run -a -s "-screen 0 1280x800x24" /usr/local/bin/lg-ac --smoke || smoke_status=\$?
echo "--- bibliotecas nativas"
native_count=\$(find "\$extract_dir" -type f -name '*.so*' | wc -l)
echo "extraídas: \$native_count"
if [ "\$native_count" -eq 0 ]; then
  echo "aviso: nenhuma biblioteca nativa encontrada em \$extract_dir"
fi
missing=\$(find "\$extract_dir" -type f -name '*.so*' -exec ldd {} + 2>/dev/null | grep 'not found' | sort -u || true)
if [ -n "\$missing" ]; then
  echo "bibliotecas ausentes:"
  echo "\$missing"
fi
echo "--- resultado: version=\$version_status smoke=\$smoke_status"
if [ "\$version_status" -ne 0 ] || [ "\$smoke_status" -ne 0 ] || [ -n "\$missing" ]; then
  exit 1
fi
EOF
}

failed=()
passed=()
for image in "${images[@]}"; do
  if ! install="$(install_command "$image")"; then
    echo "FALHOU $image"
    echo "Imagem sem suporte neste script."
    failed+=("$image")
    continue
  fi
  echo "Testando $image..."
  status=0
  output="$(docker run --rm -v "$binary:/usr/local/bin/lg-ac:ro" "$image" sh -c "$(container_script "$install")" 2>&1)" || status=$?
  if [[ "$status" -eq 0 ]]; then
    echo "OK $image"
    passed+=("$image")
  else
    echo "FALHOU $image (código $status)"
    echo "$output"
    failed+=("$image")
  fi
done

echo
echo "Resumo: ${#passed[@]} ok, ${#failed[@]} com falha."
if [[ ${#failed[@]} -gt 0 ]]; then
  echo "Falharam: ${failed[*]}"
  exit 1
fi
