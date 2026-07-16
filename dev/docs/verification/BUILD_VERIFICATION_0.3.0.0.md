# DeskPulse 0.3.0.0 Build Verification

## Required commands

```powershell
cd D:\Kai\GitHub\DeskPulse\dev
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Build.ps1
.\scripts\Publish.ps1
.\Installer\Build-Installer.ps1
```

## Expected outputs

- `publish\v0.3.0.0\service\DeskPulse.Service.exe`
- `publish\v0.3.0.0\tray\DeskPulse.Tray.exe`
- `publish\v0.3.0.0\installer\DeskPulse_Setup_0.3.0.0.exe`
- `releases\current\DeskPulse_Setup_0.3.0.0.exe`
- `releases\v0.3.0.0\DeskPulse_Setup_0.3.0.0.exe`

## Verification

- Build completes with zero errors.
- Publish output is self-contained for win-x64.
- Installer compiles successfully with Inno Setup 6.
- Installer upgrades the accepted 0.2.2.3 installation.
- Installed service and tray report 0.3.0.0.
- Service safeguards and diagnostic load tests pass the acceptance checks in `HANDOVER.md`.
