# 기술 설계 문서: 독립 설정 앱 분리 (standalone-settings-app)

## 개요

AltKey의 설정 화면(`SettingsWindow`)을 메인 키보드 프로세스(`AltKey.App.exe`)에서 분리하여 독립 실행 파일(`AltKey.Settings.exe`)로 구현합니다.

### 핵심 문제

현재 구조에서는 가상 키보드와 설정 창이 동일 프로세스 내에 존재합니다. 사용자가 자체 가상 키보드로 설정 항목(예: 단축키 입력란)을 입력할 때, 포커스가 같은 프로세스 내에서 전이되므로 Windows IME가 한글 조합 상태를 유지하지 못하고 자모가 분리되는 UX 결함이 발생합니다.

설정 앱을 별도 프로세스로 분리하면 가상 키보드가 설정 앱을 완전히 '외부 앱'으로 인식하게 되어, 기존 Unicode/VirtualKey 입력 방식 그대로 한글 조합이 정상 동작합니다.

### 목표 아키텍처

```
AltKey.Core.dll          ← 공용 라이브러리 (모델 + 핵심 서비스)
AltKey.App.exe           ← 가상 키보드 메인 앱 (기존 프로젝트 리네임)
AltKey.Settings.exe      ← 설정 UI 전용 독립 앱 (신규)
```

---

## 아키텍처

### 프로젝트 구조

```
AltKey/                          (솔루션 루트)
├── AltKey.Core/                 ← 신규: 공용 라이브러리
│   ├── AltKey.Core.csproj       (TargetFramework: net8.0-windows, OutputType: Library)
│   ├── Models/
│   │   ├── AppConfig.cs         (기존에서 이동)
│   │   ├── WindowConfig.cs      (기존에서 이동)
│   │   ├── HeaderButtonConfig.cs
│   │   ├── AccessibilityOptions.cs
│   │   ├── JsonOptions.cs
│   │   ├── KeyAction.cs
│   │   ├── KeySlot.cs
│   │   ├── LayoutConfig.cs
│   │   ├── SwitchScanMode.cs
│   │   ├── SwitchScanSuggestionPriority.cs
│   │   └── VirtualKeyCode.cs
│   └── Services/
│       ├── PathResolver.cs      (기존에서 이동)
│       └── ConfigService.cs     (기존에서 이동, 원자적 쓰기로 강화)
│
├── AltKey/                      ← 기존 프로젝트 (AltKey.App으로 리네임 예정)
│   └── (기존 파일 유지, AltKey.Core 참조 추가)
│
└── AltKey.Settings/             ← 신규: 설정 UI 앱
    ├── AltKey.Settings.csproj   (TargetFramework: net8.0-windows, UseWPF: true)
    ├── App.xaml / App.xaml.cs   (DI 컨테이너 구성)
    ├── Views/                   (기존 SettingsWindow 등 이관)
    ├── ViewModels/              (기존 SettingsViewModel 이관, 경량화)
    └── Services/
        └── IpcClientService.cs  (Named Pipe 클라이언트)
```

### 의존성 방향

```
AltKey.App  ──→  AltKey.Core
AltKey.Settings ──→  AltKey.Core
AltKey.App  ──X──  AltKey.Settings   (직접 참조 없음, IPC로만 통신)
```

### IPC 아키텍처

```
AltKey.App (Named Pipe 서버)          AltKey.Settings (Named Pipe 클라이언트)
┌─────────────────────────┐           ┌──────────────────────────────┐
│  IpcServerService       │◄──────────│  IpcClientService            │
│  - 파이프 이름:          │  메시지   │  - 설정 변경 시 메시지 전송  │
│    "AltKey.SettingsPipe" │  전송     │  - 연결 끊김 감지            │
│  - 메시지 수신 후        │           │  - 하트비트 수신             │
│    ConfigService 갱신   │           └──────────────────────────────┘
│  - FileSystemWatcher    │
│    폴백 (300ms 디바운싱) │
└─────────────────────────┘
```

---

## 컴포넌트 및 인터페이스

### 1. AltKey.Core — ConfigService (강화)

기존 `ConfigService`를 원자적 쓰기 방식으로 전면 개선합니다.

