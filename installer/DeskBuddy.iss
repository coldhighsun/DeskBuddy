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

#define AppName "DeskBuddy"
#define AppExe "DeskBuddy.App.exe"

[Setup]
; Never change AppId: it is how an upgrade finds the installation it replaces.
AppId={{15DEE168-F181-4422-95AC-BE381EC6B58C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}
; Per-user install: no administrator rights needed, which is what a locked-down work computer allows.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
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
english.KeepDataPrompt=Keep your settings, to-do list, note and logs?%n%nChoose No to delete them as well.
chinesesimplified.DesktopIcon=创建桌面快捷方式(&D)
chinesesimplified.RunAfterInstall=启动 {#AppName}
chinesesimplified.KeepDataPrompt=是否保留你的设置、待办、便签和日志？%n%n选择“否”会一并删除。

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

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:RunAfterInstall}"; Flags: nowait postinstall skipifsilent

[Code]
const
  ProcessName = '{#AppExe}';

{ Asks the running pet to close and ends it if it is still there a few seconds later. }
procedure StopApp;
var
  ResultCode: Integer;
begin
  { taskkill reports 128 when there is no such process; then there is nothing to wait for. }
  if Exec(ExpandConstant('{sys}	askkill.exe'), '/IM ' + ProcessName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and (ResultCode <> 128) then
  begin
    Sleep(3000);
    Exec(ExpandConstant('{sys}	askkill.exe'), '/F /IM ' + ProcessName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
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
