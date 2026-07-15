$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

dotnet restore .\DeskPulse.sln
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

dotnet build .\DeskPulse.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

Write-Host 'DeskPulse build completed successfully.' -ForegroundColor Green
