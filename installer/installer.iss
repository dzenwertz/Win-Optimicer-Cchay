; Script de Inno Setup para RETH Optimicer
[Setup]
AppName=RETH Optimicer
AppVersion=3.0.0
AppPublisher=RETH
AppPublisherURL=https://github.com/dzenwertz/RETH-Optimicer
DefaultDirName={autopf}\RETH Optimicer
DefaultGroupName=RETH Optimicer
OutputDir=..\installer_output
OutputBaseFilename=RETH_Optimicer_Setup_v3.0.0
SetupIconFile=..\cchay-optimicer-cs\Assets\reth.ico
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\RethOptimicer.exe

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RETH Optimicer"; Filename: "{app}\RethOptimicer.exe"
Name: "{autodesktop}\RETH Optimicer"; Filename: "{app}\RethOptimicer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RethOptimicer.exe"; Description: "{cm:LaunchProgram,RETH Optimicer}"; Flags: nowait postinstall skipifsilent
