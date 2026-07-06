# Changelog

All notable changes to DeskPulse will be documented in this file.

The format is loosely based on Keep a Changelog, and this project currently follows early pre-release versioning.


## [0.1.1] - 2026-07-06

### Summary

Version 0.1.1 adds the first practical hidden Maintenance workspace for database management, statistics, cleanup, exclusions, and diagnostics.

### Changed

- Reordered Maintenance > Cleanup so the lowest-risk generated-file cleanup appears first, rule-based unwanted-data cleanup appears second, and full record deletion appears last.
- Separated Maintenance into its own right-click form instead of showing it as a tab inside normal Settings.
- Normal Settings now contains only General, Files, and Export Options.

- Removed the redundant top-level manual folder action checkboxes from Maintenance > Logging Rules; per-row Include/Exclude/Sub checkboxes are now the only rule controls.
- Widened the folder-exclusion rules table and made the Folder path column fill the available width for easier reading.
- Changed the Settings form and exclusion rules grid to use the Windows message-box font instead of a fixed custom form font.

- Updated the application version constant to `0.1.1`.
- Updated project metadata in `DeskPulse.csproj` to `0.1.1`.
- Updated the application manifest assembly identity version to `0.1.1.0`.
- Extended hidden Maintenance mode so it can be opened with `-maintenance` or `-m`.
- Reworked the hidden Maintenance tab into sub-tabs.

### Added

- Added maintenance button hover tooltips explaining whether each action deletes selected records, newly added rules, generated files, registry settings, or all activity records.
- Added multi-select support to Maintenance > Statistics using normal Shift/Ctrl row selection, with add-rule actions applied to all selected rows.

- Added a right-click tray menu `Maintenance...` shortcut when DeskPulse is started with `-maintenance` or `-m`; it opens Settings directly on the Maintenance tab.
- Added an `Also exclude subfolders` option to the manual folder-exclusion entry.
- Added folder exclusion rule suffixes: `|recursive` and `|folder-only`; plain existing folder entries remain recursive for backward compatibility.
- Added `Remove Exclusions from Past Records` under Maintenance > Logging Rules to permanently delete existing file/program records that match the current logging rules, with a destructive-action warning dialog.
- Added a matching determinate progress dialog for `Remove Exclusions from Past Records`: 1% while matching records are determined, then the remaining 99% based on actual deleted records.
- Added a manual folder-entry field under Maintenance > Logging Rules so a folder path can be typed or pasted and added to the excluded folders list.
- Added Maintenance > Database overview with database path, SQLite database size, WAL/SHM size, and record counts.
- Added Maintenance > Statistics with Top 100 views for full paths, folders, file processes, extensions, and program events.
- Added Maintenance > Cleanup actions for clearing file activity, user/session activity, program activity, or all activity records while preserving the database structure.
- Added generated-file cleanup for the Excel export, diagnostic log, and startup fallback log.
- Added Maintenance > Logging Rules with editable excluded folders and excluded processes.
- Added default excluded folders for common Windows/temp/cache locations.
- Added default excluded processes for common noisy/background Windows processes.
- Added exclusion checks to file logging and program activity logging.
- Added Maintenance > Diagnostics startup task status display.

### Notes

- Locked `0.1.1` as the maintenance/data-management release baseline; future feature changes should move to `0.1.2` unless a critical 0.1.1 hotfix is required.
- Database clear actions are destructive and require confirmation.
- Database clear actions keep the SQLite file and table structure but remove selected records.
- Exclusion changes are saved with the normal Settings Save button.
- Compile and test locally before committing or publishing.

### Added / Changed in this package

- Redesigned the hidden Maintenance Logging Rules page as `Logging Rules`.
- Added exact file exclusion rules and a Statistics-grid right-click action to exclude a selected Top 100 full-path item.
- Added a compact rule-entry panel with Folder/Process type selection, Include/Exclude action selection, folder subfolder option, and Browse buttons.
- Added a single ordered rules table for folder and process/program rules. The first matching rule wins.
- Added rule management buttons for Move Up, Move Down, Remove, Duplicate, and Reset Defaults.
- Added a `LoggingRules` registry value while keeping legacy `ExcludedFolders` and `ExcludedProcesses` values for compatibility.
- Cleaned up spacing on the Maintenance logging-rules form to reduce unused whitespace.


### Changed

- Clarified the Maintenance cleanup tab by renaming destructive record-clear buttons to `Delete All ...` labels.
- Added a separate `Remove unwanted data using Logging Rules` cleanup group for deleting only past file/program records that match current Exclude rules.
- Expanded cleanup hints and tooltips to clearly distinguish full record deletion from rule-based unwanted-data cleanup and generated-file cleanup.
## [0.1.0] - 2026-07-04

