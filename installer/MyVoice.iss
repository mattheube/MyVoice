#define MyAppVersion "2.2.0"
[Setup]
AppId={{4197D906-B03B-48CD-997A-5A6389477F25}
AppName=MyVoice
AppVersion={#MyAppVersion}
AppPublisher=MyVoice Personal
DefaultDirName={localappdata}\Programs\MyVoice
DefaultGroupName=MyVoice
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\releases
OutputBaseFilename=MyVoiceSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\MyVoice.ico
UninstallDisplayIcon={app}\MyVoice.exe
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"
Name: "startup"; Description: "Lancer MyVoice avec Windows"; Flags: unchecked
[Files]
Source: "..\portable\*"; DestDir: "{app}"; Excludes: "*.pdb,*.pyc,__pycache__\*"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\MyVoice"; Filename: "{app}\MyVoice.exe"
Name: "{autodesktop}\MyVoice"; Filename: "{app}\MyVoice.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MyVoice"; ValueData: """{app}\MyVoice.exe"""; Flags: uninsdeletevalue; Tasks: startup
[Run]
Filename: "{app}\MyVoice.exe"; Description: "Lancer MyVoice"; Flags: nowait postinstall skipifsilent