```csharp
// AltKey.Core/Services/ConfigService.cs
public class ConfigService
{
    public AppConfig Current { get; private set; } = new();
    public event Action<string?>? ConfigChanged;

    // 원자적 쓰기: config.tmp → File.Replace → config.json
    // 실패 시 최대 3회 재시도 (300ms 간격)
    public void Save();

    // 파일 잠금 시 최대 3회 재시도 (100ms 간격)
    // 모든 재시도 실패 시 Current 유지 + 로그 기록
    public void Load();

    // 설정 변경 + 저장 + 이벤트 발행 (기존과 동일)
    public void Update(Action<AppConfig> updater, string? propertyName = null);
}
```

**원자적 쓰기 흐름:**
```
1. config.bak 생성 (기존 config.json 복사)
2. config.tmp에 새 내용 쓰기
3. File.Replace(config.tmp, config.json, config.bak) 호출
4. 실패 시 300ms 대기 후 최대 3회 재시도
```

### 2. AltKey.Core — PathResolver (확장)

설정 앱 실행 파일 경로 결정 기능을 추가합니다.

```csharp
// AltKey.Core/Services/PathResolver.cs
public static class PathResolver
{
    public static bool IsPortable { get; }       // exe 옆 config.json 존재 여부
    public static string DataDir { get; }        // 포터블: exe 디렉터리 / 설치형: %AppData%\AltKey
    public static string LayoutsDir { get; }
    public static string ConfigPath { get; }

    // 설정 앱 실행 파일 경로 (AltKey.App과 동일 디렉터리)
    public static string SettingsExePath { get; }
}
```

### 3. AltKey.App — IpcServerService (신규)

Named Pipe 서버를 백그라운드에서 실행하며, 설정 변경 메시지를 수신합니다.

```csharp
// AltKey/Services/IpcServerService.cs
public class IpcServerService : IDisposable
{
    // 파이프 이름: "AltKey.SettingsPipe"
    // 백그라운드 Task로 실행 (UI 스레드 차단 없음)
    public void Start();
    public void Stop();

    // 수신된 IpcMessage를 ConfigService에 반영
    // 메시지 크기 > 4096바이트이면 무시
    private void HandleMessage(IpcMessage message);

    // AltKey.Settings에 종료 시그널 전송
    public Task SendShutdownAsync();
}
```

### 4. AltKey.App — FileSystemWatcherService (신규)

Named Pipe 폴백용 파일 변경 감지 서비스입니다.

```csharp
// AltKey/Services/FileSystemWatcherService.cs
public class FileSystemWatcherService : IDisposable
{
    // config.json 변경 감지 시작
    public void Start();

    // 300ms 디바운싱 적용 후 ConfigService.Load() 호출
    private void OnConfigFileChanged(object sender, FileSystemEventArgs e);
}
```

### 5. AltKey.Settings — IpcClientService (신규)

Named Pipe 클라이언트로, 설정 변경 시 AltKey.App에 메시지를 전송합니다.

```csharp
// AltKey.Settings/Services/IpcClientService.cs
public class IpcClientService : IDisposable
{
    // 설정 변경 메시지 전송
    public Task SendAsync(IpcMessage message);

    // 연결 끊김 감지 → 부모 프로세스 생존 확인
    private void OnConnectionLost();
}
```

### 6. IPC 메시지 형식 (공용)

```csharp
// AltKey.Core/Models/IpcMessage.cs
public class IpcMessage
{
    // 메시지 종류: "config_changed" | "shutdown" | "activate"
    public string Command { get; set; } = "";

    // 변경된 속성 이름 (config_changed 시 사용)
    public string? PropertyName { get; set; }

    // 변경된 값의 JSON 직렬화 문자열
    public string? ValueJson { get; set; }
}
```

### 7. AltKey.Settings — App.xaml.cs (신규 진입점)

```csharp
// AltKey.Settings/App.xaml.cs
public partial class App : Application
{
    // Named Mutex: "AltKey.Settings_SingleInstance"
    // 중복 실행 시 기존 창 활성화 후 종료
    protected override void OnStartup(StartupEventArgs e);

    // DI 컨테이너 구성:
    //   - ConfigService (AltKey.Core)
    //   - ThemeService
    //   - LayoutService
    //   - IpcClientService
    //   - SettingsViewModel (경량화 버전)
    //   - AccessibilityService (TTS)
    private IServiceProvider ConfigureServices();
}
```

### 8. AltKey.Settings — SettingsViewModel (경량화)

