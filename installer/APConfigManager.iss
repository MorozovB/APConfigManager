; AP Configuration Manager Installer
; Inno Setup Script

#define MyAppName "AP Configuration Manager"
#define MyAppVersion "1.0.3-beta"
#define MyAppPublisher "APConfig"
#define MyAppExeName "APConfigManager.Desktop.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\AP Configuration Manager
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\output
OutputBaseFilename=APConfigManager-v{#MyAppVersion}-x64-setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupIconFile=compiler:SetupClassicIcon.ico
UninstallDisplayIcon={app}\desktop\{#MyAppExeName}
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional:";

[Files]
; Desktop app
Source: "..\publish\desktop\*"; DestDir: "{app}\desktop"; Flags: ignoreversion recursesubdirs createallsubdirs

; API + React UI
Source: "..\publish\api\*"; DestDir: "{app}\api"; Flags: ignoreversion recursesubdirs createallsubdirs

; Windows App SDK Runtime
Source: "WindowsAppRuntimeInstall-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "windowsdesktop-runtime-8.0.28-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "aspnetcore-runtime-8.0.28-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\desktop\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\desktop\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Install Windows App SDK Runtime (silent)
Filename: "{tmp}\WindowsAppRuntimeInstall-x64.exe"; Parameters: "--quiet --force"; StatusMsg: "Installing Windows App SDK Runtime..."; Flags: waituntilterminated runhidden
Filename: "{tmp}\windowsdesktop-runtime-8.0.28-win-x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing .NET Runtime..."; Flags: waituntilterminated runhidden
Filename: "{tmp}\aspnetcore-runtime-8.0.28-win-x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing .NET Runtime..."; Flags: waituntilterminated runhidden

; Launch app after install
Filename: "{app}\desktop\{#MyAppExeName}"; Description: "Launch AP Configuration Manager"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up app folder (but NOT %LocalAppData%\APConfigManager)
Type: filesandordirs; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;