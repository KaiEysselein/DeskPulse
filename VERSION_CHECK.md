# DeskPulse 0.1.3.1 — Release Verification

## Active version references

Expected values:

```text
Program.cs AppInfo.Version                         0.1.3.1
DeskPulse.csproj Version                           0.1.3.1
DeskPulse.csproj AssemblyVersion                   0.1.3.1
DeskPulse.csproj FileVersion                       0.1.3.1
DeskPulse.csproj InformationalVersion              0.1.3.1
app.manifest assemblyIdentity version              0.1.3.1
README.md current version                          0.1.3.1
HANDOVER.md current version                        0.1.3.1
```

Historical versions in `CHANGELOG.md` and explicit migration comments are valid.

## Current forms

```text
AboutForm
ExportDateRangeForm
InstalledAppSelectionForm
SettingsForm
ViewLogForm
LogEntryDetailsForm
AddLogRuleForm
RuleCleanupProgressForm
```

There is no `MaintenanceForm` and no `MaintenanceProgressForm` in the package.

## Current UI

Tray left-click menu:

```text
View Log...
Settings...
About
Exit
```

No right-click menu.

Settings tabs:

```text
General
Rules
Export Options
Maintenance
```

Rules tabs:

```text
Folder Activity
App Activity
File Activity
User Activity
```

## Command-line verification

Supported:

```text
-uninstall
--uninstall
/uninstall
```

Not supported:

```text
-m
-maintenance
-debug
-debug-skipped
```

## Registry

```text
HKCU\Software\DeskPulse
SettingsSchemaVersion = 4
```

Rules are stored as JSON under the `Rules` subkey.

## Build verification required locally

```powershell
dotnet clean
dotnet restore
dotnet build
```

Expected:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Publish verification:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output ".\publish\v0.1.3.1"
```

## Source hygiene

Do not commit generated output, databases, exports, logs, executables, or ZIP packages. Delete obsolete Maintenance form files if they still exist in an older checkout.
