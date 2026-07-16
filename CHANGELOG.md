# Changelog

## 0.2.2.2 - 2026-07-16
- Added the workspace-level `dev` and `releases` separation.
- Added milestone-only release retention: permanent releases are kept only for versions matching `v0.x.0.0`; intermediate installers replace `releases\current`.
- Updated the installer build workflow to copy approved installers into the correct local release folder automatically.
- Consolidated normal, paused, and warning PNG/ICO assets into one shared `Resources` folder.
- Added tray icon state changes for active logging, temporary pause, and service-warning conditions.
- Renamed the application icon asset from `file-logger.ico` to `DeskPulse.ico` in the Service and Tray projects and updated all active code and project references.
- Removed obsolete root-level remnants of the former single-project application layout from the active source package.
- Reorganized build verification and release audit records under `docs\verification` and `docs\archive`.
- Added a read-only repository folder scan utility under `scripts`.
- Added future feature backlog items for distinct session-only and persistent pause modes, each with a distinct tray icon state.
- Updated active project, installer, publish, release, verification, audit and handover references to 0.2.2.2.
- No logging, database, rule-processing or export behaviour was intentionally changed.

## 0.2.2.1 - 2026-07-15
- Tray-opened forms now close automatically after focus moves outside DeskPulse, while remaining open for owned child dialogs.
- Added a persisted log time-format selector with 24-hour and 12-hour (AM/PM) display options.
- Log timestamps continue to display whole seconds only; stored database values are unchanged.
- Updated active project, installer, publish, release, verification, audit and handover references to 0.2.2.1.

## 0.2.2.0 — 2026-07-15

- Promoted the complete audited 0.2.1.8 feature set to the GitHub-ready 0.2.2.0 release baseline.
- Updated all active application, assembly, installer, publish, documentation, handover, release, audit, and verification references to 0.2.2.0.
- Preserved all earlier changelog entries and historical build-verification documents under their original version numbers.


## 0.2.1.8

- Added a visible **Activity** column to the File Activity log, using the inferred user-facing action when available and falling back to the recorded activity type.
- Added **Activity** to the File Activity **Group by** selector.
- Removed the **Export Options** Settings tab and its configurable worksheet/column interface. Export continues with the existing/default export layout.
- Changed visible log time formatting to whole-second precision while retaining stored timestamp precision.
- Hardened build and publish scripts so failed native commands stop the workflow and cannot report a false successful publish.
- Updated active application, project, installer, publish, handover, roadmap, audit, release and verification references to 0.2.1.8.


## 0.2.1.7 — 2026-07-15

- Replaced the Explorer-only File Activity switch with a configurable dual-list application filter.
- Available applications are gathered from distinct process names in existing File Activity data and show historical record counts.
- Added search, multi-select transfer, double-click transfer, manual process entry, and clear-all controls.
- Filtered process names are matched case-insensitively for new File Activity logging.
- Database housekeeping removes historical File Activity records attributed to currently filtered applications.
- Updated all active application, project, installer, publish, documentation, handover, release, audit, and verification references to 0.2.1.7.


## 0.2.1.6 — 2026-07-15

- Added **Log file activity caused by Windows File Explorer** under Settings → General → Activity logging. It is enabled by default; when disabled, new file events attributed to `explorer.exe` are suppressed.
- Moved **Track Windows system activity** from Maintenance to Settings → General → Activity logging.
- Database housekeeping now applies the Explorer logging filter to historical File Activity records.
- Removed the user-facing **Repair Historical Data...** maintenance feature while retaining automatic mapped-drive normalization for newly logged records.
- Updated all active application, project, installer, publish, documentation, handover, release, audit, and verification references to 0.2.1.6.

﻿# Changelog

## 0.2.1.5

- Added a persistent, user-editable Records per page control to all log tabs, defaulting to 500 and supporting 1 to 10,000 records per page.
- Added database-backed File Activity grouping by Date, File name, Extension, Folder, or Application.
- Added expandable and collapsible group rows; double-click a group row to reveal or hide its matching records.
- Kept ordinary record double-click behavior for opening Details.
- Updated active application, project, installer, publish, release, audit, handover, roadmap, and verification references to 0.2.1.5.

## [0.2.1.4] - 2026-07-15

