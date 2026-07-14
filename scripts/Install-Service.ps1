# Manual test installation. Run from elevated PowerShell after Publish.ps1.
# Do not use this script over an installer-managed installation without uninstalling first.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$serviceSource = Join-Path $root 'publish\v0.2.0.1\service'
$traySource = Join-Path $root 'publish\v0.2.0.1\tray'
$installRoot = Join-Path $env:ProgramFiles 'DeskPulse'
$serviceInstall = Join-Path $installRoot 'Service'
$trayInstall = Join-Path $installRoot 'Tray'

if (!(Test-Path (Join-Path $serviceSource 'DeskPulse.Service.exe'))) { throw 'Publish the projects first by running scripts\Publish.ps1.' }
if (!(Test-Path (Join-Path $traySource 'DeskPulse.Tray.exe'))) { throw 'Publish the projects first by running scripts\Publish.ps1.' }

Stop-Process -Name 'DeskPulse.Tray' -Force -ErrorAction SilentlyContinue
Stop-Service 'DeskPulse.Service' -ErrorAction SilentlyContinue
sc.exe delete 'DeskPulse.Service' | Out-Null
Start-Sleep -Seconds 2

New-Item -ItemType Directory -Force $serviceInstall, $trayInstall | Out-Null
Copy-Item "$serviceSource\*" $serviceInstall -Recurse -Force
Copy-Item "$traySource\*" $trayInstall -Recurse -Force

$programData = Join-Path $env:ProgramData 'DeskPulse'
New-Item -ItemType Directory -Force $programData | Out-Null
& icacls $programData /grant '*S-1-5-32-545:(OI)(CI)M' /T /C | Out-Null
& "$trayInstall\DeskPulse.Tray.exe" --initialize-settings

sc.exe create 'DeskPulse.Service' binPath= "`"$serviceInstall\DeskPulse.Service.exe`"" start= auto DisplayName= "DeskPulse Service" | Out-Null
sc.exe description 'DeskPulse.Service' 'DeskPulse background monitoring service.' | Out-Null
sc.exe failure 'DeskPulse.Service' reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
Start-Service 'DeskPulse.Service'

$startup = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startup 'DeskPulse Tray.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = "$trayInstall\DeskPulse.Tray.exe"
$shortcut.WorkingDirectory = $trayInstall
$shortcut.Save()

Start-Process "$trayInstall\DeskPulse.Tray.exe"
Write-Host 'DeskPulse service installed and one tray startup shortcut created.' -ForegroundColor Green
