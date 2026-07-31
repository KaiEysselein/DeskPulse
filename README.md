# DeskPulse

## Local Windows activity logging with clear user and administrator boundaries

**DeskPulse** is an open-source Windows activity logger built around a privileged Windows service and a lightweight per-user tray application.

It records selected file, application, user-session and Windows activity in local SQLite databases while giving users direct control over filtering, pausing, reviewing, cleaning and exporting their own records.

**Current version:** `0.4.0.6`

[Download the latest DeskPulse installer](https://github.com/KaiEysselein/DeskPulse/releases/latest)

## Highlights in 0.4.0.6

- Calendar records are separated into **Files**, **Apps** and **User Activity** tabs.
- Each Calendar tab displays and exports only its own activity type.
- Calendar loads only records explicitly marked for Calendar view.
- Grouping and expanded/collapsed group state are independent for each Calendar tab.
- The obsolete All records/Marked records Calendar toggle and saved preference were removed.
- Installer upgrades no longer show an Additional Tasks page.
- Fresh installs enable the desktop shortcut and startup message by default.
- Upgrades and same-version reinstalls preserve the existing shortcut and startup-message choices.
- The automated suite now contains **81 passing tests**.

## Architecture

DeskPulse separates privileged monitoring from the desktop interface:

- **DeskPulse.Service** runs automatically in the background, monitors activity and is the sole SQLite writer.
- **DeskPulse.Tray** provides current-user controls and separate UAC-elevated system administration entry points.
- **DeskPulse.Shared** contains shared settings, rules, models, database access and monitoring logic.

Activity data remains local unless a user explicitly exports it.

## Main capabilities

- File and folder activity logging
- Application activity logging
- Windows startup, shutdown, lock, unlock, logon and logoff events
- DeskPulse install, update and reinstall activity records
- Rule-based Include and Exclude filtering
- Application-aware File Activity filtering
- Optional folder-opening suppression without suppressing extensionless files
- Paged log views with details, grouping, deletion and Excel export
- Integrated Records and Calendar layouts
- Calendar marking and separate Files, Apps and User Activity tabs
- Independent grouping and expansion state per Calendar tab
- Animated progress feedback for large view operations
- Per-user startup status message and 24-hour/AM-PM preference
- Database cleanup using the current rules
- Pause and resume control
- Service CPU and RAM safeguards
- Controlled diagnostic load tests, hard-capped at 50% CPU and RAM
- Protected system and per-user storage under `%ProgramData%\DeskPulse`
- Windows SID, scope and session attribution
- Service-side named-pipe client authorization
- Separate current-user and elevated system log, settings and maintenance interfaces

## Data and security boundaries

DeskPulse deliberately provides no combined all-users log.

- Current-user views and maintenance target only the calling user's SID database.
- Elevated administrator views and maintenance target only the protected System database.
- DeskPulse does not expose another user's personal database through its administrator interface.
- State-changing commands are authorized by the service.
- The Windows service owns every SQLite write, including deletion, cleanup and lifecycle records.

## Data locations

| Purpose | Location |
|---|---|
| System database | `%ProgramData%\DeskPulse\System\DeskPulse-System.db` |
| System settings | `%ProgramData%\DeskPulse\System\settings.json` |
| Current-user database | `%ProgramData%\DeskPulse\Users\<Windows-SID>\DeskPulse.db` |
| Current-user settings | `%ProgramData%\DeskPulse\Users\<Windows-SID>\Settings\settings.json` |
| Shipped default rules | `%ProgramFiles%\DeskPulse\Config\default-rules.yaml` |
| Administrator rule overrides | `%ProgramData%\DeskPulse\Config\admin-rules.yaml` |
| Administrator rule diagnostics | `%ProgramData%\DeskPulse\System\admin-rules-error.log` |
| Aggregate rule candidates | `%ProgramData%\DeskPulse\System\rule-candidates.csv` |
| Exports | User-selected location |

The uninstaller removes the application, service and startup registration while preserving system and per-user databases.

## Activity filtering

DeskPulse supports user-defined Include and Exclude rules for file, folder, application and user activity.

Machine-wide YAML rules protect against high-volume background activity when **Track Windows system activity** is disabled. Shipped defaults live under the protected installation folder and are updated with DeskPulse releases. Local administrator overrides live under protected ProgramData storage and are preserved during upgrades.

Administrator rules are evaluated before shipped defaults, and the first matching rule wins. Supported actions are `include`, `exclude`, `route_system` and `route_user`. Routing writes an event to exactly one database. System routing takes precedence over normal user attribution.

Each YAML rule can also set `visible_in_ui: true` or `false`. This controls only whether the rule appears in Settings; it does not change whether the service enforces it.

## Status icons

| State | Meaning |
|---|---|
| **Normal** | Logging is active |
| **Paused** | Logging is paused |
| **Warning** | The service or safeguard state requires attention |

Shared transparent PNG and ICO resources are stored under `dev\Resources`.

## Build and verification

DeskPulse targets .NET 8 for Windows. The current release was built, published, packaged and installed successfully, with **81 automated tests passing**. Release-specific verification is recorded in [`VERSION_CHECK.md`](VERSION_CHECK.md).

## Project links

- [Releases](https://github.com/KaiEysselein/DeskPulse/releases)
- [Changelog](CHANGELOG.md)
- [Roadmap](ROADMAP.md)
- [Backlog](BACKLOG.md)
- [Stable and Nightly channels](RELEASE_CHANNELS.md)
- [License](LICENSE)

DeskPulse is licensed under the **GNU General Public License v3.0**.
