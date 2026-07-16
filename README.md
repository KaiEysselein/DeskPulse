# DeskPulse

<table>
<tr>
<td width="68%" valign="top">

## Windows activity logging without the clutter

**DeskPulse** is a Windows activity logger built around a background Windows service and a lightweight per-user tray application.

It records selected file, application, user-session, and Windows activity into a local SQLite database while giving the user direct control over filtering, pausing, reviewing, cleaning, exporting, and service-safety recovery.

**Current version:** `0.3.0.0`

### [Formal milestone releases](https://github.com/KaiEysselein/DeskPulse/releases)

</td>
<td width="32%" align="center" valign="top">

<img src="dev/Resources/DeskPulse_Normal.png" alt="DeskPulse" width="260">

</td>
</tr>
</table>

## Repository structure

```text
DeskPulse\
├── dev\
│   ├── Installer\
│   ├── Resources\
│   ├── scripts\
│   ├── src\
│   └── DeskPulse.sln
├── releases\
└── project documentation in the repository root
```

- [`dev`](dev) contains application source code, build scripts, installer definitions, shared resources, and technical verification records.
- `releases` is the local artifact area for the latest approved intermediate installer and retained `v0.x.0.0` milestones.
- GitHub-facing documentation remains in the repository root.

## What DeskPulse does

DeskPulse separates privileged monitoring from the normal desktop interface:

- **DeskPulse.Service** runs in the background, monitors activity, owns all database writes, and enforces service-resource safeguards.
- **DeskPulse.Tray** provides the tray menu, Settings, View Log, Export, Maintenance, diagnostics, and recovery controls.
- **DeskPulse.Shared** contains common settings, rules, models, database access, and monitoring logic.

Activity data remains on the computer unless the user explicitly exports it.

## Main capabilities

- File and folder activity logging
- Application activity logging
- Windows startup, shutdown, lock, unlock, logon, and logoff events
- Rule-based Include and Exclude filtering
- Configurable Windows-system activity suppression
- Application-based File Activity filtering
- Paged log views with record details and export
- Database cleanup using the current rules
- Temporary and persistent logging pause states
- Service CPU/RAM warning and critical safeguards
- Controlled diagnostic service-load tests, hard-capped at 50%
- Persistent critical safety pause with explicit recovery
- Windows service status and maintenance controls
- Local SQLite storage

## Status icons

| State | Meaning |
|---|---|
| **Normal** | Logging is active |
| **Paused** | Logging is paused |
| **Warning** | The service or safeguard state requires attention |

Shared artwork and icon resources are stored under [`dev/Resources`](dev/Resources).

> **Known visual issue:** Some current tray-state icon assets may show a non-transparent background on certain Windows themes. This does not affect logging or safeguard operation.

## Data locations

| Purpose | Location |
|---|---|
| Shared settings | `%ProgramData%\DeskPulse\settings.json` |
| Activity database | `%USERPROFILE%\Documents\DeskPulse\DeskPulse.db` |
| Exports | `%USERPROFILE%\Documents\DeskPulse` |

The uninstaller removes the application, service, startup registrations, and application settings. The activity database and exports under `Documents\DeskPulse` are intentionally preserved.

## Database ownership

All SQLite write operations are performed by `DeskPulse.Service`, including normal activity logging, deletion, cleanup, housekeeping, schema migration, and compaction. The tray opens the database read-only for views, counts, statistics, and exports.

## Release policy

DeskPulse keeps permanent releases only for milestone versions matching `v0.x.0.0`, such as `v0.2.0.0` and `v0.3.0.0`.

Intermediate builds retain their exact application version, but their approved installer replaces the previous contents of the local `releases\current` folder. They are not retained as permanent GitHub Releases.

## Project links

- [Releases](https://github.com/KaiEysselein/DeskPulse/releases)
- [Change log](CHANGELOG.md)
- [Roadmap](ROADMAP.md)
- [Backlog](BACKLOG.md)
- [License](LICENSE)

DeskPulse is licensed under the **GNU General Public License v3.0**.
