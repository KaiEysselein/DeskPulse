# DeskPulse

DeskPulse is a Windows tray application built with C# / .NET 8 WinForms.

It monitors selected file activity through Windows ETW file I/O tracing and logs the results to a CSV file that can be opened in Excel.

## Version

Current version: `0.0.1`

## Current Features

- Windows system tray application
- File open/write/close activity monitoring
- Settings window with registered Windows file types
- Two-list file type selector for monitored extensions
- CSV logging
- Excel-safe log viewing through a copied `-view.csv` file
- Temporary folder file activity exclusion
- Default log folder: `%USERPROFILE%\Documents\DeskPulse\`
- Default log file: `DeskPulse-log.csv`

## Requirements

- Windows 11
- .NET 8 SDK
- Administrator rights when running

DeskPulse currently requires Administrator privileges because Windows kernel ETW file I/O tracing requires elevation.

## Build

```powershell
dotnet clean
dotnet build