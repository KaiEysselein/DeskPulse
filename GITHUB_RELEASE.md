# DeskPulse 0.3.4.0

DeskPulse 0.3.4.0 moves machine-wide Windows exclusions into protected, data-driven policy files and improves Settings usability on compact and highly scaled displays.

## Highlights

- Versioned shipped path and process rules with protected administrator overrides.
- Validated automatic reload with last-known-good and built-in safety fallback behavior.
- Dynamic elevated Machine-wide Rules view with locked policy checkboxes, rule IDs, source, status and reasons.
- Rule revision tracking warns when an administrator override should be reviewed after a default changes.
- Aggregate rule-candidate diagnostics store process names and extensions without full paths or user identity.
- Scrollable forms and tab pages, verified at compact sizes and simulated 125%, 150% and 200% scaling.
- Automated fallback, override, visibility, revision and WinForms layout tests.

## Upgrade

The installer replaces shipped defaults, preserves administrator overrides and existing databases, and protects rule configuration so only LocalSystem and administrators can modify it.

## Scope boundary

DeskPulse does not expose a combined or all-users activity view. Candidate diagnostics contain no full paths, user names, SIDs or event contents.

## Installer

```text
DeskPulse_Setup_0.3.4.0.exe
```

## Verification

Release build, automated tests, packaging, installation and live verification are recorded in `VERSION_CHECK.md`.
