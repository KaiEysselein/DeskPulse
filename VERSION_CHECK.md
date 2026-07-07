# DeskPulse 0.1.3.0 — All Visible Forms Designer Check

Checked package: `DeskPulse_0.1.3.0_final_source`

## Versioning

Current version references are intended to be `0.1.3.0`.

## Designer status

Designer-backed forms / surfaces:

- `Forms/AboutForm.cs`
- `Forms/ExportDateRangeForm.cs`
- `Forms/MaintenanceProgressForm.cs`
- `Forms/SettingsForm.cs`

`SettingsForm.cs` now contains designer-backed visible layouts for:

- General tab
- Files tab
- Export Options tab
- Maintenance tab
- Maintenance > Database
- Maintenance > Statistics
- Maintenance > Cleanup
- Maintenance > Logging Rules
- Maintenance > Diagnostics

`MaintenanceForm.cs` remains a thin launcher/wrapper for `SettingsForm(true)`. Edit the maintenance UI in Visual Studio through:

```text
Forms > SettingsForm.cs > View Designer > Maintenance tab
```

## Static verification performed here

- Zip structure prepared from the fixed `0.1.3.0` all-forms designer package.
- Braces balanced in all `.cs` files.
- Designer event-handler references in `SettingsForm.Designer.cs` checked against `SettingsForm.cs` handler definitions.
- Old code-built Maintenance tab builder methods removed from `SettingsForm.cs` so the designer-backed Maintenance layout is now the active layout path.
- `SettingsForm` runtime constructor now loads the designer-backed Maintenance controls instead of rebuilding the Maintenance UI from helper methods.

## Required local verification

This environment cannot run `dotnet build`. Please verify locally:

```powershell
dotnet clean
dotnet restore
dotnet build
dotnet run -- -m
```

Then verify visually in Visual Studio:

```text
Forms > SettingsForm.cs > View Designer
```

You should see contents on the General, Files, Export Options, and Maintenance tabs. This is the locked `0.1.3.0` source baseline; new functional changes should move to `0.1.4.0`.