- Corrected ETW mapped-drive path normalization so `LanmanRedirector` device paths remove both the server and mapped share root, producing user-facing drive-letter paths such as `W:\Projects - PNP\...`.
- Added **Maintenance > Cleanup > Repair Historical Data** with progress reporting through the DeskPulse Windows service.
- Historical repair safely updates recognized File Activity path defects and refreshes `Item`, `FullPath`, `FolderPath`, `FileName`, and `Extension`; uncertain records remain unchanged.
- Updated active application, project, publish, installer, documentation, handover, audit, verification, and release references to 0.2.1.4.

## [0.2.1.3] - 2026-07-15

### Fixed

- Separated File Activity exclusion evaluation from App Activity inclusion so matching file exclusions also apply during live monitoring and historical database housekeeping.

### Changed

- Added an **Extension** column to the File Activity tab in View Log.
- Renamed the per-row **More...** button to **Details** on File Activity, App Activity, and User Activity.
- Updated all active application, project, publish, installer, documentation, handover, audit, verification, and release references to 0.2.1.3.

## [0.2.1.2] - 2026-07-14

### Fixed

- Replaced the non-working Settings **Cancel** action with a consistent **Save**, **Save and Close**, and **Close** footer.
- Added unsaved-change protection for **Close**, the window X, and Esc.
- Corrected tray menu command activation so View Log, Settings, About, and Quit respond on the first click and restore an existing window instead of creating duplicates.
- Renamed **Exit tray** to **Quit DeskPulse**.
- Added live database-housekeeping progress from the Windows service to the tray, including record counts and separate File Activity, App Activity, deletion, and compaction stages.
- Throttled deletion progress updates to each 10% of records deleted.

### Changed

- Updated all active application, project, publish, installer, documentation, handover, and release references to 0.2.1.2.

## [0.2.1.1] - 2026-07-14

### Changed

- Rules Import now presents explicit **Merge with existing rules** and **Replace existing rules** radio-button modes.
- Merge is the safer default: existing File Activity and App Activity rules are preserved, new imported rules are appended, and matching rules are updated without duplicates.
- User Activity rules remain unchanged by rules import.

## [0.2.1.0] - 2026-07-14

### Fixed

- Removed the duplicated installer option for starting the DeskPulse tray; the option now appears only on the final installer page.
- Prevented rule-based database housekeeping from scanning or deleting user/session activity records.
- Simplified User Activity settings to one Log checkbox per supported predefined event.
- Preserved unchecked User Activity events across Save and reload, including migration from the former Include/Exclude representation.
- Verified File Activity, App Activity, User Activity, and Export Options reset-default behaviour.
- Removed the obsolete hidden Maintenance logging-rules Reset Defaults control and handler.

## [0.2.0.1] - 2026-07-14

### Architecture and database ownership

- Made `DeskPulse.Service` the sole SQLite writer for live inserts, migrations, selected-record deletion, Create Rule cleanup, Database Housekeeping, exclusion cleanup, clearing individual activity tables, clearing all activity, and `VACUUM`.
- Restricted tray-side SQLite access to read-only views, counts, statistics, date discovery and exports.
- Added and documented the database-write audit.
- Fixed SQLite Error 8 (`attempt to write a readonly database`) across tray-initiated write workflows.

### Windows activity protection

- Added **Track Windows system activity** under Settings → Maintenance → Logging protection.
- Excluded the complete `%WINDIR%` tree by default, together with selected ProgramData locations, the Recycle Bin and high-volume Windows processes.
- Displayed built-in exclusions as grey, read-only **Windows default** rules.
- Ensured built-in exclusions take precedence over user Include rules while system tracking is disabled.
- Applied the same Windows-default policy during historical database housekeeping.

### Startup and deployment

- Replaced the obsolete elevated Task Scheduler tray startup mechanism with the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `DeskPulse.Tray`.
- Added best-effort cleanup of legacy scheduled tasks and Startup-folder shortcuts.
- Normalized legacy relative data paths to absolute paths under the interactive user's Documents folder.
- Ensured installer initialization runs as the original Windows user before the LocalSystem service starts.
- Versioned publish output under `publish\v0.2.0.1\service` and `publish\v0.2.0.1\tray`.
- Updated application, assembly, installer, handover and GitHub release references to 0.2.0.1.


### Settings interface