기존 `SettingsViewModel`에서 AltKey.App 전용 서비스 의존성을 제거합니다.

| 제거 대상 | 이유 |
|-----------|------|
| `HotkeyService` | 전역 단축키는 AltKey.App에서만 관리 |
| `StartupService` | 자동 실행 레지스트리는 AltKey.App에서만 관리 |
| `InputService` | 입력 엔진은 AltKey.App 전용 |
| `UpdateService`, `DownloadService`, `InstallerService` | 업데이트는 AltKey.App에서만 관리 |
| `ProfileService` | 포그라운드 앱 감지는 AltKey.App 전용 |

설정 변경 시 `IpcClientService`를 통해 AltKey.App에 메시지를 전송하는 로직이 추가됩니다.

---

## 데이터 모델

### IpcMessage

```csharp
// 두 앱 간 Named Pipe로 교환되는 메시지 구조
public class IpcMessage
{
    public string Command { get; set; } = "";      // "config_changed" | "shutdown" | "activate"
    public string? PropertyName { get; set; }      // 변경된 AppConfig 속성명
    public string? ValueJson { get; set; }         // 변경된 값 (JSON 직렬화)
}
```

**메시지 예시:**
```json
// 테마 변경
{"command":"config_changed","property_name":"Theme","value_json":"\"Dark\""}

// 종료 시그널
{"command":"shutdown"}

// 창 활성화 요청 (중복 실행 방지)
{"command":"activate"}
```

### AppConfig (변경 없음)

기존 `AppConfig` 구조는 그대로 유지됩니다. AltKey.Core로 이동만 합니다.

### 설정 파일 경로 결정 로직

```
IsPortable = File.Exists(Path.Combine(exeDir, "config.json"))

포터블 모드:
  DataDir    = exeDir
  ConfigPath = exeDir/config.json
  LayoutsDir = exeDir/layouts/

설치형 모드:
  DataDir    = %AppData%\AltKey
  ConfigPath = %AppData%\AltKey\config.json
  LayoutsDir = %AppData%\AltKey\layouts\

공통:
  SettingsExePath = exeDir/AltKey.Settings.exe
  TmpPath         = ConfigPath + ".tmp"   (원자적 쓰기용)
  BakPath         = ConfigPath + ".bak"   (백업용)
```

---

## 정확성 속성 (Correctness Properties)

*속성(Property)이란 시스템의 모든 유효한 실행에서 참이어야 하는 특성 또는 동작입니다. 즉, 시스템이 무엇을 해야 하는지에 대한 형식적 명세입니다. 속성은 사람이 읽을 수 있는 명세와 기계가 검증할 수 있는 정확성 보장 사이의 다리 역할을 합니다.*

### Property 1: AppConfig 직렬화 라운드트립

*임의의* 유효한 `AppConfig` 객체에 대해, `JsonOptions.Default`로 직렬화한 후 역직렬화한 결과는 원본 객체의 모든 필드와 동등해야 한다.

**Validates: Requirements 9.3**

### Property 2: 알 수 없는 JSON 속성 무시 및 기본값 적용

*임의의* 추가 속성이 포함된 JSON 문자열 또는 일부 속성이 누락된 JSON 문자열을 `ConfigService.Load()`로 역직렬화할 때, 예외 없이 성공하고 누락된 속성에는 `AppConfig` 기본값이 적용되어야 한다.

**Validates: Requirements 9.2**

### Property 3: IPC 메시지 직렬화 라운드트립

*임의의* 속성명과 값 조합으로 생성된 `IpcMessage` 객체에 대해, JSON 직렬화 후 역직렬화한 결과는 원본 메시지와 동등해야 한다.

**Validates: Requirements 4.1**

### Property 4: IPC 메시지 크기 검증

*임의의* 크기의 바이트 배열을 Named Pipe 메시지로 수신할 때, 4096바이트를 초과하는 메시지는 반드시 무시되고 4096바이트 이하의 메시지만 처리되어야 한다.

**Validates: Requirements 4.6**

### Property 5: ConfigService 재시도 로직

*임의의* 횟수(0~3회)의 `IOException` 실패 후 성공하는 저장 시나리오에서, `ConfigService.Save()`는 최대 3회까지 재시도하고 3회 이내에 성공하면 파일이 올바르게 저장되어야 한다. 3회 모두 실패하면 마지막으로 성공한 설정 값이 유지되어야 한다.

