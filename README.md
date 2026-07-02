# DeskPulse

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms.

It monitors selected file activity through Windows ETW file I/O tracing and stores the results in a local SQLite database. Excel is used as an export/viewing format only, not as the live log file.

## Version

Current version: `0.0.2`

## Current Features

- Windows system tray application
- File open/write/close activity monitoring
- ETW-based Windows file I/O tracing
- SQLite live storage using `DeskPulse.db`
- XLSX export for Excel viewing/reporting
- Exported worksheet name: `File Activity`
- Settings window with tabs
- `Files` settings tab
- Registered Windows file type list
- Two-list file type selector for monitored extensions
- Right-hand monitored file type list used as the source of truth
- Temporary-folder file activity exclusion
- Registry-backed option to switch temporary-folder exclusion on/off
- Hardcoded exclusion for file activity created by DeskPulse itself
- DeskPulse database/export files excluded from monitoring to avoid recursive logging
- Path export split into:
  - `Full Path`
  - `Folder Path`
  - `File Name`
  - `Extension`
- Network paths reported through `LanmanRedirector` are normalized where possible into mapped-drive style paths
- Startup/error fallback diagnostics written to `%TEMP%\DeskPulse-startup.log`

## Default Data Location

Default data folder:

```text
%USERPROFILE%\Documents\DeskPulse\