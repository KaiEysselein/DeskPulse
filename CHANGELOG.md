# Changelog

## 0.4.0.6 - 2026-07-31

- Separated Calendar records into Files, Apps and User Activity tabs.
- Limited each Calendar tab and export to its corresponding activity type.
- Changed Calendar to load only records explicitly marked for Calendar view.
- Removed the former All records/Marked records toggle and its saved preference.
- Made grouping and expanded/collapsed group state independent for each Calendar tab.
- Fixed erratic cross-tab behaviour after double-click grouping or collapse actions.
- Removed the installer's Additional Tasks page.
- Enabled the desktop shortcut and startup message by default on fresh installations.
- Preserved existing desktop-shortcut and startup-message choices during upgrades and same-version reinstalls.
- Expanded the automated suite to 81 passing tests.
- Cleaned the repository and technical handover material to remove stale generated output and obsolete release instructions.

## 0.4.0.5 - 2026-07-30

- Added collapsible grouped rows to User Activity by date, event, user or computer.
- Applied expand/collapse, group summaries, Calendar marking and grouped deletion consistently across File, App and User Activity tables.
- Added regression coverage for every User Activity grouping header.

## 0.4.0.4 - 2026-07-30

- Ensured Calendar View loads File, App and User Activity through one tested multi-source query.
- Improved Calendar App and User rows with meaningful item and detail fallbacks.
- Added double-click grouping, summarising and ungrouping for the Calendar Details column.
- Added regression coverage for all Calendar activity sources and Details grouping.

## 0.4.0.3 - 2026-07-30

- Added an animated progress window for Log and Calendar loading, paging, sorting, grouping, ungrouping, expansion, collapse, and record-filter changes.
- Runs the progress animation on an independent UI thread so it remains visibly active while large record sets are processed.

## 0.4.0.2 - 2026-07-30

- Replaced the separate Data View and Calendar View controls with one destination-labelled toggle button.
- Added a single All records/Marked records Calendar filter that remembers the current user's choice.
- Added double-click Calendar header grouping and ungrouping by date, hour, activity, or item.
- Added expandable and collapsible Calendar group rows.
- Renamed the Calendar date checkbox to All dates to distinguish it from the record filter.

## 0.4.0.1 - 2026-07-30

- Added a three-second, click-to-dismiss startup status message near the system tray.
- Added fresh-install and Current User Settings controls for the per-user startup message preference while preserving it during upgrades.
- Moved the 24-hour versus AM/PM preference into Current User Settings and applied it to Log and Calendar views.
- Integrated Calendar View into the Log window behind a remembered Data View / Calendar View toggle.
- Removed the standalone current-user and administrator Calendar tray-menu commands.
- Added calendar-date handoff to Data View and explicit export of the displayed Calendar records.
- Moved Export to the top toolbar and Records per page to the far left of the paging toolbar.
- Improved compact-window toolbar layout and retained double-click header grouping/ungrouping.

## 0.4.0.0 — 2026-07-29

- Added **Current User → Calendar View** to the tray menu.
- Added elevated **Administrator → System Calendar View** for Calendar-marked system records.
- Calendar windows now run independently, refresh when activated and return to the foreground when selected again.
- Enabled administrator-authorized Calendar marking in System Log while keeping all other system fields read-only.
- Calendar views now open maximized.
- Replaced the Group by dropdown with direct header gestures: single-click sorts and double-click groups or ungroups supported columns.

## 0.3.4.7 — 2026-07-29

- Routed individual and grouped Calendar updates through the authenticated DeskPulse service.
- Fixed “attempt to write a readonly database” when marking records in the current-user log.
- Added batching for large grouped Calendar updates while sharing the service database lock with live logging.

## 0.3.4.6 — 2026-07-29

- Added a persistent **Calendar** checkbox to File, App and User Activity records.
- Added three-state Calendar checkboxes to grouped File and App Activity summaries for safe bulk marking.
- Added Calendar View with an all-marked overview, bold marked dates and selected-day filtering.
- Added automatic migration of existing databases with unchecked calendar flags.

