$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $projectRoot
$issFile = Join-Path $PSScriptRoot 'DeskPulse.iss'
$version = '0.3.1.0'
$versionFolder = "v$version"

$candidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 compiler (ISCC.exe) was not found."
}

$publishRoot = Join-Path $projectRoot "publish\$versionFolder"
$serviceExe = Join-Path $publishRoot 'service\DeskPulse.Service.exe'
$trayExe = Join-Path $publishRoot 'tray\DeskPulse.Tray.exe'
$installerDir = Join-Path $publishRoot 'installer'
$installerName = "DeskPulse_Setup_$version.exe"
$installerExe = Join-Path $installerDir $installerName

if (-not (Test-Path $serviceExe)) {
    throw "Published service not found: $serviceExe"
}
if (-not (Test-Path $trayExe)) {
    throw "Published tray app not found: $trayExe"
}

New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

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

if (-not (Test-Path $installerExe)) {
    throw "Installer was not created: $installerExe"
}

$releasesRoot = Join-Path $workspaceRoot 'releases'
$currentReleaseDir = Join-Path $releasesRoot 'current'

New-Item -ItemType Directory -Path $currentReleaseDir -Force | Out-Null
Get-ChildItem -LiteralPath $currentReleaseDir -Force -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force

$currentInstaller = Join-Path $currentReleaseDir $installerName
Copy-Item -LiteralPath $installerExe -Destination $currentInstaller -Force

$isMilestone = $version -match '^0\.\d+\.\d+\.0$'
$milestoneInstaller = $null

if ($isMilestone) {
    $milestoneDir = Join-Path $releasesRoot $versionFolder
    New-Item -ItemType Directory -Path $milestoneDir -Force | Out-Null

    $milestoneInstaller = Join-Path $milestoneDir $installerName
    Copy-Item -LiteralPath $installerExe -Destination $milestoneInstaller -Force
}

Write-Host ""
Write-Host "Installer created at:" -ForegroundColor Green
Write-Host $installerExe
Write-Host ""
Write-Host "Current approved installer copied to:" -ForegroundColor Green
Write-Host $currentInstaller

if ($milestoneInstaller) {
    Write-Host ""
    Write-Host "Milestone installer retained at:" -ForegroundColor Green
    Write-Host $milestoneInstaller
}
