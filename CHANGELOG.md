# Changelog

All notable changes to DeskPulse will be documented in this file.

The format is loosely based on Keep a Changelog, and this project currently follows early pre-release versioning.

## [0.0.2] - 2026-07-02

### Summary
Version 0.0.2 implements the SQLite storage solution and changes Excel from a live log file into an export/reporting format.

### Changed
- Replaced CSV-based live logging with SQLite database storage.
- Changed the primary live storage file to `DeskPulse.db`.
- Changed the previous "Open log file in Excel" workflow so that Excel opens an exported workbook instead of the live storage file.
- Changed the default storage concept from a direct log file path to a DeskPulse data folder.
- Changed the Excel export to use an `.xlsx` workbook instead of a CSV snapshot.
- Kept the exported worksheet name as `File Activity`.
- Renamed the file type concept to `Extension`, storing values such as `.dwg`, `.docx`, `.xlsx`, `.pdf`, etc.
- Split file path information into separate exported fields:
  - `Full Path`
  - `Folder Path`
  - `File Name`
  - `Extension`
- Normalized ETW `LanmanRedirector` paths into readable mapped-drive style paths where possible.
- Updated the settings window to use tabs.
- Moved file-related settings into a `Files` settings tab.
- Removed the manual monitored-extensions text field from settings.
- Changed the right-hand monitored file type list to be the source of truth.
- Updated the About text to describe SQLite storage and XLSX export.

### Added
- Added SQLite database initialization on startup.
- Added `ActivityEvents` database table for file activity and error records.
- Added database migration logic for new columns required by v0.0.2.
- Added SQLite indexes for event creation time, item/full path, and extension.
- Added XLSX export using ClosedXML.
- Added `DeskPulse-export.xlsx` as the Excel viewing/reporting file.
- Added fallback startup/error diagnostics to `%TEMP%\DeskPulse-startup.log`.
- Added a registry-backed setting called `IgnoreTempFolders`.
- Added a settings checkbox for ignoring temporary-folder activity.
- Added hardcoded exclusion for file activity created by DeskPulse itself.
- Added project references for:
  - `Microsoft.Data.Sqlite`
  - `SQLitePCLRaw.bundle_e_sqlite3`
  - `ClosedXML`

### Fixed
- Fixed the XLSX export temporary-file issue by ensuring temporary workbook files still use the `.xlsx` extension.
- Fixed the nullable warning in the About window GitHub link handler.
- Prevented DeskPulse-created/opened/closed files from being logged.
- Prevented recursive logging of DeskPulse database and export files.
- Improved exported file path readability for network files reported through `LanmanRedirector`.

### Notes
- SQLite is now the live data store.
- Excel is used only as an export/view/report format.
- CSV is no longer the primary live logging format.
- The app still requires Administrator rights because ETW kernel file I/O tracing requires elevation.
- Deployment packaging is still to be treated carefully; v0.0.2 can be considered a working source/build baseline before formal installer packaging.

## [0.0.1] - 2026-07-02

### Initial baseline
- Created the first DeskPulse source baseline.
- Implemented DeskPulse as a Windows tray application written in C# / .NET 8 WinForms.
- Implemented ETW-based file I/O tracing using `Microsoft.Diagnostics.Tracing.TraceEvent`.
- Required Administrator rights through `app.manifest`, because Windows kernel ETW file I/O tracing requires elevation.
- Added tray menu entries:
  - `Open log file in Excel`
  - `Settings...`
  - `About`
  - `Exit`
- Added About window with app name, version, and clickable GitHub link.
- Added selected file activity monitoring for configured extensions.
- Added tracking for file open, write/save, and close activity.
- Added process name and process ID capture for the process touching the file.
- Added settings storage under `HKCU\Software\DeskPulse`.
- Added settings window with a two-list selector:
  - registered Windows file types on the left
  - monitored file types on the right
- Added default monitored extensions:
  - `.txt`
  - `.pdf`
  - `.docx`
  - `.xlsx`
  - `.dwg`
  - `.jpg`
  - `.png`
  - `.cs`
- Added temporary-folder exclusion logic.
- Added exclusion logic for the live CSV log and Excel-view snapshot CSV to avoid recursive logging.
- Added default log target concept:
  - `%USERPROFILE%\Documents\DeskPulse\DeskPulse-log.csv`
- Added Excel-safe CSV snapshot concept:
  - `%USERPROFILE%\Documents\DeskPulse\DeskPulse-log-view.csv`

### Project files
- Added/confirmed the main source file:
  - `Program.cs`
- Added/confirmed the project file:
  - `DeskPulse.csproj`
- Added/confirmed application manifest:
  - `app.manifest`
- Added/confirmed application icon:
  - `file-logger.ico`
- Added/confirmed documentation files:
  - `README.md`
  - `CHANGELOG.md`
- Added/confirmed license intention:
  - GNU General Public License v3.0 or later
  - SPDX: `GPL-3.0-or-later`

### Known limitations
- Program start/stop monitoring was removed from the current baseline.
- Run-at-startup code was removed from the app.
- Windows autostart should be handled externally for now, preferably through Windows Task Scheduler.
- Installer integration was deferred.
- Tray notification/balloon icon branding was not fully resolved.
- Standalone/publish deployment was not yet reliable.
- Copied standalone EXE and/or publish-folder builds had not yet behaved reliably on all tests.
- The source/build-folder version was the known working baseline.

### Deployment notes
- The build-folder executable could work when run from its build folder because adjacent dependency files were available.
- Copying only the build-folder EXE elsewhere was not considered valid.
- Proper publish-folder, portable ZIP, single-file EXE, or installer deployment still needed to be stabilized.
- Generated files and build output should not be committed to Git:
  - `bin/`
  - `obj/`
  - `publish/`
  - logs
  - CSV files
  - generated EXE files
  - user-specific IDE files