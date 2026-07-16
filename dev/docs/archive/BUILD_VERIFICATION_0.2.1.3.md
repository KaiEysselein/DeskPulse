# DeskPulse 0.2.1.3 Build Verification

## Source verification

The 0.2.1.3 source package was reviewed for the following changes:

- File Activity exclusions are evaluated independently from App Activity rules during live monitoring and historical housekeeping.
- View Log File Activity includes an Extension column.
- The per-row action button is labelled Details on File Activity, App Activity, and User Activity.
- Active application, assembly, installer, publish, documentation, handover, audit, verification, and release references use version 0.2.1.3.

## Local compilation status

Compilation was not performed in the packaging environment because the .NET SDK and Inno Setup are not installed there. Build, publish, and installer compilation must be completed on the Windows development computer using the supplied PowerShell scripts.