**Validates: Requirements 5.2, 5.3, 5.4**

### Property 6: WindowConfig 마이그레이션 정확성

*임의의* `Width` 값(픽셀)을 가진 이전 버전 `WindowConfig` JSON을 로드할 때, 계산된 `Scale` 값은 `round(Width / 900.0 * 100)`이어야 하며 60~200 범위로 클램핑되어야 한다.

**Validates: Requirements 9.5**

### Property 7: 창 위치 화면 내 배치

*임의의* 메인 창 위치(Left, Top)와 화면 크기에 대해, 설정 창의 초기 배치 위치는 항상 화면 경계 내에 있어야 한다 (창이 화면 밖으로 벗어나지 않아야 한다).

**Validates: Requirements 7.2**

---

## 오류 처리

### ConfigService 오류 처리

| 오류 상황 | 처리 방법 |
|-----------|-----------|
| `config.json` 파일 없음 | 기본값 `AppConfig`로 새 파일 생성 |
| JSON 파싱 실패 (손상된 파일) | 기본값 `AppConfig` 사용, 오류 로그 기록 |
| 파일 잠금 (읽기) | 100ms 간격으로 최대 3회 재시도, 실패 시 현재 값 유지 |
| 파일 잠금 (쓰기) | 300ms 간격으로 최대 3회 재시도 |
| `File.Replace` 실패 | 300ms 간격으로 최대 3회 재시도, `config.bak`으로 복구 가능 |

### IPC 오류 처리

| 오류 상황 | 처리 방법 |
|-----------|-----------|
| Named Pipe 연결 실패 (AltKey.App 측) | `FileSystemWatcher` 폴백으로 자동 전환 |
| Named Pipe 연결 끊김 (AltKey.Settings 측) | 부모 프로세스 생존 확인 → 없으면 자동 종료 |
| 메시지 크기 > 4096바이트 | 메시지 무시, 로그 기록 |
| 메시지 역직렬화 실패 | 메시지 무시, 로그 기록 |
| `AltKey.Settings.exe` 파일 없음 | 사용자에게 안내 메시지 표시 후 중단 |

### 프로세스 생명주기 오류 처리

| 오류 상황 | 처리 방법 |
|-----------|-----------|
| AltKey.App 정상 종료 | Named Pipe로 `shutdown` 시그널 전송 |
| AltKey.App 비정상 종료 | AltKey.Settings 하트비트 검사(30초 주기)로 감지, 60초 내 자동 종료 |
| AltKey.Settings 중복 실행 | Named Mutex로 감지, 기존 창 활성화 후 새 인스턴스 종료 |

---

## 테스트 전략

### 단위 테스트 (예시 기반)

**ConfigService 테스트:**
- 포터블/설치형 모드별 `PathResolver.DataDir` 경로 반환값 검증
- 손상된 JSON 로드 시 기본값 `AppConfig` 반환 확인
- `Save()` 호출 후 `config.tmp` → `config.json` 교체 확인 (파일 시스템 mock)
- `Save()` 호출 후 `config.bak` 생성 확인
- 이전 버전 JSON(`Width`/`Height` 기반) 로드 시 `Scale` 마이그레이션 확인

**IPC 테스트:**
- `IpcMessage` 직렬화/역직렬화 정확성 확인
- 4096바이트 초과 메시지 무시 확인
- `shutdown` 명령 수신 시 `Application.Shutdown()` 호출 확인 (mock)
- 파이프 연결 끊김 후 부모 프로세스 확인 로직 검증

**프로세스 제어 테스트:**
- `AltKey.Settings.exe` 파일 없을 때 에러 메시지 표시 확인
- Named Mutex 중복 실행 방지 로직 검증
- 최소화 상태 창 활성화 요청 시 복원 확인

### 속성 기반 테스트 (Property-Based Testing)

속성 기반 테스트 라이브러리: **FsCheck** (NuGet: `FsCheck` + `FsCheck.Xunit`)

각 속성 테스트는 최소 100회 반복 실행합니다.

**Property 1: AppConfig 직렬화 라운드트립**
```csharp
// Feature: standalone-settings-app, Property 1: AppConfig 직렬화 라운드트립
[Property]
public Property AppConfig_RoundTrip(AppConfig config)
{
    var json = JsonSerializer.Serialize(config, JsonOptions.Default);
    var restored = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default);
    return (restored != null && AreEquivalent(config, restored)).ToProperty();
}
```

