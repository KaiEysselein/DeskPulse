$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$issFile = Join-Path $PSScriptRoot 'DeskPulse.iss'

$candidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 compiler (ISCC.exe) was not found."
}

$serviceExe = Join-Path $projectRoot 'publish\service\DeskPulse.Service.exe'
$trayExe = Join-Path $projectRoot 'publish\tray\DeskPulse.Tray.exe'

if (-not (Test-Path $serviceExe)) {
    throw "Published service not found: $serviceExe"
}
if (-not (Test-Path $trayExe)) {
    throw "Published tray app not found: $trayExe"
}

Push-Location $PSScriptRoot
try {
    & $iscc $issFile
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Installer created at:" -ForegroundColor Green
Write-Host (Join-Path $PSScriptRoot 'Output\DeskPulse_Setup_0.2.0.0.exe')
