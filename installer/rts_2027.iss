; Inno Setup 6 script for the RTS_2027 Windows installer (M10-22).
; Build: iscc /DAppVersion=0.1.5 /DSourceDir=..\publish\Game installer\rts_2027.iss
; Input: the self-contained win-x64 publish folder (dotnet publish src/Game -c Release -r win-x64 --self-contained -o publish/Game).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish\Game"
#endif

#define AppName "RTS_2027"
#define AppExe "Game.exe"

[Setup]
AppId={{6F0C2B7A-4D1E-4A56-9C3B-5B7E2A8D9F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=RTS_2027 contributors
AppPublisherURL=https://github.com/pwnzor11/RTS_2027
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\publish\installer
OutputBaseFilename=RTS_2027-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "firewall"; Description: "Allow LAN games through Windows Firewall (private networks)"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; Inbound UDP 47270-47271 for the game exe, private profile only.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""RTS_2027 LAN (UDP)"" dir=in action=allow protocol=UDP localport=47270-47271 program=""{app}\{#AppExe}"" profile=private"; Flags: runhidden; Tasks: firewall; StatusMsg: "Adding firewall rule..."
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Runs always: deleting a rule that is not there only prints "No rules match", which is harmless.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""RTS_2027 LAN (UDP)"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"
