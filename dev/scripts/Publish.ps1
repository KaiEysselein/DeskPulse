param(
    [string]$Version = '0.4.0.3',
    [string]$PublishFolder = "v$Version",
    [string]$InformationalVersion = $Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publishRoot = Join-Path $root "publish\$PublishFolder"
$serviceOutput = Join-Path $publishRoot 'service'
$trayOutput = Join-Path $publishRoot 'tray'
$serviceExe = Join-Path $serviceOutput 'DeskPulse.Service.exe'
$trayExe = Join-Path $trayOutput 'DeskPulse.Tray.exe'

Remove-Item $publishRoot -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish (Join-Path $root 'src\DeskPulse.Service\DeskPulse.Service.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version -p:AssemblyVersion=$Version -p:FileVersion=$Version -p:InformationalVersion=$InformationalVersion -o $serviceOutput
if ($LASTEXITCODE -ne 0) { throw "Service publish failed with exit code $LASTEXITCODE." }
if (-not (Test-Path $serviceExe)) { throw "Published service executable not found: $serviceExe" }

dotnet publish (Join-Path $root 'src\DeskPulse.Tray\DeskPulse.Tray.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version -p:AssemblyVersion=$Version -p:FileVersion=$Version -p:InformationalVersion=$InformationalVersion -o $trayOutput
if ($LASTEXITCODE -ne 0) { throw "Tray publish failed with exit code $LASTEXITCODE." }
if (-not (Test-Path $trayExe)) { throw "Published tray executable not found: $trayExe" }

Write-Host "Published to publish\$PublishFolder\service and publish\$PublishFolder\tray" -ForegroundColor Green
