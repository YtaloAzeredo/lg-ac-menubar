#!/usr/bin/env bash
# Roda os testes. Só com as Command Line Tools (sem Xcode), o SwiftPM não acha o
# módulo Testing, e o overlay _Testing_Foundation vem sem módulo; contornamos os dois.
set -euo pipefail
cd "$(dirname "$0")/.."

flags=()
dev_dir="$(xcode-select -p 2>/dev/null || true)"
fw="$dev_dir/Library/Developer/Frameworks"
if [[ "$dev_dir" == *CommandLineTools* && -d "$fw" ]]; then
  flags=(
    -Xswiftc -F -Xswiftc "$fw"
    -Xswiftc -Xfrontend -Xswiftc -disable-cross-import-overlays
    -Xlinker -F -Xlinker "$fw"
    -Xlinker -rpath -Xlinker "$fw"
  )
fi

exec swift test ${flags[@]+"${flags[@]}"} "$@"
