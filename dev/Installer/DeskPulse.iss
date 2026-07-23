#define MyAppName "DeskPulse"
#define MyAppVersion "0.3.2.0"
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

OutputDir=..\publish\v0.3.2.0\installer
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
Name: "{commonappdata}\DeskPulse"; Permissions: users-modify; Flags: uninsneveruninstall

[Files]
Source: "..\publish\v0.3.2.0\service\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\v0.3.2.0\tray\*"; DestDir: "{app}\Tray"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Register-AllUsersTrayStartup.ps1"; DestDir: "{tmp}"; Flags: deleteafterinstall

[InstallDelete]
; Remove startup shortcuts from older builds. Tray autostart now uses a
; machine-wide scheduled task triggered at logon of any user.
Type: files; Name: "{userstartup}\DeskPulse.lnk"
Type: files; Name: "{userstartup}\DeskPulse Tray.lnk"
Type: files; Name: "{commonstartup}\DeskPulse.lnk"
Type: files; Name: "{commonstartup}\DeskPulse Tray.lnk"

[Icons]
Name: "{group}\DeskPulse"; Filename: "{app}\Tray\{#TrayExeName}"
Name: "{group}\Uninstall DeskPulse"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DeskPulse"; Filename: "{app}\Tray\{#TrayExeName}"; Tasks: desktopicon

[Run]
; Normalize the shared settings as the original interactive user before the
; LocalSystem service starts. The service then migrates the live database into
; its protected ProgramData SID folder.
Filename: "{app}\Tray\{#TrayExeName}"; Parameters: "--initialize-settings"; Flags: runhidden waituntilterminated runasoriginaluser
Filename: "{app}\Tray\{#TrayExeName}"; Parameters: "--disable-startup"; Flags: runhidden waituntilterminated runasoriginaluser
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{tmp}\Register-AllUsersTrayStartup.ps1"" -TrayPath ""{app}\Tray\{#TrayExeName}"" -ErrorLogPath ""{commonappdata}\DeskPulse\scheduled-task-registration-error.log"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\Service\{#ServiceExeName}"" start= auto DisplayName= ""DeskPulse Service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description {#ServiceName} ""DeskPulse background monitoring service"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failure {#ServiceName} reset= 86400 actions= restart/5000/restart/15000/restart/60000"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Flags: runhidden waituntilterminated
Filename: "{app}\Tray\{#TrayExeName}"; Parameters: "{code:GetInstallLifecycleParameters}"; Flags: runhidden waituntilterminated runasoriginaluser
Filename: "{app}\Tray\{#TrayExeName}"; Description: "Start DeskPulse Tray"; Flags: nowait postinstall skipifsilent runasoriginaluser

; These run before Inno Setup removes installed files.
[UninstallRun]
; Remove any original-user startup registry entry before stopping the tray.
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

; Remove settings and transient service state. Preserve the ProgramData System
; and Users database folders so uninstall cannot destroy recorded activity.
Type: files; Name: "{commonappdata}\DeskPulse\settings.json"
Type: files; Name: "{commonappdata}\DeskPulse\settings.json.tmp"
Type: files; Name: "{commonappdata}\DeskPulse\critical-safety-pause.flag"
Type: files; Name: "{commonappdata}\DeskPulse\scheduled-task-registration-error.log"
Type: filesandordirs; Name: "{localappdata}\DeskPulse"
Type: filesandordirs; Name: "{userappdata}\DeskPulse"

; Remove any leftover installed files after service/tray shutdown.
Type: filesandordirs; Name: "{app}"

[Code]
var
  PreviousDeskPulseVersion: String;
  DeskPulseInstallAction: String;

function DetectPreviousDeskPulseVersion: String;
var
  ExistingExe: String;
begin
  ExistingExe := ExpandConstant('{app}\Service\{#ServiceExeName}');
  if not FileExists(ExistingExe) then
    ExistingExe := ExpandConstant('{app}\Tray\{#TrayExeName}');

  if FileExists(ExistingExe) then
  begin
    if not GetVersionNumbersString(ExistingExe, Result) then
      Result := '';
  end
  else
    Result := '';
end;

function GetInstallLifecycleParameters(Param: String): String;
begin
  Result := '--record-install-lifecycle "' + DeskPulseInstallAction + '" "' +
    PreviousDeskPulseVersion + '" "{#MyAppVersion}"';
end;

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
  PreviousDeskPulseVersion := DetectPreviousDeskPulseVersion;

  if PreviousDeskPulseVersion = '' then
    DeskPulseInstallAction := 'Installed'
  else if CompareText(PreviousDeskPulseVersion, '{#MyAppVersion}') = 0 then
    DeskPulseInstallAction := 'Reinstalled'
  else
    DeskPulseInstallAction := 'Updated';

  StopAndRemoveExistingService;
  Result := '';
end;
