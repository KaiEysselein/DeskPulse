# DeskPulse 0.3.0.0 Technical Development Handover

## Purpose and scope

This file is the detailed technical handover for the source under `dev`. It covers architecture, runtime behaviour, safeguard implementation, diagnostic commands, build and verification requirements. Repository-level release status and public-project continuity are maintained separately in `..\HANDOVER.md`.

## Authoritative milestone baseline

DeskPulse 0.3.0.0 is the accepted milestone promotion of the tested 0.2.2.3 service-safeguard baseline. No additional runtime feature was introduced during promotion; active application, assembly, installer, publish, verification and handover references are promoted to 0.3.0.0.

Historical version references remain unchanged in archived verification and release-history documents.

Repository: https://github.com/KaiEysselein/DeskPulse

## Architecture

DeskPulse consists of three .NET 8 Windows projects:

- `DeskPulse.Service`: privileged automatic Windows service; ETW monitoring, application and session monitoring, database writes, named-pipe server, diagnostic load generation and resource safeguards.
- `DeskPulse.Tray`: non-elevated WinForms tray application; Settings, View Log, Export, Maintenance, safeguard status/recovery and named-pipe client.
- `DeskPulse.Shared`: shared settings, models, rules, SQLite access and monitoring logic.

The service owns all SQLite write operations. The tray opens the activity database read-only for views, counts, statistics and exports.

## Service-safety milestone scope

Version 0.3.0.0 includes:

- Once-per-second monitoring of DeskPulse.Service CPU and working-set RAM use.
- Configurable CPU and RAM warning thresholds.
- Configurable CPU and RAM critical thresholds.
- Configurable sustained warning and critical durations.
- Warning event logging and warning tray state while logging continues.
- Critical event logging and immediate safety pause of activity logging.
- Optional persistence of the critical safety pause across service and Windows restarts.
- **Keep logging paused after restart following a critical trigger** enabled by default for safety.
- Explicit **Resume Logging** recovery, which clears the persistent critical marker and restarts monitoring.
- User-facing safeguard configuration under **Settings → Maintenance**.
- Validation requiring warning thresholds and durations to remain below their corresponding critical values.

Default safeguard values:

| Level | CPU | Service RAM | Sustained period |
|---|---:|---:|---:|
| Warning | 30% | 30% | 5 seconds |
| Critical | 45% | 45% | 10 seconds |

## Diagnostic safeguard test facility

The installed tray executable can start controlled load inside DeskPulse.Service so the safeguards can be verified:

```powershell
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-cpu 40 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-memory 25 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --test-service-load --cpu 40 --memory 25 --duration 60
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --load-status
& "C:\Program Files\DeskPulse\Tray\DeskPulse.Tray.exe" --stop-service-load-test
```

Aliases for the combined test are `--load` and `-l`; `--ram` may be used instead of `--memory`.

Safety limits:

- CPU target never exceeds 50%.
- RAM target never exceeds 50% of total physical memory.
- Limits are enforced by DeskPulse.Service regardless of tray input.
- Requests above 50% are rejected.
- Duration is limited to 1–300 seconds.
- Only one test may run at a time.
- Tests can be stopped from the live test window or command line.

The **DeskPulse Diagnostic Load Test** window states that the test verifies the service safeguards and displays elapsed-time progress, target values, measured service CPU, allocated test memory, service working set and total system RAM use.

## Build, publish and install

Run from the development folder:

```powershell
cd D:\Kai\GitHub\DeskPulse\dev
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Build.ps1
.\scripts\Publish.ps1
.\Installer\Build-Installer.ps1
Start-Process ".\publish\v0.3.0.0\installer\DeskPulse_Setup_0.3.0.0.exe"
```

Because 0.3.0.0 is a retained milestone, `Build-Installer.ps1` copies the installer to both:

```text
D:\Kai\GitHub\DeskPulse\releases\current
D:\Kai\GitHub\DeskPulse\releases\v0.3.0.0
```

The formal GitHub Release tag is:

```text
v0.3.0.0
```

## Acceptance verification

1. Build succeeds with zero errors.
2. Installer upgrades the accepted 0.2.2.3 installation.
3. Service and tray report version 0.3.0.0.
4. Exactly one tray instance appears in the active user session.
5. DeskPulse.Service starts automatically and remains responsive.
6. File, App and User Activity records are written normally.
7. A controlled warning-level test records one warning and keeps logging active.
8. A controlled critical-level test pauses logging and records the critical event.
9. With restart persistence enabled, the critical pause survives service or Windows restart.
10. Resume Logging clears the safety pause and restores activity monitoring.
11. Safeguard settings save, reload and validate correctly.
12. Diagnostic tests cannot exceed 50% CPU or 50% RAM and can be stopped manually.
13. Installer is retained under `releases\v0.3.0.0` and copied to `releases\current`.

## Known deferred item

The current tray-state icon artwork may show a non-transparent background on some Windows themes. This visual asset correction is deferred and is not represented as completed in 0.3.0.0. It does not affect safeguard operation, logging, recovery or data integrity.

## Future installer item

Consider optional machine-wide tray startup for all Windows users. Before implementation, resolve concurrent-session behaviour, per-session duplicate prevention, shared versus per-user settings, and database path/ownership. Prefer an **At logon of any user** scheduled task rather than changing only the HKCU Run registration.
