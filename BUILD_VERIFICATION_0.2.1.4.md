# DeskPulse 0.2.1.4 Build Verification

## Scope

- Correct mapped-drive `LanmanRedirector` path normalization.
- Add service-owned historical data repair with Maintenance UI and progress reporting.
- Advance active version references to 0.2.1.4.

## Verification status

- Source-level implementation and version-reference review completed in the handover environment.
- Local .NET compilation was not available in the handover environment because the .NET SDK executable was not installed.
- Run `scripts\Build.ps1`, `scripts\Publish.ps1`, and `Installer\Build-Installer.ps1` on the Windows development machine before acceptance.
- Test with a copied database containing a path such as `\;LanmanRedirector\;W:token\server\share\folder\file.ext`; the repaired result must be `W:\folder\file.ext`.
