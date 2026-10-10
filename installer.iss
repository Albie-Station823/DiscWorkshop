[Setup]
AppId={{C23B37C3-557F-413E-914B-97E44772B972}
AppName=Disc Workshop
AppVersion=1.8.3
AppPublisher=Albert Mark Burdge
DefaultDirName={localappdata}\Programs\Disc Workshop
DefaultGroupName=Disc Workshop
PrivilegesRequired=lowest
MinVersion=10.0.22000
OutputDir=release
OutputBaseFilename=DiscWorkshop-Setup-1.8.3
SetupIconFile=assets\DiscWorkshop.ico
UninstallDisplayIcon={app}\DiscWorkshop.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
InfoBeforeFile=docs\SETUP.txt
CloseApplications=yes

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "DiscWorkshop-1.8.3.exe"; DestDir: "{app}"; DestName: "DiscWorkshop.exe"; Flags: ignoreversion
Source: "docs\USER-GUIDE.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "docs\SETUP.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Disc Workshop"; Filename: "{app}\DiscWorkshop.exe"; WorkingDir: "{app}"
Name: "{group}\User Guide"; Filename: "{app}\USER-GUIDE.html"
Name: "{autodesktop}\Disc Workshop"; Filename: "{app}\DiscWorkshop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\USER-GUIDE.html"; Description: "Read setup and usage instructions"; Flags: postinstall shellexec skipifsilent
Filename: "{app}\DiscWorkshop.exe"; Description: "Open Disc Workshop"; Flags: postinstall nowait skipifsilent unchecked



