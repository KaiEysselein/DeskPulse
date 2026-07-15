# DeskPulse 0.2.1.5 Build Verification

## Scope

- Variable records-per-page control on all View Log tabs.
- File Activity grouping by Date, File name, Extension, Folder, and Application.
- Double-click group expansion/collapse and retained record Details behavior.
- Active version references advanced to 0.2.1.5.

## Source-level verification

- Paging calculations and SQL LIMIT/OFFSET use the selected page size.
- The page-size value is validated to 1–10,000 and persisted under the DeskPulse user registry key.
- Group headers are produced by SQLite GROUP BY queries rather than loading the entire database into memory.
- Expanded groups query only records matching the selected group key and current date range.
- Create Rule and Delete remain restricted to real log-entry rows.

## Local verification required

The .NET SDK was unavailable in the packaging environment. Run `scripts\Build.ps1`, `scripts\Publish.ps1`, and `Installer\Build-Installer.ps1` on the Windows development machine, then verify each grouping mode against a representative database before release.