- Show **Save** and **Cancel** only on tabs that contain staged settings: General, Rules and Export Options.
- Show **Import Rules** and **Export Rules** only on the Rules tab.
- Show a single **Close** button on Maintenance because maintenance actions execute immediately.
- Save **Track Windows system activity** immediately when toggled, so Maintenance no longer depends on the global Save button.
- Restore deterministic footer-button behaviour after database housekeeping and service-maintenance actions.

### Baseline

- Locked 0.2.0.1 as the authoritative stabilisation baseline pending local compilation, installation and acceptance testing.

## [0.2.0.0] - 2026-07-13

### Architecture and deployment

- Split DeskPulse into Windows Service, tray app and shared-library projects.
- Moved ETW, application and Windows session monitoring into `DeskPulse.Service`.
- Removed normal elevation requirements from the tray application.
- Added secured local named-pipe communication between tray and service.
- Added self-contained win-x64 build and publish scripts.
- Added an Inno Setup installer that registers and starts `DeskPulse.Service` and starts the tray once per user login.
- Added comprehensive uninstall cleanup while preserving `Documents\DeskPulse` and its database.
- Corrected the pipe ACL package reference to `System.IO.Pipes.AccessControl` 5.0.0.

### Controls and reliability

- Added Pause Logging / Resume Logging to the tray menu.
- Added service status reporting and logging-state feedback.
- Added Settings → Maintenance → Restart Windows Service.
- Ensured only one top-level DeskPulse form is open at a time.
- Corrected named-pipe access for the non-elevated tray.

### Logging and reports

- Added Windows startup, login, logout, lock and unlock logging under User Activity.
- Added `DeskPulse service started` and `DeskPulse started (possible login)` wording.
- Removed Event Type from active database, logs and forms.
- Added full-result database sorting with page reset to page 1.
- Added exact visible-record ranges in Log View.
- Changed default report dates to earliest database record through today and added Today Only.
- Added optional exclusion-rule creation during deletion.
- Applied the DeskPulse icon consistently to all forms.
- Set the default database folder to `%USERPROFILE%\Documents\DeskPulse`.

## [0.1.3.2] - 2026-07-13

### Changed

- Combined Folder Activity filtering into the File Activity rules list.
- Removed the separate Folder Activity rules tab and Folder Activity log-view tab.
- Added Windows-style path wildcard support: `*` matches within one folder level, while `**` matches zero or more folder levels.
- Added **Add Folder...** in File Activity rules, with a choice between the selected folder only (`\*`) and the selected folder plus all descendants (`\**\*`).
- Migrated existing Folder Activity registry rules into equivalent File Activity wildcard rules while preserving enabled state, Include/Exclude action, and rule order.
- Advanced the registry settings schema to version 5 and removed the obsolete active `Rules\FolderActivity` registry value after migration.
- Updated rule import/export so older schema-1 exports containing Folder Activity rules remain importable and are converted automatically.
- Updated View Log, database housekeeping, documentation, and handover information for the unified File Activity model.

### Compatibility

- Existing folder-only rules migrate to `<folder>\*`.
- Existing recursive folder rules migrate to `<folder>\**\*`.
- App Activity rules continue to take precedence for matching executable files.

### View Log paging/export refinement

- Replaced the two-button pager with First Page, Previous Page, Next Page, and Last Page controls.
- Moved Export from the top toolbar to the right of the paging controls.
- Export now writes only the active View Log tab and the currently displayed 500-record page to an XLSX file.
- The exported workbook includes the visible columns, including the database ID, and excludes the More button column.

## [0.1.3.1] - View Log pagination

### Changed

- Added the database record ID as the first column in all four View Log tabs.
- Reduced View Log page size from 1,000 to 500 records.
- Added Previous and Next navigation at the bottom of View Log.
- Added independent paging for Folder, App, File, and User Activity tabs across the selected date range.
- Added page and total-record indicators for the active tab.

## [0.1.3.1] - 2026-07-13 — Tray and Maintenance removal

### Added — Maintenance database housekeeping

- Added a normal `Maintenance` tab to the right of `Export Options`.
- Added `Clean database with current rules...`.
- The action saves the rules currently displayed in Settings, evaluates the entire File, Folder, App, and User activity history against those rules, permanently removes conflicting records, and compacts the SQLite database.
- Added determinate progress and a completion summary by activity category.


