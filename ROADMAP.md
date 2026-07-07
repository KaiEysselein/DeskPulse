# DeskPulse Roadmap

This roadmap tracks planned work and ideas for future DeskPulse versions.

The roadmap is not a changelog. Completed changes belong in `CHANGELOG.md`. Detailed bugs, screenshots, test notes, and discussion should preferably be tracked as GitHub Issues.

---

## Current locked release baseline

```text
0.1.3.0
```

Version `0.1.3.0` is now treated as the locked all-forms designer-editable source baseline.

After this package, new feature work, UI changes, refactoring, and bug-fix iterations should be tracked as `0.1.4.0` work unless a critical 0.1.3.0 hotfix is explicitly needed.

The 0.1.3.0 baseline includes:

- selected file activity logging
- user/session activity logging
- program start/close logging
- configurable Excel export worksheets and fields
- date-range export
- export progress percentage/status text
- general settings including optional start with Windows logon
- split tray menus
- dedicated Maintenance form available from the right-click tray menu when started with `-maintenance` or `-m`
- Maintenance database statistics, cleanup, logging rules, and diagnostics
- ordered Logging Rules with first-match-wins behaviour
- Folder, File, and Process/Program rules
- Include/Exclude actions per rule
- subfolder matching for folder rules
- Statistics Top 100 views with multi-select and right-click rule creation
- targeted removal of unwanted past data based on current Exclude rules
- generated-file cleanup for export/log housekeeping
- quieter normal operation without notification balloons
- Visual Studio Designer-compatible forms for the visible UI surfaces
- designer-compatible forms for About, Export Date Range, Maintenance Progress, and normal Settings
- designer-backed Settings General, Files, and Export Options tab contents

---

## Completed in 0.1.3.0 — Data Management and Logging Filters

Implemented work:

- Added hidden Maintenance mode using `-maintenance` and `-m`.
- Separated Maintenance into its own form opened from the right-click tray menu in maintenance mode.
- Kept normal Settings limited to General, Files, and Export Options.
- Added Maintenance > Database overview with database path, size information, and record counts.
- Added Maintenance > Statistics with Top 100 views for full paths, folders, file processes, extensions, and program events.
- Added Statistics multi-select using normal Windows Shift/Ctrl selection behaviour.
- Added Statistics buttons and context menu actions to create File, Folder, and Process/Program logging rules from selected rows.
- Added Maintenance > Cleanup with safer visual order: generated files first, rule-based unwanted-data removal second, and full record deletion last.
- Added generated-file cleanup for Excel export, diagnostic log, and startup fallback log.
- Added full category cleanup for file activity, user/session activity, program activity, and all activity records.
- Added rule-based `Remove Unwanted Data...` cleanup for past file/program records matching current Exclude rules.
- Added destructive-action warning dialogs and determinate progress for unwanted-data deletion.
- Added Maintenance > Logging Rules as a single ordered rule manager.
- Added Folder, File, and Process/Program rule types.
- Added Include and Exclude actions per rule.
- Added subfolder matching for folder rules.
- Added Browse support for folder and file/program rule entry.
- Added Move Up, Move Down, Remove, Duplicate, and Reset Defaults rule management actions.
- Added `LoggingRules` registry value while preserving legacy settings compatibility.
- Added default noisy Windows/temp/cache/process rules.
- Added rule checks to file logging and program activity logging.
- Added Maintenance > Diagnostics including startup task status.
- Added button hover tooltips explaining exactly what each action deletes or preserves.
- Split UI forms into the `Forms` folder.
- Converted small utility forms to Visual Studio Designer-compatible partial classes.
- Converted the normal Settings window shell, General tab, Files tab, Export Options tab, footer, Save button, and Cancel button to `SettingsForm.Designer.cs`.

Notes:

- Database clearing preserves the database file, table structure, and indexes but removes selected records.
- `Remove Unwanted Data...` keeps the rules themselves and deletes only matching past file/program records.
- Filtering should avoid hiding important user-created files by default.
- Future destructive actions must have clear button text, hover tooltips, and confirmation dialogs.

---

## Suggested next version: 0.1.4.0 — Stabilisation, UI Polish, and Export Improvements

Note: `AboutForm`, `ExportDateRangeForm`, `MaintenanceProgressForm`, and the visible `SettingsForm`/Maintenance tabs are designer-compatible. `MaintenanceForm` remains a thin launcher/wrapper; edit the Maintenance UI through `SettingsForm.cs` > View Designer.

Candidate work for 0.1.4.0:

- Compile-test and runtime-test the locked 0.1.3.0 package locally after replacing files.
- Rebuild the Maintenance UI in a Visual Studio Designer-compatible layout.
- Tidy remaining form spacing and reduce unnecessary whitespace across all forms.
- Improve resizing/anchoring of tables and path columns.
- Add export cancellation.
- Add export preview/summary before opening Excel.
- Improve export performance on large date ranges.
- Add warning if selected date range contains a very large number of rows.
- Add export file naming options.
- Add optional export to a user-selected folder.
- Add last-used export date range setting.
- Improve user-facing wording on Maintenance warnings and confirmations if testing shows ambiguity.
- Review whether any 0.1.3.0 logging rules should become default rules.

---

## Suggested later version: 0.1.5.0 — Startup and Deployment Polish

Candidate work:

- Improve Task Scheduler startup handling and diagnostics.
- Add a status indicator showing whether Windows startup is enabled and valid.
- Detect if the saved startup path no longer exists.
- Add clearer guidance for portable-folder deployment.
- Stabilize publish-folder packaging.
- Investigate installer packaging.

---

## Suggested later version: 0.2.0.0 — Program Activity Enhancements

Candidate work:

- Improve program start/close detection accuracy.
- Add more useful program metadata where available.
- Add program grouping by executable name.
- Add duration tracking for program sessions.
- Add summary worksheet for program usage.
- Add configurable include/exclude list for program logging.

---

## Email logging idea — not currently planned for 0.1.x

Email logging is technically possible only in limited scenarios and is more privacy-sensitive than file/program logging.

Candidate approach for a future version, if ever added:

- Outlook sent-email metadata only.
- Disabled by default.
- Current user only.
- No email body storage.
- No hidden/stealth monitoring.
- Clear warning in settings.

Email read/open logging is not recommended for the current DeskPulse roadmap.

---

## Bug tracking recommendation

Use GitHub Issues for live bugs and detailed future tasks.

Suggested labels:

```text
bug
enhancement
maintenance
privacy
export
database
logging-filter
program-activity
ui
v0.1.3.0
v0.1.4.0
```

Suggested issue examples:

```text
Bug: Maintenance table column does not resize on small screens
Bug: Startup task path becomes invalid after moving portable folder
Feature: Add export cancellation
Feature: Add export preview/summary
Feature: Add user-selected export folder
Feature: Add program duration tracking
```
