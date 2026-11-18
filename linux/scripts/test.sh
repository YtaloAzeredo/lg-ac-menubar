#!/usr/bin/env bash
set -euo pipefail
export AVALONIA_TELEMETRY_OPTOUT=1
cd "$(dirname "$0")/.."

exec dotnet test LGAC.Linux.slnx "$@"