### Changed

- Removed the tray icon right-click menu entirely.
- Added `Exit` directly below `About` on the left-click menu.
- Removed Maintenance mode command-line handling (`-maintenance` and `-m`).
- Removed the dedicated `MaintenanceForm`.
- Renamed the reusable maintenance progress window to `RuleCleanupProgressForm` so View Log rule cleanup and database compaction continue without a Maintenance feature.
- Kept normal Settings, Rules, View Log, export, rule-based cleanup, database compaction, About, and uninstall functionality intact.


## [0.1.3.1] - Debug logging removal and DeskPulse app lifecycle

### Changed

- Removed all command-line debug and skipped-event logging functionality.
- Removed the DeskPulse diagnostic log, related Maintenance controls, and generated-file cleanup action.
- DeskPulse now records its own start and stop events in App Activity when program activity logging and the applicable App Activity rule allow it.
- Added an explicit default App Activity Include rule for `DeskPulse` before the catch-all Include rule.

## [0.1.3.1] - 2026-07-13

### Added — View Log cleanup progress

- Added a determinate progress window when a View Log rule is applied together with old-data cleanup.
- Progress now reports rule saving, batched record deletion, transaction commit, database compaction, and finalisation.

### Fixed

- Fixed the View Log grid-selection handler build error by making `ConfigureGrid` an instance method so it can call `UpdateCreateRuleButton()`.

### Added — View Log rule creation

- Added single-row selection in View Log and a disabled-until-selected `Create Rule` button.
- Added an `Add rule to rules list` dialog with category-appropriate rule options.
- Added optional cleanup of existing records conflicting with a new Exclude rule.
- Added a record-count warning before destructive cleanup; no warning appears when no records would be removed.
- Added database compaction after confirmed cleanup and immediate monitor settings reload.

## [0.1.3.1] - View Log iteration

### Added

- Added `View Log...` to the tray icon left-click menu.
- Added a date-filtered four-tab log viewer for Folder, App, File, and User activity.
- Added per-row `More...` details dialogs showing all stored database fields.
- Limited each tab to the newest 1,000 matching records for responsive viewing.

### Fixed

- Renamed the `ViewLogForm` SQLite reader helper from `Text` to `ReadText` so it no longer hides the inherited WinForms `Form.Text` property or breaks the Designer assignment.

## [0.1.3.1] - User Activity tab cleanup

- Removed the User Activity Type selector, manual Event/Text entry, Add Rule controls, Include/Exclude selectors, and Include subfolders option.
- Removed the User Activity table columns for Exclude, Type, and Sub.
- Retained only On, Include, and the predefined event description in the User Activity table.
- Removed Move Up, Move Down, Remove, and Duplicate actions from User Activity; Reset Defaults remains available.
- Prevented User Activity rows from being manually deleted and removed now-unused shared rule-entry control code.


### Strict activity-rule allow-lists and registry schema 3

- Changed App Activity logging to require an enabled matching Include rule; unmatched applications are no longer logged.
- Changed User Activity logging to require an enabled matching Include rule; unmatched user/session events are no longer logged.
- Kept first-match-wins behavior: a matching Exclude suppresses logging and a matching Include permits it.
- Added default User Activity Include rules for DeskPulse start/stop, lock/unlock, logon/logoff, console connect/disconnect, and remote connect/disconnect.
- Added a final `*` App Activity Include rule after the default noisy-process exclusions, preserving useful existing behavior while making it rule-driven.
- Increased `SettingsSchemaVersion` to `3`. Existing schema-2 settings are migrated once: empty User Activity rules receive the default event Includes, and App Activity receives a final Include-all rule if it does not already have one.
- Removed the now-unused legacy `IsProgramActivityExcluded` helper.

## [0.1.3.1] - Installed application picker

### Added

- Added an `Add App...` button to Rules > App Activity.
- Added a searchable installed-application selection form with multi-select checkboxes.
- Selected applications are appended to App Activity as enabled Include rules.
- Applications already present as exact App Activity values are not added again.
- The picker reads registered application executables from Windows uninstall registration and App Paths data.

### Notes

- Only applications for which Windows exposes a resolvable executable path are shown. Some Microsoft Store and portable applications may not appear and can still be added with Browse or by typing a process/path pattern.

