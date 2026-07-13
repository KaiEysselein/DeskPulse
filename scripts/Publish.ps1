$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
dotnet publish .\src\DeskPulse.Service\DeskPulse.Service.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\service
dotnet publish .\src\DeskPulse.Tray\DeskPulse.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\tray
Write-Host 'Published to publish\service and publish\tray' -ForegroundColor Green