### Summary

Version 0.1.0 starts the next DeskPulse development baseline from the working 0.0.4 source package, makes startup/settings actions quieter, adds a normal General settings tab, polishes the UI, adds a cleaner calendar-only date-range export flow with real percentage progress, and adds program start/close activity logging for the current interactive Windows session.

### Changed

- Added a `Log program start and close activity` checkbox to the `General` settings tab.
- Added `Programs` as a selectable Excel export worksheet with configurable fields.
- Split tray icon menus by mouse button: left-click now shows day-to-day actions only, and right-click shows secondary actions.
- Changed the left-click tray menu to contain only `Export Activity Log` and `Settings...`.
- Changed the right-click tray menu to contain only `About` and `Exit`.
- Removed the tray balloon notification shown after saving settings.
- Removed remaining normal-success tray notification code so successful startup/settings actions stay silent.
- Updated the application version constant to `0.1.0`.
- Updated project metadata in `DeskPulse.csproj` to `0.1.0`.
- Updated the application manifest assembly identity version to `0.1.0.0`.
- Removed the startup balloon notification after successful monitoring startup, so DeskPulse loads straight to the tray.
- Moved Windows startup/autostart out of the hidden Maintenance placeholder and into a normal `General` settings tab.
- Polished the WinForms settings UI with a cleaner layout, larger dialog, grouped sections, improved spacing, and consistent buttons.
- Polished the About dialog with a cleaner title/version layout and less cramped spacing.
- Renamed the tray menu item from `Open log file in Excel` to `Export Activity Log`.
- Changed the Excel export workflow so clicking `Export Activity Log` opens an export-options dialog first.
- Replaced the export date text fields/dropdowns with two calendar controls only: one for the start day and one for the end day.
- Restricted exported activity, summary, error, and user worksheets to the selected inclusive date range.
- Changed the export dialog so it stays open while the export is running, shows a real percentage progress bar based on counted Excel data rows, and closes only after the export/open operation completes.
- Changed the export progress status text so the current milestone is written below the progress bar while the export advances.

### Added

- Added `README.md` updated for version `0.1.0`.
- Added `ROADMAP.md` for future bugs, enhancements, and planned versions.
- Added current-session program start/close monitoring using periodic process snapshots.
- Added `ProgramEvents` SQLite table for application/process start and close records.
- Added registry-backed `LogProgramActivity` setting.
- Added export fields for program activity including date, time, event type, program, process ID, program path, window title, user, computer, app version, and note.
- Added a normal `General` settings tab.
- Added `Start DeskPulse when I log in to Windows` setting.
- Added registry-backed `StartWithWindows` setting.
- Added Windows Task Scheduler integration using a current-user `DeskPulse` task with `ONLOGON` trigger and highest privileges.
- Added startup-task creation/removal through `schtasks.exe`.
- Added reusable settings-form layout helpers for tabs, group boxes, hint labels, and action buttons.
- Added an `Export Activity Log` date-range dialog with two calendar controls.
- Added a real percentage progress bar for Excel export generation, based on the number of data rows written to the selected worksheets.
- Added live export status messages below the progress bar: reading records, counting rows, writing worksheet rows, saving, replacing, opening, and complete.
- Defaulted the export date range to today only.

### Notes

- Startup failure/error messages are still shown if monitoring cannot start.
- The Windows startup option creates a Task Scheduler entry for the current user rather than using the simple Startup folder, because DeskPulse requires Administrator privileges for ETW tracing.
- The startup option should be tested from the published folder path that will actually remain on the user's computer.
- Compile and test locally before committing or publishing.
- Export date filtering is inclusive: selected start date through selected end date.
- Normal successful export actions remain quiet; export errors are still shown in a dialog.
- Export progress is based on data rows written to the selected worksheets, with additional progress steps for reading, counting, saving, replacing, and opening the workbook.
- Program start/close logging tracks processes in the current interactive Windows session. It is intended as user-session activity logging, not as a full Windows service or machine-wide audit log.

## [0.0.4] - 2026-07-03

### Summary

Version 0.0.4 adds configurable Excel export worksheets and separates normal settings from hidden maintenance/admin tools.

### Changed

- Updated the application version constant to `0.0.4`.
- Updated project metadata in `DeskPulse.csproj` to `0.0.4`.
- Added a normal-user `Export Options` settings tab.
- Changed the first export-options design from a worksheet-only selector to a worksheet-plus-field selector.
- Changed the `Maintenance` tab so it is hidden during normal use and visible only when DeskPulse is started with `-maintenance`, `--maintenance`, or `/maintenance`.
- Changed Excel export generation so it creates worksheets according to the selected export options and order.

### Added