## [0.1.3.1] - Folder Activity rule editor cleanup

### App Activity UI cleanup

- Removed the fixed Type selector and the Include/Exclude/Subfolders controls above the App Activity list.
- Removed the Type and Sub columns from the App Activity grid.
- Added an editable App / pattern field with Browse support for executable files.
- New App Activity rules are appended at the bottom and default to On + Include.
- App rules do not use subfolder semantics; child processes are independent process events, not sub-app rules.

- Moved the Rules sub-tabs to: Folder Activity, App Activity, File Activity, User Activity.
- Removed the Folder Activity Type selector and the Include/Exclude/Include subfolders controls above the list.
- New Folder Activity rules are appended at the bottom and default to On, Include, and Sub.
- Added an editable `File / Pattern` entry field with a folder Browse button.
- Removed the Type column from the Folder Activity rules grid while retaining the internal `folder` rule type in JSON registry storage.



## [0.1.3.1] - File Activity Include/Exclude and File Browse

### File/App rule de-duplication

- Exact enabled `.exe` rules duplicated in both File Activity and App Activity are now retained only in App Activity.
- App Activity remains authoritative for matching executable rules; wildcard file rules such as `*.exe` are not treated as duplicates of a specific app rule.


### Changed

- Removed the File Activity Include/Exclude selectors above the list; new rules are appended as enabled Include rules by default.

- Added mutually exclusive Include and Exclude checkboxes to File Activity rule rows.
- Added Include/Exclude selection when creating a File Activity rule.
- Added a Browse button that selects an exact file and inserts its full path.
- File Activity rules now use first-match-wins order: a matching Include rule monitors the file and a matching Exclude rule suppresses it.
- Added working wildcard and filename matching for patterns such as `*.exe`, `Report*.xlsx`, and `desktop.ini`.
- Clarified that files or extensions not matched by an Include rule are not monitored.

## [0.1.3.1] - 2026-07-13

### Registry settings schema revision

- Added `SettingsSchemaVersion = 2`.
- Reorganised current settings into registry subkeys: `General`, `Rules`, and `Export`.
- Changed the four activity-rule lists to JSON values under `HKCU\Software\DeskPulse\Rules`.
- Added explicit `Enabled`, `RuleType`, `Action`, `Value`, and `IncludeSubfolders` fields for each stored rule.
- Added automatic migration from legacy `ExtensionsToMonitor`, combined `LoggingRules`, and the earlier multiline activity-rule values.
- Fixed migration so an empty or unusable legacy File Activity value falls back to the existing monitored-extension list and converts entries such as `.xlsx` to `*.xlsx`.
- Preserved deliberately empty File Activity lists after schema 2 migration, meaning no files are monitored until entries are added.
- Disabled rule rows are now retained in the registry instead of being discarded when Settings is saved.
- Legacy registry values are read for migration but are no longer rewritten as the active settings format.

## [0.1.3.1] - Incremental development

### Rules type separation

- Restricted File Activity to File rules only.
- Restricted Folder Activity to Folder rules only.
- Restricted User Activity to Event rules only.
- Restricted App Activity to Process rules only.
- Added automatic redistribution of existing mixed rules into the matching activity-specific lists.
- File monitoring now evaluates both exact-file rules and folder-path rules.
- Clarified that Folder Activity is a folder-path filter for File Activity, not a separate folder event stream.

### Changed

- Added a separate `Folder Activity` tab under Settings > Rules.
- Duplicated the activity-rule editor pattern so Folder Activity has its own ordered Include/Exclude rules list.
- Added independent `FolderActivityRules` registry persistence, initially migrated from the current File Activity rules when absent.

All notable changes to DeskPulse will be documented in this file.

The format is loosely based on Keep a Changelog, and this project currently follows early pre-release versioning.


### File Activity allow-list update

- Changed File Activity into an explicit monitored-file allow-list.
- Added exact file, filename, extension wildcard (`*.exe`), and general wildcard matching.
- Removed the File Activity Type selector, Include/Exclude controls, and Include subfolders control.
- Added the note: `Files / extensions not listed are not monitored.`
- Kept folder include/exclude behavior under Folder Activity.

## [0.1.3.1] - 2026-07-13 — Activity-specific rules

### Changed

