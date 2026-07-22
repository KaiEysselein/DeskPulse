# DeskPulse 0.3.2.0

DeskPulse 0.3.2.0 introduces a focused separation between ordinary user settings and administrator maintenance tools.

## Highlights

- Ordinary **Settings...** shows only General and Rules and does not request elevation.
- **Administrator settings...** starts a separate copy of DeskPulse through the standard Windows UAC prompt.
- The administrator process validates elevation and shows only Maintenance.
- Closing Administrator settings ends the elevated process and requires fresh UAC approval next time.
- Fixed View Log export being interrupted when the native Save dialog took focus.

## Upgrade

The installer supports upgrading an existing DeskPulse installation. The activity database and exports under `Documents\DeskPulse` are preserved.

## Scope boundary and next work

0.3.2.0 separates the user and administrator settings processes, but deliberately keeps the existing single activity database under `Documents\DeskPulse`. It does not yet split machine-wide and per-user records or make the elevated UI a complete service security boundary.

The 0.3.2.x continuation will implement service-owned `%ProgramData%\DeskPulse` databases, Windows-SID routing, safe migration and rollback, system/per-user rule ownership, and service-side named-pipe authorization.

## Installer

Release asset:

```text
DeskPulse_Setup_0.3.2.0.exe
```

## Verification

The local 0.3.2.0 build, publish and installer compilation passed on 2026-07-22. Installation, administrator-settings UAC behavior and the corrected View Log export flow were interactively confirmed.