## 0.3.4.5 — 2026-07-29

- Adds a cell-aware right-click menu to activity reports for deleting records and creating rules.
- Pre-populates filename, extension, folder, application-name and executable-path rules from the clicked field.
- Routes File Activity application exclusions through the enforced filtered-applications list used by live logging and historical cleanup.
- Aligns form titles, labels, hints, confirmations, maintenance terminology, and storage guidance with current behavior.
- Separates deletion from rule creation and makes cancellation the safe default in deletion confirmations.

## 0.3.4.4 — 2026-07-29

- Advances the completed System/User attribution, historical preview, grouping, responsive rule handling, and PowerShell-free installation work to the 0.3.4.4 maintenance release.
- Hides unused columns while all grouped rows are collapsed and restores detail columns when a group is expanded.
- Allows permanent deletion of grouped records behind two confirmation steps and a prominent historical-data warning.
- Supports safe full-path app wildcards, including single-level `*` and recursive `**` folder matching.
- Opens log views with the complete available history selected by default.
- Refines the grouped-deletion prompt to show the affected record and group counts without exposing confirmation workflow details.
- Fixes the About window's OK button and Escape key so the modeless tray window closes correctly.

All notable DeskPulse changes are recorded here. Historical verification records under `dev\docs` remain unchanged.

## 0.3.4.3 — 2026-07-29

- Added sortable grouped record counts and rule creation for supported grouped columns.
- Prevented rule creation from freezing while the service reloads settings.
- Added a bounded timeout while waiting for service pipe responses.

## 0.3.4.2 — 2026-07-28

- Removed PowerShell execution from the installer and running tray.
- Replaced scheduled-task startup registration with a standard all-users Startup shortcut.
- Made retroactive rule pre-checks asynchronous and process-rule aware.

## 0.3.4.1 — 2026-07-28

### Added

- Added full-result App Activity grouping and expandable groups by date, application, process ID and path in both Current User and Administrator logs.
- Added process-token owner attribution, explicit `route_system`/`route_user` policy actions and a read-only historical attribution preview.
- Replaced the log's Today Only action with exact-time presets from Today and Last 24 Hours through ten years.

### Fixed

- File and App Activity grouping now aggregates and sorts the complete selected date range before applying page limits.
- Newly created rules are reloaded by the service before retroactive cleanup begins and are activated immediately when cleanup is not requested.
- System-attributed File Activity now uses system settings throughout filtering and storage instead of inheriting the active user settings snapshot.

## 0.3.4.0 — 2026-07-28

### Added

- Moved machine-wide Windows path and process exclusions from hard-coded lists into a versioned `default-rules.yaml`.
- Added a protected, upgrade-preserved `%ProgramData%\DeskPulse\Config\admin-rules.yaml` for administrator Include exceptions, Exclude additions and default-rule overrides.
- Added automatic validated rule reload, last-known-good behavior and protected rule validation diagnostics.
- Added a per-rule `visible_in_ui` YAML setting that controls rule-grid visibility independently of enforcement.
- Added a dynamic Machine-wide Rules grid to elevated Administrator Settings, populated from the effective YAML rules with source, status, visibility and reason details.
- Added form and tab-page scrolling so controls remain reachable on compact or highly scaled displays.
- Added default-rule revisions and administrator override review warnings when a shipped default changes.
- Added bounded, privacy-safe aggregate candidate diagnostics containing process names and file extensions, but no full paths, user names, SIDs or event contents.
- Added automated rule fallback, override, visibility, revision and WinForms layout tests.

## 0.3.3.2 — 2026-07-27

### Changed

- Current-user and system Excel exports now show progress in a dedicated modal window.
- The owning Log window closes after a successful export and remains open when export is cancelled or fails.

### Fixed

