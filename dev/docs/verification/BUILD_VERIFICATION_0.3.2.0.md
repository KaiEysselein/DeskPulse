# DeskPulse 0.3.2.0 Build Verification

Date: 2026-07-22

## Verified

- Solution restore and Release build completed with zero warnings and zero errors.
- Self-contained win-x64 service and tray publish completed under `publish\v0.3.2.0`.
- Published service and tray executables report file version 0.3.2.0.
- Inno Setup 6.7.3 compiled `DeskPulse_Setup_0.3.2.0.exe` successfully.
- Identical installer copies were produced under `publish\v0.3.2.0\installer`, `releases\current` and `releases\v0.3.2.0`.
- Installation and the separate Administrator settings UAC flow were interactively confirmed.
- Ordinary Settings shows General and Rules only; elevated Administrator settings shows Maintenance only.
- The corrected View Log export flow remains open through the Save dialog and creates the Excel workbook.

## Explicitly outside 0.3.2.0

- Moving the activity database from Documents to ProgramData.
- Splitting system and per-user databases and routing records by Windows SID.
- Migrating existing data with backup, validation and rollback.
- Separating system and per-user rule ownership.
- Service-side authorization of administrative named-pipe clients.

These items continue as 0.3.2.x work and remain tracked in `BACKLOG.md`.
