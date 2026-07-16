#define MyAppName "DeskPulse"
#define MyAppVersion "0.2.2.2"
#define MyAppPublisher "Kai Eysselein"
#define ServiceName "DeskPulse.Service"
#define ServiceExeName "DeskPulse.Service.exe"
#define TrayExeName "DeskPulse.Tray.exe"

[Setup]
AppId={{A73C22CF-67EC-4EAF-B65D-219B90536982}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}

DefaultDirName={autopf}\DeskPulse
DefaultGroupName=DeskPulse
DisableProgramGroupPage=yes

OutputDir=..\publish\v0.2.2.2\installer
OutputBaseFilename=DeskPulse_Setup_{#MyAppVersion}

Compression=lzma2
SolidCompression=yes
WizardStyle=modern

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin

CloseApplications=yes
RestartApplications=no

UninstallDisplayName=DeskPulse
UninstallDisplayIcon={app}\Tray\{#TrayExeName}

VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Dirs]
Name: "{commonappdata}\DeskPulse"; Permissions: users-modify

[Files]
Source: "..\publish\v0.2.2.2\service\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\v0.2.2.2\tray\*"; DestDir: "{app}\Tray"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Remove startup shortcuts from older builds. Tray autostart is now stored in the current user HKCU Run key.
Type: files; Name: "{userstartup}\DeskPulse.lnk"
Type: files; Name: "{userstartup}\DeskPulse Tray.lnk"
Type: files; Name: "{commonstartup}\DeskPulse.lnk"
Type: files; Name: "{commonstartup}\DeskPulse Tray.lnk"

[Icons]
Name: "{group}\DeskPulse"; Filename: "{app}\Tray\{#TrayExeName}"
Name: "{group}\Uninstall DeskPulse"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DeskPulse"; Filename: "{app}\Tray\{#TrayExeName}"; Tasks: desktopicon

[Run]
; Normalize/migrate the shared settings as the original interactive user before
; the LocalSystem service starts. This guarantees an absolute Documents path.
Filename: "{app}\Tray\{#TrayExeName}"; Parameters: "--initialize-settings"; Flags: runhidden waituntilterminated runasoriginaluser
Filename: "{app}\Tray\{#TrayExeName}"; Parameters: "--enable-startup"; Flags: runhidden waituntilterminated runasoriginaluser
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\Service\{#ServiceExeName}"" start= auto DisplayName= ""DeskPulse Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""DeskPulse background monitoring service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failure {#ServiceName} reset= 86400 actions= restart/5000/restart/15000/restart/60000"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated
Filename: "{app}\Tray\{#TrayExeName}"; Description: "Start DeskPulse Tray"; Flags: nowait postinstall skipifsilent runasoriginaluser

; These run before Inno Setup removes installed files.
[UninstallRun]
; Remove the original interactive user's tray autostart registry entry before stopping the tray.
Filename: "{app}\Tray\{#TrayExeName}"; Parameters: "--disable-startup"; Flags: runhidden waituntilterminated; RunOnceId: "DisableDeskPulseTrayStartup"

; Stop the tray first so its executable, database and settings are not locked.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#TrayExeName} /T"; Flags: runhidden waituntilterminated; RunOnceId: "KillDeskPulseTray"

; Stop and unregister the Windows service.
Filename: "{sys}\sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "StopDeskPulseService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteDeskPulseService"

; Remove possible startup registry values created by current or older DeskPulse versions.
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""DeskPulse"" /f"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteHKCURunDeskPulse"
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""DeskPulse.Tray"" /f"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteHKCURunDeskPulseTray"
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKLM\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""DeskPulse"" /f"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteHKLMRunDeskPulse"
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKLM\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""DeskPulse.Tray"" /f"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteHKLMRunDeskPulseTray"
Filename: "{sys}\reg.exe"; Parameters: "delete ""HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"" /v ""DeskPulse"" /f"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteWOWRunDeskPulse"

; Remove likely scheduled-task names from current and older builds.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""DeskPulse"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteTaskDeskPulse"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""DeskPulse Tray"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteTaskDeskPulseTray"
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""DeskPulse.Tray"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteTaskDeskPulseTrayDot"

[UninstallDelete]
; Remove startup shortcuts that may have been created by DeskPulse itself.
Type: files; Name: "{userstartup}\DeskPulse.lnk"
Type: files; Name: "{commonstartup}\DeskPulse.lnk"
Type: files; Name: "{userstartup}\DeskPulse Tray.lnk"
Type: files; Name: "{commonstartup}\DeskPulse Tray.lnk"

; Remove application settings and service data, but preserve the database and user files in Documents\DeskPulse.
Type: filesandordirs; Name: "{commonappdata}\DeskPulse"
Type: filesandordirs; Name: "{localappdata}\DeskPulse"
Type: filesandordirs; Name: "{userappdata}\DeskPulse"

; Remove any leftover installed files after service/tray shutdown.
Type: filesandordirs; Name: "{app}"

[Code]
function ServiceExists(const Service: string): Boolean;
var
  ResultCode: Integer;
begin
  Result :=
    Exec(ExpandConstant('{sys}\sc.exe'),
      'query "' + Service + '"',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode)
    and (ResultCode = 0);
end;

procedure StopAndRemoveExistingService;
var
  ResultCode: Integer;
begin
  if ServiceExists('{#ServiceName}') then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'),
      '/F /IM {#TrayExeName} /T',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);

    Exec(ExpandConstant('{sys}\sc.exe'),
      'stop "{#ServiceName}"',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);

    Sleep(2000);

    Exec(ExpandConstant('{sys}\sc.exe'),
      'delete "{#ServiceName}"',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);

    Sleep(1500);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopAndRemoveExistingService;
  Result := '';
end;
