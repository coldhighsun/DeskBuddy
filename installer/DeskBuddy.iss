; Inno Setup 7 script for DeskBuddy. Built by tools/Build-Installer.ps1, which passes:
;   /DAppVersion=<version>   the version MinVer computes from git tags
;   /DPublishDir=<dir>       the self-contained publish output
;   /DOutputDir=<dir>        where the installer is written

#ifndef AppVersion
  #error AppVersion is not defined; build with tools/Build-Installer.ps1.
#endif
#ifndef PublishDir
  #error PublishDir is not defined; build with tools/Build-Installer.ps1.
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

; VersionInfoVersion must be numeric, so drop a pre-release suffix such as -rc.1 or -alpha.0.5.
#define HyphenPos Pos("-", AppVersion)
#define NumericVersion HyphenPos > 0 ? Copy(AppVersion, 1, HyphenPos - 1) : AppVersion

#define AppName "DeskBuddy"
#define AppExe "DeskBuddy.exe"

[Setup]
; Never change AppId: it is how an upgrade finds the installation it replaces.
AppId={{15DEE168-F181-4422-95AC-BE381EC6B58C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
AppPublisherURL=https://github.com/coldhighsun/DeskBuddy
AppSupportURL=https://github.com/coldhighsun/DeskBuddy/issues
AppUpdatesURL=https://github.com/coldhighsun/DeskBuddy/releases
VersionInfoVersion={#NumericVersion}
; Per-user install: no administrator rights needed, which is what a locked-down work computer allows.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
SetupIconFile=..\src\DeskBuddy.App\Assets\DeskBuddy.ico
UninstallDisplayIcon={app}\{#AppExe}
OutputDir={#OutputDir}
OutputBaseFilename=DeskBuddy-Setup-{#AppVersion}
Compression=lzma2/ultra
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
; The running pet is closed by the [Code] section below, not by the Restart Manager.
CloseApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[CustomMessages]
english.DesktopIcon=Create a &desktop shortcut
english.RunAfterInstall=Start {#AppName}
english.KeepDataPrompt=Keep your settings and logs?%n%nChoose No to delete them as well.
chinesesimplified.DesktopIcon=创建桌面快捷方式(&D)
chinesesimplified.RunAfterInstall=启动 {#AppName}
chinesesimplified.KeepDataPrompt=是否保留你的设置和日志？%n%n选择“否”会一并删除。

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; The app can register itself to start at sign-in. Remove that again on uninstall, together with the Task Manager
; "startup apps" switch, so nothing keeps pointing at a program that is gone.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#AppName}"; ValueType: none; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueName: "{#AppName}"; ValueType: none; Flags: dontcreatekey uninsdeletevalue

[UninstallDelete]
; Installers the app downloaded to update itself (see AppUpdates); nothing else is kept there.
Type: filesandordirs; Name: "{%TEMP}\{#AppName}\update"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:RunAfterInstall}"; Flags: nowait postinstall; Check: ShouldLaunchApp

[Code]
const
  ProcessName = '{#AppExe}';
  { Held by the running app; the name matches SingleInstanceMutexName in App.xaml.cs. }
  InstanceMutex = 'Local\DeskBuddy.SingleInstance';

{ Waits up to the given number of milliseconds for the running app to go away. }
function WaitForAppToExit(TimeoutMs: Integer): Boolean;
var
  Waited: Integer;
begin
  Waited := 0;
  while CheckForMutexes(InstanceMutex) and (Waited < TimeoutMs) do
  begin
    Sleep(250);
    Waited := Waited + 250;
  end;
  Result := not CheckForMutexes(InstanceMutex);
end;

{ Asks the running pet to close and ends it if it is still there a few seconds later. }
procedure StopApp;
var
  ResultCode: Integer;
  AppPath: String;
begin
  if not CheckForMutexes(InstanceMutex) then
    Exit;

  { Started with --quit, the installed app asks the running one to close cleanly and returns once it has. A version
    from before --quit existed returns at once and is left to taskkill below. }
  AppPath := ExpandConstant('{app}\{#AppExe}');
  if FileExists(AppPath) then
    Exec(AppPath, '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if not WaitForAppToExit(0) then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM ' + ProcessName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if not WaitForAppToExit(3000) then
      Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM ' + ProcessName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

{ A silent install starts the app only when the updater asks for it (/RELAUNCH=1); a person or script running it silently does not get the app started. }
function ShouldLaunchApp: Boolean;
begin
  Result := not WizardSilent or (ExpandConstant('{param:RELAUNCH|0}') = '1');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopApp;
  Result := '';
end;

function InitializeUninstall: Boolean;
begin
  StopApp;
  Result := True;
end;

{ After the program is removed, offers to delete the user's data; a silent uninstall always keeps it. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
  begin
    DataDir := ExpandConstant('{localappdata}\{#AppName}');
    if DirExists(DataDir) and (MsgBox(CustomMessage('KeepDataPrompt'), mbConfirmation, MB_YESNO) = IDNO) then
    begin
      DelTree(DataDir, True, True, True);
    end;
  end;
end;
