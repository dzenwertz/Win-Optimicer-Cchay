; Script de Inno Setup para Cchay Optimicer
[Setup]
AppName=Cchay Optimicer
AppVersion=2.0.1
AppPublisher=Joseph ICV & dzenwertz
DefaultDirName={autopf}\Cchay Optimicer
DefaultGroupName=Cchay Optimicer
OutputDir=..\installer_output
OutputBaseFilename=Cchay_Optimicer_Setup_v2.0.1
SetupIconFile=..\cchay-optimicer-cs\Assets\cchay_logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\cchay-optimicer-cs.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Cchay Optimicer"; Filename: "{app}\cchay-optimicer-cs.exe"
Name: "{autodesktop}\Cchay Optimicer"; Filename: "{app}\cchay-optimicer-cs.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\cchay-optimicer-cs.exe"; Description: "{cm:LaunchProgram,Cchay Optimicer}"; Flags: nowait postinstall skipifsilent
