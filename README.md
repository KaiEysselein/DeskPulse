# DeskPulse

## Windows activity logging without the clutter

**DeskPulse** is a Windows activity logger built around a background Windows service and a lightweight per-user tray application.

It records selected file, application, user-session and Windows activity in a local SQLite database while giving the user direct control over filtering, pausing, reviewing, cleaning and exporting recorded data.

**Current version:** `0.3.4.0`

[Download the latest DeskPulse installer](https://github.com/KaiEysselein/DeskPulse/releases/latest)


## What DeskPulse does

DeskPulse separates privileged monitoring from the normal desktop interface:

- **DeskPulse.Service** runs in the background, monitors activity and owns all database writes.
- **DeskPulse.Tray** provides isolated current-user and UAC-elevated system log, settings, export and maintenance interfaces.
- **DeskPulse.Shared** contains common settings, rules, models, database access and monitoring logic.

DeskPulse is designed for local use. Activity data remains on the computer unless the user explicitly exports it.

## Main capabilities

- File and folder activity logging
- Application activity logging
- Windows startup, shutdown, lock, unlock, logon and logoff events
- DeskPulse install, update and reinstall activity records
- Rule-based Include and Exclude filtering
- Configurable Windows-system activity suppression
- Application-based File Activity filtering
- Paged log views with details and export
- Database cleanup using the current rules
- Pause and resume control
- Service CPU and RAM safeguards
- Controlled safeguard diagnostic tests, hard-capped at 50% CPU and RAM
- Windows service status and maintenance controls
- Separate short-lived administrator processes for system log, settings and maintenance
- Per-user and system SQLite storage under protected `%ProgramData%\DeskPulse` folders
- Windows-SID and session attribution for activity records
- Service-side named-pipe client authorization
- Optional folder-opening suppression without suppressing extensionless files

## Status icons

| State | Meaning |
|---|---|
| **Normal** | Logging is active |
| **Paused** | Logging is paused |
| **Warning** | The service or safeguard state requires attention |

The shared transparent PNG and ICO resources are stored under `dev\Resources`.

## Data locations

| Purpose | Location |
|---|---|
| System database | `%ProgramData%\DeskPulse\System\DeskPulse-System.db` |
| System settings | `%ProgramData%\DeskPulse\System\settings.json` |
| Shipped default rules | `%ProgramFiles%\DeskPulse\Config\default-rules.yaml` |
| Administrator rule overrides | `%ProgramData%\DeskPulse\Config\admin-rules.yaml` |
| Administrator rule diagnostics | `%ProgramData%\DeskPulse\System\admin-rules-error.log` |
| Aggregate rule candidates | `%ProgramData%\DeskPulse\System\rule-candidates.csv` |
| Current-user database | `%ProgramData%\DeskPulse\Users\<Windows-SID>\DeskPulse.db` |
| Current-user settings | `%ProgramData%\DeskPulse\Users\<Windows-SID>\Settings\settings.json` |
| Exports | User-selected location |

The uninstaller removes the application, service and startup registration while preserving system and per-user databases.

## 0.3.4 release boundary

Version 0.3.4.0 adds protected, data-driven machine-wide rule policy, administrator override and fallback handling, rule diagnostics and a dynamic administrator rule view. It also makes forms and tab pages scrollable on compact or highly scaled displays.

## Activity filtering

DeskPulse supports user-defined Include and Exclude rules for file, folder, application and user activity.

Machine-wide YAML rules protect against high-volume background activity when **Track Windows system activity** is disabled. Shipped defaults live under the protected installation folder and are updated with DeskPulse releases. Local administrator overrides live under protected ProgramData storage and are preserved during upgrades.

Administrator rules are evaluated before shipped defaults, and the first matching rule wins. An administrator can add an Include exception before a broad Exclude, add new exclusions, or replace and disable a shipped rule by using the same rule ID. Changes are validated and detected automatically. If an edit is invalid, DeskPulse retains the last complete valid rules and records the problem in `admin-rules-error.log`.

Each YAML rule also supports `visible_in_ui: true` or `false`. This controls only whether the rule appears in the existing Settings rule grids; it never changes whether the service enforces the rule. The property defaults to `true`.

Elevated Administrator Settings includes a dynamically populated **Machine-wide Rules** page. It shows the effective YAML rule values, sources, enabled and visibility states, reasons, validation status, and actions to reload or open the relevant configuration and diagnostics files.

The YAML files currently contain only machine-wide path and process rules. Current-user preferences and personal rules remain in their SID-specific JSON settings file.

File Activity can also be filtered by the application responsible for the event, allowing repetitive activity from applications such as Windows File Explorer to be suppressed while retaining activity generated by other programs.

## Database ownership

All SQLite write operations are performed by `DeskPulse.Service`, including normal logging, record deletion, rule cleanup, database housekeeping and installation lifecycle records. The tray opens the database read-only for views, counts, statistics and exports.

## Project links

- [Releases](https://github.com/KaiEysselein/DeskPulse/releases)
- [Change log](CHANGELOG.md)
- [Roadmap](ROADMAP.md)
- [Backlog](BACKLOG.md)
- [License](LICENSE)

DeskPulse is licensed under the **GNU General Public License v3.0**.