- Added registry-backed `ExportSheets` setting for worksheet order and selected fields.
- Added selectable Excel worksheet options:
  - `File Activity`
  - `Daily Summary`
  - `Summary by Extension`
  - `Summary by Process`
  - `Errors`
  - `User`
- Added Up/Down ordering controls for Excel worksheet order.
- Added field sub-tabs for checked worksheets.
- Added selectable and sortable fields/columns per worksheet, including fields such as file type/extension, dates, times, process, path, write count, and notes.
- Added `-uninstall`, `--uninstall`, and `/uninstall` command-line cleanup mode.
- Added portable cleanup for current-user registry settings and generated log/report files while preserving SQLite database data.
- Added `UserEvents` SQLite table for user/session activity.
- Added logging for DeskPulse started and DeskPulse stopped events.
- Added logging for PC lock/unlock and Windows session logon/logoff events via Windows session switch events.
- Added `User` Excel export worksheet with selectable/sortable fields.

### Notes

- Default export behaviour remains conservative: `File Activity` only.
- Default field behaviour keeps all standard fields enabled unless the user changes them.
- `File Activity` remains the worksheet name for the main detailed activity export.
- The SQLite database remains the live store.
- Excel remains export/reporting only.
- `-debug` remains diagnostic logging only; it does not show the Maintenance tab by itself.

## [0.0.3] - 2026-07-02

### Summary

Version 0.0.3 updates DeskPulse as a portable-app maintenance release.

This version aligns the app, project metadata, and documentation to `0.0.3`, and adds a new `Maintenance` tab to the settings window. The new tab allows basic portable-app cleanup without relying on an installer.

### Changed

- Updated the application version constant to `0.0.3`.
- Updated project metadata in `DeskPulse.csproj` to `0.0.3`.
- Updated README, changelog, and handover documentation to refer to version `0.0.3`.
- Expanded the settings window from a single `Files` tab to include a second `Maintenance` tab.

### Added

- Added a `Maintenance` settings tab.
- Added an `Open DeskPulse Data Folder` button.
- Added an `Open DeskPulse Program Folder` button.
- Added a `Remove DeskPulse Registry Settings` button.
- Added confirmation text explaining that registry cleanup does not delete:
  - the program files
  - the SQLite database
  - the Excel export
  - the data folder
- Added a disabled placeholder checkbox for future Windows startup/autostart settings.
- Added explanatory UI text noting that future autostart should use Windows Task Scheduler because DeskPulse requires Administrator privileges for ETW tracing.
- Added `AppSettings.GetRegistryPathForDisplay()`.
- Added `AppSettings.DeleteRegistrySettings()`.

### Notes

- This version does not implement Windows autostart yet.
- This version does not create or remove Task Scheduler entries yet.
- This version does not delete the DeskPulse data folder or database automatically.
- This version does not use an installer for cleanup.
- Registry cleanup applies only to the current Windows user under `HKCU\Software\DeskPulse`.


### Added

- Added command-line diagnostic logging with `-debug`, `--debug`, or `/debug`.
- Added optional full skip-reason tracing with `-debug-skipped`, `--debug-skipped`, or `/debug-skipped`.
- Added `DeskPulse-diagnostics.log` for ETW file-filter troubleshooting.
- Added diagnostic entries for accepted monitored events, raw ETW path, normalized path, detected extension, active monitored extensions, and process information.
- Added Maintenance tab buttons to open the diagnostic log and show active monitored extensions.

### Fixed

- Fixed monitored extensions such as `.pad` and `.zip` not appearing in the Excel export when ETW close events arrived without a usable filename.
- Changed accepted `OPEN` events to write an activity row immediately.
- Changed accepted `WRITE` events to write an activity row immediately.
- Kept `CLOSE` event handling when the close event contains a usable filename.
- Reduced normal `-debug` log volume by logging accepted monitored events only; skipped events are now logged only when `-debug-skipped` is also used.

## [0.0.2] - 2026-07-02

### Summary

Version 0.0.2 implements the SQLite storage solution and changes Excel from a live log file into an export/reporting format.

### Changed

- Replaced CSV-based live logging with SQLite database storage.
- Changed the primary live storage file to `DeskPulse.db`.
- Changed the previous `Open log file in Excel` workflow so that Excel opens an exported workbook instead of the live storage file.
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
- Development of this version used AI-assisted coding support, with final decisions, testing, and release responsibility remaining with the maintainer.

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

### Added

- Added ordered exclusion rules for folder/process filtering where the upper rule takes priority over lower rules.
- Added Include/Exclude rule support for folder exclusions, allowing broad exclusions with specific include exceptions above them.
- Added Move Up/Move Down controls for exclusion rule ordering.
