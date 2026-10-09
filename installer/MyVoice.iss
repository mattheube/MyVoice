#define MyAppVersion "2.3.0"
[Setup]
AppId={{4197D906-B03B-48CD-997A-5A6389477F25}
AppName=MyVoice
AppVersion={#MyAppVersion}
AppPublisher=MyVoice
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
Name: "startup"; Description: "Lancer MyVoice avec Windows"
[Files]
Source: "..\portable\*"; DestDir: "{app}"; Excludes: "*.pdb,*.pyc,__pycache__\*"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\MyVoice"; Filename: "{app}\MyVoice.exe"
Name: "{autodesktop}\MyVoice"; Filename: "{app}\MyVoice.exe"; Tasks: desktopicon
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MyVoice"; ValueData: """{app}\MyVoice.exe"""; Flags: uninsdeletevalue; Tasks: startup
[Run]
Filename: "https://vb-audio.com/Cable/"; Description: "Configurer le microphone virtuel (site officiel VB-Audio)"; Flags: shellexec postinstall skipifsilent unchecked; Check: NeedsCable
Filename: "{app}\MyVoice.exe"; Description: "Lancer MyVoice"; Flags: nowait postinstall skipifsilent

[Code]
function HasCableEndpoint(Direction, ExpectedName: String): Boolean;
var
  Keys, Values: TArrayOfString;
  RootPath, PropertyPath, Value: String;
  I, J: Integer;
  State: Cardinal;
begin
  Result := False;
  RootPath := 'SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\' + Direction;
  if not RegGetSubkeyNames(HKLM64, RootPath, Keys) then Exit;
  for I := 0 to GetArrayLength(Keys) - 1 do begin
    if RegQueryDWordValue(HKLM64, RootPath + '\' + Keys[I], 'DeviceState', State) and (State = 1) then begin
      PropertyPath := RootPath + '\' + Keys[I] + '\Properties';
      if RegGetValueNames(HKLM64, PropertyPath, Values) then
        for J := 0 to GetArrayLength(Values) - 1 do
          if RegQueryStringValue(HKLM64, PropertyPath, Values[J], Value) then
            if Pos(Uppercase(ExpectedName), Uppercase(Value)) > 0 then begin Result := True; Exit; end;
    end;
  end;
end;

function NeedsCable: Boolean;
begin
  Result := not (HasCableEndpoint('Render', 'CABLE Input') and HasCableEndpoint('Capture', 'CABLE Output'));
end;
