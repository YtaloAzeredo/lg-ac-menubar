param(
    [switch]$NoInstall,
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime
)

$ErrorActionPreference = 'Stop'

$machineRuntime = 'win-x64'
if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
    $machineRuntime = 'win-arm64'
}
if ($env:PROCESSOR_ARCHITEW6432 -eq 'ARM64' -or $env:PROCESSOR_ARCHITECTURE -eq 'ARM64') {
    $machineRuntime = 'win-arm64'
}
if (-not $Runtime) {
    $Runtime = $machineRuntime
}
$Runtime = $Runtime.ToLowerInvariant()
if (-not $NoInstall -and $Runtime -ne $machineRuntime) {
    Write-Error "Só é possível instalar o app da própria arquitetura ($machineRuntime). Use -NoInstall para gerar $Runtime."
    exit 1
}

$arch = $Runtime.Substring(4)
$project = Join-Path $PSScriptRoot 'src\LGAC.App'
$dist = Join-Path $PSScriptRoot 'dist'
$publishDir = Join-Path $dist ".publish-$Runtime"
$artifact = Join-Path $dist "LG-AC-windows-$arch.exe"

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "Compilando o app ($Runtime)..."
$publishExitCode = 0
Push-Location $PSScriptRoot
try {
    dotnet publish $project -c Release -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -o $publishDir
    $publishExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}
if ($publishExitCode -ne 0) {
    Write-Error "dotnet publish falhou (código $publishExitCode)."
    exit 1
}

$published = Join-Path $publishDir 'LGAC.App.exe'
if (-not (Test-Path -LiteralPath $published)) {
    Write-Error "Executável não encontrado em $published."
    exit 1
}

Copy-Item -LiteralPath $published -Destination $artifact -Force
Remove-Item -LiteralPath $publishDir -Recurse -Force
Write-Host "OK: $artifact"

if ($NoInstall) {
    exit 0
}

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\LG AC'
$installedExe = Join-Path $installDir 'LG AC.exe'

Write-Host 'Fechando o app em execução...'
$running = @(Get-Process -Name 'LG AC' -ErrorAction SilentlyContinue)
foreach ($process in $running) {
    try {
        $process.Kill()
    } catch {
    }
}
foreach ($process in $running) {
    try {
        $process.WaitForExit(10000) | Out-Null
    } catch {
    }
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
for ($attempt = 1; $attempt -le 5; $attempt++) {
    try {
        Copy-Item -LiteralPath $artifact -Destination $installedExe -Force
        break
    } catch {
        if ($attempt -eq 5) {
            throw
        }
        Start-Sleep -Milliseconds 500
    }
}

$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'LG AC.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$installedExe,0"
$shortcut.Save()

Write-Host "Instalado em $installedExe"
Write-Host "Atalho criado em $shortcutPath"
Start-Process -FilePath $installedExe