- Tray-menu commands are deferred until the context menu closes, restoring reliable first-click opening for every form and action.
- The Windows 11 hidden-icons flyout is dismissed before the DeskPulse menu opens and can no longer cover it.
- Normal, Paused and Warning tray-state icons are included in published and installed builds, allowing Pause Logging to display the correct icon.

## 0.3.3.1 — 2026-07-27

### Fixed

- Standalone Current User Log, Current User Settings, System Log and System Settings windows now fit within the active screen's working area.
- Standalone windows are explicitly centered, restored, activated and brought to the foreground after opening, preventing a taskbar-only window on compact or highly scaled displays.

## 0.3.3.0 — 2026-07-23

### Added

- Added protected system and per-user ProgramData databases, settings and rule ownership keyed by Windows SID.
- Added event scope, SID and Windows-session attribution with simultaneous-session routing.
- Added service-side named-pipe client identity, installation-path and elevation authorization.
- Added isolated current-user Log and Settings plus UAC-elevated System Log and System Settings and Maintenance.
- Added an optional **Log folder openings** setting that preserves extensionless-file logging.
- Added complete active-tab/date-range export with progress reporting.

### Changed

- The tray now groups ordinary actions under **Current User** and machine-wide actions under **Administrator**.
- Current-user actions are named **Log...** and **Settings...**, with hover text identifying their scope.
- Only one DeskPulse form may be open from the tray at a time.
- Per-user maintenance targets only the verified caller's SID database; administrator maintenance targets only the protected system database.
- The all-users scheduled tray task supports parallel Windows sessions and uses the installed tray working directory.

### Fixed

- Service startup now succeeds when no interactive console user is present.
- Settings storage saves no longer attempt to rewrite a read-only parent SID-folder ACL.
- Legacy single-user database migration is guarded so only the first SID receives historical data.

## 0.3.2.0 — 2026-07-22

### Added

- Added an **Administrator settings...** tray action that starts the same executable as a separate process through the Windows UAC `runas` flow.
- Added explicit validation that `--administrator-settings` is running with an elevated administrator token.

### Changed

- Ordinary Settings now remains unelevated and exposes only the user-facing General and Rules pages.
- The elevated, short-lived Administrator settings window exposes only Maintenance and ends administrator access when it closes.
- Service-side named-pipe authorization and the ProgramData system/per-user database architecture are deferred to 0.3.2.x; this release does not present the UI split as a complete security boundary.

### Fixed

- View Log now remains open while its native Save dialog and Excel export workflow have focus, preventing export from being cancelled by the tray focus-loss timer.

## 0.3.1.0 — 2026-07-20

### Added

- User Activity records for first installation, version update and same-version reinstallation.
- Release verification documents for 0.3.1.0.

### Fixed

- **Clean database with current rules...** no longer causes the Settings window to disappear before confirmation.
- Settings is no longer closed by the generic tray focus-loss mechanism.
- Normal, Paused and Warning tray icons now use genuine transparent PNG and ICO assets without checkerboard or rectangular backgrounds.
- Diagnostic-load, service-resource-warning and critical-safety-pause events are now enabled and migrated into User Activity rules.
- Diagnostic CPU workers are phase-staggered with 49% duty headroom so the service remains below the advertised 50% hard cap.

### Changed

- Promoted the completed 0.3.0.1 correction work to release version 0.3.1.0.
- Updated application, assembly, publish, installer, retained-release and GitHub documentation references to 0.3.1.0.
- Release builds whose fourth version component is zero are retained under `releases\v<version>`.

## 0.3.0.0 — 2026-07-16

- Promoted the tested 0.2.2.3 service-safeguard baseline.
- Added configurable sustained CPU and RAM warning and critical thresholds.
- Added critical safety pause, optional restart persistence and explicit Resume Logging recovery.
- Added controlled service-load diagnostics with service-side 50% CPU and RAM caps.

## Earlier versions

Earlier release and verification history remains available under `dev\docs\archive` and `dev\docs\verification`.