- Moved the `Storage` group from the former `Files` tab into `General`.
- Removed the now-empty top-level `Files` tab.
- Replaced the previous combined Rules layout with three activity-specific tabs: File Activity, User Activity, and App Activity.
- Added an independent ordered rules editor to each activity tab.
- File Activity rules support Folder, File, and Process matching.
- User Activity rules support event-type or event-description text matching.
- App Activity rules support Folder, File, and Process matching.
- Existing combined logging rules migrate into both File Activity and App Activity rule lists to preserve current exclusions.
- Added separate registry-backed `FileActivityRules`, `UserActivityRules`, and `AppActivityRules` settings.

## [0.1.3.1] - 2026-07-13

### Changed in Settings rules consolidation

- Renamed the top-level `Monitoring` tab to `Rules`.
- Added nested `File Activity`, `Exclusions`, and `Logging Rules` sub-tabs under `Rules`.
- Removed the visible `Monitored file types` section from Settings while preserving the existing monitored-extension setting for compatibility.
- Kept Maintenance limited to Database, Cleanup, and Diagnostics.


### Changed

- Began the active development cycle that will become version `0.1.4.0` after completion and verification.
- Added a normal Settings tab named `Monitoring`.
- Moved `File activity filters` and `Monitored file types` from `Files` to `Monitoring`.
- Kept `Files` focused on storage settings.
- Moved the former Maintenance `Statistics` contents into a normal Settings tab named `Exclusions`.
- Moved `Logging Rules` from Maintenance into a normal Settings tab.
- Reduced Maintenance sub-tabs to `Database`, `Cleanup`, and `Diagnostics`.
- Updated active application, project, manifest, and package documentation version references to `0.1.3.1`.


## [0.1.3.0] - 2026-07-07

### Summary

Version 0.1.3.0 is locked as the all-forms designer-editable source baseline and refreshes the project documentation so the package consistently presents 0.1.3.0 as the current baseline.

### Changed

- Updated the application version constant to `0.1.3.0`.
- Updated project metadata in `DeskPulse.csproj` to `0.1.3.0`.
- Updated the application manifest assembly identity version to `0.1.3.0`.
- Updated README, HANDOVER, ROADMAP, and package verification references to `0.1.3.0`.
- Moved the suggested next-development version to `0.1.4.0`.
- Split the main WinForms UI classes out of the large `Program.cs` file into a dedicated `Forms` folder.
- Converted `AboutForm`, `ExportDateRangeForm`, and `MaintenanceProgressForm` to designer-compatible partial classes with `.Designer.cs` and `.resx` files.
- Converted the normal `SettingsForm` shell and tab contents to a designer-compatible partial class with `SettingsForm.Designer.cs` and `SettingsForm.resx`.
- The `SettingsForm` window, main tab container, General tab, Files tab, Export Options tab, footer line, Save button, and Cancel button can now be opened and adjusted in the Visual Studio WinForms Designer.
- Kept only runtime data population code in `SettingsForm.cs`; the visible Settings and Maintenance layouts now live in `SettingsForm.Designer.cs`.
- Maintenance UI contents are now designer-backed inside `SettingsForm.Designer.cs`; `MaintenanceForm` remains only a thin launcher/wrapper for maintenance mode.
- Fixed Visual Studio Designer parsing by removing null-forgiving operators from `InitializeComponent()` assignments.

### Notes

- Older changelog entries are preserved as project history.
- The package was source-prepared only; compile and runtime testing must be done locally because the AI environment does not include the .NET SDK.

## 0.1.3.0 Maintenance/Data-Management Baseline - 2026-07-06

### Summary

This baseline adds the first practical hidden Maintenance workspace for database management, statistics, cleanup, exclusions, and diagnostics.

### Changed

- Reordered Maintenance > Cleanup so the lowest-risk generated-file cleanup appears first, rule-based unwanted-data cleanup appears second, and full record deletion appears last.
- Separated Maintenance into its own right-click form instead of showing it as a tab inside normal Settings.
- Normal Settings now contains only General, Files, and Export Options.

- Removed the redundant top-level manual folder action checkboxes from Maintenance > Logging Rules; per-row Include/Exclude/Sub checkboxes are now the only rule controls.
- Widened the folder-exclusion rules table and made the Folder path column fill the available width for easier reading.
- Changed the Settings form and exclusion rules grid to use the Windows message-box font instead of a fixed custom form font.

