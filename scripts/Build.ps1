$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
dotnet restore .\DeskPulse.sln
dotnet build .\DeskPulse.sln -c Release
