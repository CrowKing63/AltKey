; AltKey Inno Setup 설치 스크립트 (T-9.5)
;
; [로컬 빌드] 먼저 퍼블리시 후 iscc 실행:
;   dotnet publish AltKey/AltKey.csproj -c Release -r win-x64 --self-contained true ^
;     -p:PublishSingleFile=true -o dist/publish
;   iscc installer\AltKey.iss
;
; [CI] release.yml에서 자동 실행 (BuildDir은 CI 퍼블리시 경로와 동일)

#define AppName    "AltKey"
#define AppVersion "0.1.3"
#define AppPublisher "CrowKing63"
#define AppURL     "https://github.com/CrowKing63/altkey"
#define AppExeName "AltKey.exe"
; CI/로컬 모두 dist/publish 로 퍼블리시한다고 가정
#define BuildDir   "..\dist\publish"

[Setup]
AppId={{E3A7F1D2-4B8C-4E9A-A2F5-1C3D6B0E7A94}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
; 출력 파일
OutputDir=..\dist
OutputBaseFilename=AltKey-Setup-v{#AppVersion}
; 압축
Compression=lzma2/ultra64
SolidCompression=yes
; UI
WizardStyle=modern
; 아이콘
UninstallDisplayIcon={app}\{#AppExeName}
SetupIconFile=..\AltKey\Assets\icon.ico
; 최소 OS: Windows 10
MinVersion=10.0
; 64비트 전용
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
; 관리자 권한 필요
PrivilegesRequired=admin

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";    Description: "{cm:CreateDesktopIcon}";    GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupentry";   Description: "Windows 시작 시 자동 실행";   GroupDescription: "시작 옵션:";

[Files]
; 실행 파일
Source: "{#BuildDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; 레이아웃: PathResolver 설치 모드 경로(%AppData%\AltKey\layouts\)에 설치
; onlyifdoesntexist 로 사용자가 커스텀한 레이아웃은 덮어쓰지 않음
Source: "..\AltKey\layouts\*"; DestDir: "{userappdata}\AltKey\layouts"; Flags: ignoreversion recursesubdirs createallsubdirs onlyifdoesntexist
; 에셋 (아이콘, 이모지, 사운드) — exe 옆에 위치
Source: "..\AltKey\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName} 제거";         Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";      Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; 시작 시 자동 실행 (선택 태스크)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; \
    Flags: uninsdeletevalue; Tasks: startupentry

[Run]
Filename: "{app}\{#AppExeName}"; \
    Description: "{#AppName} 실행"; \
    Flags: nowait postinstall skipifsilent
Filename: "{app}\{#AppExeName}"; \
    Flags: nowait; \
    Check: IsAutoRestart

[UninstallRun]
; 제거 시 실행 중인 AltKey 종료
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExeName}"; Flags: runhidden; RunOnceId: "KillAltKey"

[Code]
function CmdLineParamExists(const Param: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Param) = 0 then
    begin
      Result := True;
      Break;
    end;
end;

function IsAutoRestart: Boolean;
begin
  Result := CmdLineParamExists('/AUTORESTART');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    Exec('taskkill.exe', '/f /im {#AppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