**Property 2: 알 수 없는 JSON 속성 무시**
```csharp
// Feature: standalone-settings-app, Property 2: 알 수 없는 JSON 속성 무시 및 기본값 적용
[Property]
public Property UnknownProperties_AreIgnored(string extraKey, string extraValue)
{
    var json = $"{{\"unknown_{extraKey}\": \"{extraValue}\"}}";
    var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default);
    return (config != null).ToProperty();
}
```

**Property 3: IPC 메시지 라운드트립**
```csharp
// Feature: standalone-settings-app, Property 3: IPC 메시지 직렬화 라운드트립
[Property]
public Property IpcMessage_RoundTrip(string command, string? propertyName, string? valueJson)
{
    var msg = new IpcMessage { Command = command, PropertyName = propertyName, ValueJson = valueJson };
    var json = JsonSerializer.Serialize(msg, JsonOptions.Default);
    var restored = JsonSerializer.Deserialize<IpcMessage>(json, JsonOptions.Default);
    return (restored != null
        && restored.Command == msg.Command
        && restored.PropertyName == msg.PropertyName
        && restored.ValueJson == msg.ValueJson).ToProperty();
}
```

**Property 4: IPC 메시지 크기 검증**
```csharp
// Feature: standalone-settings-app, Property 4: IPC 메시지 크기 검증
[Property]
public Property MessageOver4096_IsRejected(byte[] data)
{
    bool isOversized = data.Length > 4096;
    bool wasProcessed = IpcServerService.TryProcess(data);
    return (isOversized == !wasProcessed).ToProperty();
}
```

**Property 5: ConfigService 재시도 로직**
```csharp
// Feature: standalone-settings-app, Property 5: ConfigService 재시도 로직
[Property]
public Property Save_RetriesUpTo3Times(PositiveInt failCount)
{
    int failures = Math.Min(failCount.Get, 4); // 0~4회 실패 시나리오
    var svc = new ConfigServiceWithMockIo(failuresBeforeSuccess: failures);
    bool succeeded = svc.TrySave();
    return (failures <= 3 ? succeeded : !succeeded).ToProperty();
}
```

**Property 6: WindowConfig 마이그레이션**
```csharp
// Feature: standalone-settings-app, Property 6: WindowConfig 마이그레이션 정확성
[Property]
public Property WindowConfig_Migration_ScaleIsCorrect(PositiveInt widthPixels)
{
    int width = widthPixels.Get;
    var json = $"{{\"window\":{{\"width\":{width}}}}}";
    var config = LoadWithMigration(json);
    int expectedScale = Math.Clamp((int)Math.Round(width / 900.0 * 100), 60, 200);
    return (config.Window.Scale == expectedScale).ToProperty();
}
```

**Property 7: 창 위치 화면 내 배치**
```csharp
// Feature: standalone-settings-app, Property 7: 창 위치 화면 내 배치
[Property]
public Property SettingsWindow_IsWithinScreenBounds(
    double mainLeft, double mainTop, double screenWidth, double screenHeight)
{
    var pos = AuxiliaryWindowPlacement.CalculateSettingsPosition(
        mainLeft, mainTop, settingsWidth: 694, settingsHeight: 780,
        screenWidth, screenHeight);
    return (pos.Left >= 0 && pos.Top >= 0
        && pos.Left + 694 <= screenWidth
        && pos.Top + 780 <= screenHeight).ToProperty();
}
```

### 통합 테스트

- AltKey.App 실행 → 설정 버튼 클릭 → AltKey.Settings.exe 실행 확인
- AltKey.Settings에서 테마 변경 → AltKey.App 테마 즉시 반영 확인
- AltKey.App 종료 → AltKey.Settings 자동 종료 확인 (60초 내)
- 포터블 모드에서 두 앱 모두 exe 옆 `config.json` 사용 확인

### 접근성 검증 (수동)

- Narrator(내레이터)로 설정 창 전체 탭 탐색 시 모든 컨트롤 읽힘 확인
- 스위치 스캔 모드에서 설정 창 조작 가능 여부 확인
- `ReducedMotionEnabled=true` 시 페이드인 애니메이션 생략 확인
- `TtsEnabled=true` 시 설정 변경 값 TTS 읽기 확인