- Updated the application version constant to `0.1.3.0`.
- Updated project metadata in `DeskPulse.csproj` to `0.1.3.0`.
- Updated the application manifest assembly identity version to `0.1.3.0`.
- Extended hidden Maintenance mode so it can be opened with `-maintenance` or `-m`.
- Reworked the hidden Maintenance tab into sub-tabs.

### Fixed / Changed in Settings designer follow-up

- Moved the visible `SettingsForm` General, Files, and Export Options tab contents into `SettingsForm.Designer.cs` so they are visible and editable in the Visual Studio WinForms Designer.
- Kept runtime loading for saved values, file-extension lists, and export field sub-tabs.
- Converted the visible Maintenance tab and sub-tabs to designer-backed controls in `SettingsForm.Designer.cs`; maintenance data loading remains in code.

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

- Locked `0.1.3.0` as the maintenance/data-management release baseline; future feature changes should move to `0.1.4.0` unless a critical 0.1.3.0 hotfix is required.
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
  - `DeskPulse.ico`
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

- Added JSON import/export for File Activity, Folder Activity, and App Activity rules.
- Obsolete Maintenance source files are explicitly excluded from compilation if they remain in an older working folder.

### 0.1.3.2 — Log View sort routing fix
- Fixed User Activity column sorting incorrectly using File Activity database columns when a grid's runtime `Name` property was unavailable.
- Sort-column routing now uses the actual grid instance, preventing SQLite errors such as `no such column: InferredAction` on the User Activity tab.

### 0.1.3.2 - Event Type removal
- Removed the Event Type column from File Activity, App Activity, and User Activity in View Log.
- Removed Event Type from App Activity and User Activity database tables, insert statements, exports, detail views, sorting, and maintenance statistics.
- Added an automatic database migration that drops the obsolete EventType columns and indexes while preserving the remaining records.

### User lifecycle activity logging
- Added `DeskPulse started (possible login)` and `DeskPulse stopped` to User Activity.
- Added one `Windows started` record per detected Windows boot.
- Retained Windows user logon, logoff, lock, unlock, console, and remote-session activity under User Activity.
- Required lifecycle rules are enabled automatically for existing settings.

### 0.1.3.2 - Report date defaults
- All report date selectors now default from the earliest record in the DeskPulse database through today.
- Added a `Today Only` button to View Log and the activity export date dialog; it changes the From/Start date to today.

### 0.1.3.2 - Optional exclusion rules when deleting log records
- Replaced the View Log delete confirmation message with a confirmation dialog that includes a `Create exclusion rule(s)` checkbox.
- The checkbox is off by default, so records can be deleted without changing future logging rules.
- When selected, DeskPulse creates unique exclusion rules for the deleted File Activity, App Activity, or User Activity records.
- The same option applies to single-record and multi-record deletion on all three View Log tabs.

### View Log pagination status
- The status line now shows the exact visible record range and filtered total, for example `Showing 1,001 to 1,500 of 8,999 records.`
- The displayed range updates when paging, sorting, refreshing, changing dates, or switching activity tabs.

- Added **Restart Windows Service** under Settings → Maintenance. The action requests administrator approval, restarts `DeskPulse.Service`, waits for it to return to Running, and reports the resulting service status.
- Corrected the Inno Setup service name to `DeskPulse.Service` so installer maintenance actions target the same service registered by the deployment scripts.

- Fixed service/tray named-pipe permissions so a normal non-elevated tray user can query status and issue pause/resume/reload commands to the LocalSystem service.

### 0.2.0.0 - Single active tray window
- Opening View Log, Settings, or About from the tray now closes any other DeskPulse top-level window first.
- Tray-opened windows are non-modal, allowing the user to switch directly between DeskPulse windows from the tray menu.

### 0.2.0.0 build correction
- Fixed service restore failure by changing `System.IO.Pipes.AccessControl` from unavailable version `8.0.0` to stable version `5.0.0`.


## Absolute data-path migration

DeskPulse 0.2.0.1 normalizes legacy relative data paths to an absolute path under the interactive user's Documents folder. The installer initializes shared settings as the original user before starting the LocalSystem service. The default database remains `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db`.
