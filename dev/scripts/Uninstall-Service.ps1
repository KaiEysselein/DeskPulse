# Manual test uninstall. Run from elevated PowerShell.
# Preserves legacy Documents data and ProgramData System/Users databases.
$ErrorActionPreference = 'Continue'
Stop-Process -Name 'DeskPulse.Tray' -Force -ErrorAction SilentlyContinue
Stop-Service 'DeskPulse.Service' -ErrorAction SilentlyContinue
sc.exe delete 'DeskPulse.Service' | Out-Null
Start-Sleep -Seconds 2

Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'DeskPulse' -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'DeskPulse.Tray' -ErrorAction SilentlyContinue
Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -like '*DeskPulse*' -or $_.TaskPath -like '*DeskPulse*' } | Unregister-ScheduledTask -Confirm:$false
Remove-Item (Join-Path ([Environment]::GetFolderPath('Startup')) 'DeskPulse Tray.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Startup')) 'DeskPulse.lnk') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:ProgramFiles 'DeskPulse') -Recurse -Force -ErrorAction SilentlyContinue
$programData = Join-Path $env:ProgramData 'DeskPulse'
Remove-Item (Join-Path $programData 'settings.json') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $programData 'settings.json.tmp') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $programData 'critical-safety-pause.flag') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA 'DeskPulse') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:APPDATA 'DeskPulse') -Recurse -Force -ErrorAction SilentlyContinue

Write-Host 'DeskPulse service, program files, settings and startup entries removed. Activity databases were preserved.' -ForegroundColor Yellow
