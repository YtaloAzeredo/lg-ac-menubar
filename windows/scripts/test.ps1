$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testExitCode = 0
Push-Location $root
try {
    dotnet test (Join-Path $root 'LGAC.slnx') @args
    $testExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}
exit $testExitCode
