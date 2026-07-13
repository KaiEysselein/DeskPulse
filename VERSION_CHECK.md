# DeskPulse 0.1.3.2 — Verification Checklist

## Version references

```text
Program.cs AppInfo.Version                         0.1.3.2
DeskPulse.csproj Version                           0.1.3.2
DeskPulse.csproj AssemblyVersion                   0.1.3.2
DeskPulse.csproj FileVersion                       0.1.3.2
DeskPulse.csproj InformationalVersion              0.1.3.2
app.manifest assemblyIdentity version              0.1.3.2
README.md current version                          0.1.3.2
HANDOVER.md current version                        0.1.3.2
```

## Registry

```text
SettingsSchemaVersion                              5
Rules\FileActivity                                 JSON
Rules\AppActivity                                  JSON
Rules\UserActivity                                 JSON
Rules\FolderActivity                               removed after migration
```

## UI

Settings rule tabs:

```text
File Activity
App Activity
User Activity
```

View Log tabs:

```text
File Activity
App Activity
User Activity
```

File Activity must show **Browse...** and **Add Folder...**.

## Wildcard tests

```text
C:\Test\*                direct files only
C:\Test\**\*             direct and recursive files
C:\Test\**\*.xlsx        recursive XLSX only
*.pdf                      PDF filenames anywhere
Report?.docx               one-character filename wildcard
```

Confirm `*` never crosses a folder separator and `**` does.

## Migration tests

- Folder-only legacy rule converts to `<folder>\*`.
- Recursive legacy rule converts to `<folder>\**\*`.
- Enabled state and Include/Exclude action remain unchanged.
- Converted folder rules precede broad file rules.
- Duplicate patterns are removed.
- Old `Rules\FolderActivity` is deleted after Save.
- A schema-1 JSON rule package containing Folder Activity imports successfully.

## Build

```powershell
dotnet clean
dotnet restore
dotnet build
```

Expected:

```text
Build succeeded.
0 Error(s)
```

## Publish

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output ".\publish\v0.1.3.2"
```

- Maintenance database housekeeping includes cancellable scanning/deletion with transaction rollback before commit.

## View Log multi-select/delete

- [ ] Multiple rows can be selected with Ctrl/Shift.
- [ ] Delete is enabled when one or more rows are selected.
- [ ] Create Rule is enabled only when exactly one row is selected.
- [ ] Confirmed deletion removes only the selected records from the active activity table.

- View Log delete confirmation includes an optional, unchecked `Also create exclusion rule(s)` checkbox for File, App, and User Activity deletions.
