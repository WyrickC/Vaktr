; Vaktr Inno Setup Script
; Compiles into a single VaktrSetup.exe installer per architecture.
;
; Parameters (passed via /D on the command line):
;   MyAppVersion  - Version string (e.g. "1.0.1")
;   MyAppArch     - Architecture: "x64compatible" (default) or "arm64"
;   MyPublishDir  - Publish output directory (default: "..\publish\x64")
;   MyOutputSuffix - Suffix for the output filename (default: "-x64")
;
; Prerequisites:
;   1. Install Inno Setup: https://jrsoftware.org/isdl.php
;   2. Build the app:
;        dotnet publish Vaktr.App/Vaktr.App.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained -o publish/x64
;   3. Open this file in Inno Setup Compiler and click Build > Compile
;   4. The installer will be created in installer/Output/

#define MyAppName "Vaktr"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.1"
#endif
#define MyAppPublisher "Vaktr"
#define MyAppURL "https://github.com/WyrickC/Vaktr"
#define MyAppExeName "Vaktr.exe"
#define MyAppDescription "System Telemetry Dashboard"

; Architecture defaults
#ifndef MyAppArch
  #define MyAppArch "x64compatible"
#endif
#ifndef MyPublishDir
  #define MyPublishDir "..\publish\x64"
#endif
#ifndef MyOutputSuffix
  #define MyOutputSuffix "-x64"
#endif

[Setup]
AppId={{B8F2A1C4-5D6E-4F7A-8B9C-0D1E2F3A4B5C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=VaktrSetup-{#MyAppVersion}{#MyOutputSuffix}
SetupIconFile=..\Vaktr.App\Assets\Vaktr.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed={#MyAppArch}
ArchitecturesInstallIn64BitMode={#MyAppArch}
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "launchonstartup"; Description: "Launch Vaktr when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Include everything from the publish folder
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDescription}"

[Registry]
; Launch on startup (optional, user chooses during install)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: launchonstartup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up app data on uninstall (optional — comment out to preserve user data)
; Type: filesandordirs; Name: "{localappdata}\Vaktr"

[Code]
// Close Vaktr if running before install/upgrade
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if CheckForMutexes('{#MyAppName}Mutex') then
  begin
    Exec('taskkill', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(500);
  end;
end;
