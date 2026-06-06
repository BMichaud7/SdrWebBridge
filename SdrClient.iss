[Setup]
AppName=SDR Client
AppVersion={#AppVersion}
AppPublisher=OpenRFStack
AppPublisherURL=https://bmichaud.com/Sdr/
DefaultDirName={autopf}\OpenRFStack\SdrClient
DefaultGroupName=OpenRFStack
OutputDir=installer
OutputBaseFilename=SdrClient-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=SDR Client (OpenRFStack)
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons"

[Files]
Source: "publish\SdrClient.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\SDR Client"; Filename: "{app}\SdrClient.exe"
Name: "{userdesktop}\SDR Client"; Filename: "{app}\SdrClient.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SdrClient.exe"; Description: "Launch SDR Client"; Flags: nowait postinstall skipifsilent
