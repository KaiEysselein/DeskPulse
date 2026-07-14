$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publishRoot = Join-Path $root 'publish\v0.2.0.1'
$serviceOutput = Join-Path $publishRoot 'service'
$trayOutput = Join-Path $publishRoot 'tray'

Remove-Item $publishRoot -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish (Join-Path $root 'src\DeskPulse.Service\DeskPulse.Service.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $serviceOutput
dotnet publish (Join-Path $root 'src\DeskPulse.Tray\DeskPulse.Tray.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $trayOutput

Write-Host 'Published to publish\v0.2.0.1\service and publish\v0.2.0.1\tray' -ForegroundColor Green
