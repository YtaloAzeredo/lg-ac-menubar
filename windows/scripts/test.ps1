$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $root 'LGAC.slnx') @args
exit $LASTEXITCODE
