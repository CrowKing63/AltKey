# 구현 계획: 독립 설정 앱 분리 (standalone-settings-app)

## 개요

AltKey의 설정 화면을 독립 프로세스(`AltKey.Settings.exe`)로 분리합니다.
구현 순서는 공용 라이브러리(AltKey.Core) 구축 → AltKey.App 정리 → AltKey.Settings 신규 앱 생성 → IPC 인프라 → 폴백/안전성 강화 → UX 마무리 순입니다.

---

## 태스크

- [ ] 1. AltKey.Core 프로젝트 생성 및 공용 모델·서비스 이동
  - [ ] 1.1 AltKey.Core.csproj 파일 생성
    - `AltKey.Core/` 디렉터리를 솔루션 루트에 생성한다.
    - `TargetFramework: net8.0-windows`, `OutputType: Library`, `Nullable: enable`, `ImplicitUsings: enable` 설정
    - NuGet 패키지: `CommunityToolkit.Mvvm 8.*`, `Microsoft.Extensions.DependencyInjection 8.*`, `System.Text.Json 8.*` 추가
    - _요구사항: 1.4_

  - [ ] 1.2 데이터 모델 파일을 AltKey.Core/Models/로 이동
    - `AltKey/Models/` 아래의 다음 파일을 `AltKey.Core/Models/`로 이동한다:
      `AppConfig.cs`, `WindowConfig.cs`, `HeaderButtonConfig.cs`, `AccessibilityOptions.cs`,
      `JsonOptions.cs`, `KeyAction.cs`, `KeySlot.cs`, `LayoutConfig.cs`,
      `SwitchScanMode.cs`, `SwitchScanSuggestionPriority.cs`, `VirtualKeyCode.cs`
    - 각 파일의 `namespace`를 `AltKey.Core.Models`로 변경한다.
    - _요구사항: 1.1, 1.5_

  - [ ] 1.3 PathResolver를 AltKey.Core/Services/로 이동 및 확장
    - `AltKey/Services/PathResolver.cs`를 `AltKey.Core/Services/PathResolver.cs`로 이동한다.
    - `namespace`를 `AltKey.Core.Services`로 변경한다.
    - `SettingsExePath` 정적 프로퍼티를 추가한다: `exeDir/AltKey.Settings.exe` 경로 반환
    - `TmpPath` (ConfigPath + ".tmp"), `BakPath` (ConfigPath + ".bak") 프로퍼티도 추가한다.
    - _요구사항: 1.2, 2.5, 3.3_

  - [ ] 1.4 ConfigService를 AltKey.Core/Services/로 이동 및 원자적 쓰기로 강화
    - `AltKey/Services/ConfigService.cs`를 `AltKey.Core/Services/ConfigService.cs`로 이동한다.
    - `namespace`를 `AltKey.Core.Services`로 변경한다.
    - `Save()` 메서드를 원자적 쓰기 방식으로 교체한다:
      1. `config.bak` 생성 (기존 `config.json` 복사)
      2. `config.tmp`에 새 내용 쓰기
      3. `File.Replace(tmpPath, configPath, bakPath)` 호출
      4. 실패 시 300ms 대기 후 최대 3회 재시도
    - `Load()` 메서드에 파일 잠금 재시도 로직 추가: 100ms 간격으로 최대 3회, 모두 실패 시 `Current` 유지 + 로그 기록
    - `IpcMessage.cs`를 `AltKey.Core/Models/`에 신규 생성한다:
      ```csharp
      public class IpcMessage
      {
          public string Command { get; set; } = "";       // "config_changed" | "shutdown" | "activate"
          public string? PropertyName { get; set; }       // 변경된 AppConfig 속성명
          public string? ValueJson { get; set; }          // 변경된 값 (JSON 직렬화)
      }
      ```
    - _요구사항: 1.3, 5.1, 5.2, 5.3, 5.4, 5.5, 9.1, 9.2, 9.4, 9.5_

  - [ ]* 1.5 Property 1: AppConfig 직렬화 라운드트립 테스트 작성
    - **Property 1: AppConfig 직렬화 라운드트립**
    - FsCheck를 사용하여 임의의 `AppConfig` 객체를 직렬화 후 역직렬화한 결과가 원본과 동등함을 검증한다.
    - **검증 요구사항: 9.3**

  - [ ]* 1.6 Property 2: 알 수 없는 JSON 속성 무시 및 기본값 적용 테스트 작성
    - **Property 2: 알 수 없는 JSON 속성 무시 및 기본값 적용**
    - 추가 속성이 포함된 JSON 또는 일부 속성이 누락된 JSON을 역직렬화할 때 예외 없이 성공하고 기본값이 적용됨을 검증한다.
    - **검증 요구사항: 9.2**

  - [ ]* 1.7 Property 5: ConfigService 재시도 로직 테스트 작성
    - **Property 5: ConfigService 재시도 로직**
    - 0~4회 `IOException` 실패 시나리오에서 `Save()`가 3회 이내 성공 시 파일이 올바르게 저장되고, 3회 모두 실패 시 마지막 성공 값이 유지됨을 검증한다.
    - **검증 요구사항: 5.2, 5.3, 5.4**

  - [ ]* 1.8 Property 6: WindowConfig 마이그레이션 정확성 테스트 작성
    - **Property 6: WindowConfig 마이그레이션 정확성**
    - 임의의 `Width` 픽셀 값을 가진 이전 버전 JSON 로드 시 `Scale = clamp(round(Width / 900.0 * 100), 60, 200)` 공식이 항상 성립함을 검증한다.
    - **검증 요구사항: 9.5**

- [ ] 2. AltKey.App에서 AltKey.Core 참조 추가 및 기존 코드 정리
  - [ ] 2.1 AltKey.csproj에 AltKey.Core 프로젝트 참조 추가
    - `AltKey.csproj`에 `<ProjectReference Include="..\AltKey.Core\AltKey.Core.csproj" />` 추가
    - 기존 `AltKey/Models/`와 `AltKey/Services/PathResolver.cs`, `AltKey/Services/ConfigService.cs`에서 이동된 파일들을 삭제한다.
    - 프로젝트 전체에서 `using AltKey.Models` → `using AltKey.Core.Models`, `using AltKey.Services` (PathResolver/ConfigService) → `using AltKey.Core.Services`로 네임스페이스를 일괄 수정한다.
    - _요구사항: 1.4_

  - [ ] 2.2 AltKey.App에서 설정 창 열기 로직을 Process.Start 방식으로 교체
    - `SettingsViewModel.OpenSettings()` 커맨드를 수정한다:
      - `AltKey.Views.SettingsWindow`를 직접 생성하는 코드를 제거한다.
      - `PathResolver.SettingsExePath`로 `AltKey.Settings.exe` 경로를 확인한다.
      - 파일이 없으면 `MessageBox`로 안내 메시지를 표시하고 중단한다.
      - 파일이 있으면 `Process.Start(SettingsExePath)`로 실행한다.
    - `TrayService`의 "설정" 메뉴 항목도 동일하게 `Process.Start` 방식으로 수정한다.
    - _요구사항: 3.1, 3.3, 3.4_

  - [ ] 2.3 체크포인트 — AltKey.App 빌드 및 기본 동작 확인
    - 모든 테스트가 통과하는지 확인하고, 질문이 있으면 사용자에게 문의한다.

- [ ] 3. AltKey.Settings 프로젝트 생성 및 설정 UI 이관
   - [ ] 3.1 AltKey.Settings.csproj 파일 생성
    - `AltKey.Settings/` 디렉터리를 솔루션 루트에 생성한다.
    - `TargetFramework: net8.0-windows`, `UseWPF: true`, `OutputType: WinExe`, `Nullable: enable`, `ImplicitUsings: enable` 설정
    - NuGet 패키지: `CommunityToolkit.Mvvm 8.*`, `Microsoft.Extensions.DependencyInjection 8.*`, `WPF-UI 3.*`, `System.Speech 8.*` 추가
    - `<ProjectReference>` 로 `AltKey.Core` 참조 추가
    - `PublishSingleFile=true`, `SelfContained=false` 발행 프로파일 추가
    - _요구사항: 2.1, 2.5, 8.5_

   - [ ] 3.2 설정 관련 뷰·뷰모델 파일을 AltKey.Settings로 복사 및 이관
    - 다음 파일을 `AltKey.Settings/Views/`로 복사한다:
      `SettingsWindow.xaml/.cs`, `SwitchScanSettingsWindow.xaml/.cs`,
      `FocusA11ySettingsWindow.xaml/.cs`, `UserDictionaryEditorWindow.xaml/.cs`,
      `LayoutEditorWindow.xaml/.cs`
    - `AltKey.Settings/ViewModels/SettingsViewModel.cs`를 생성한다 (기존 파일 기반으로 경량화):
      - 제거 대상 서비스 의존성: `HotkeyService`, `StartupService`, `InputService`, `UpdateService`, `DownloadService`, `InstallerService`, `ProfileService`
      - 제거된 서비스와 관련된 `[ObservableProperty]` 필드 및 `partial void On...Changed` 핸들러도 함께 제거한다.
      - `namespace`를 `AltKey.Settings.ViewModels`로 변경한다.
    - 필요한 서비스 파일(`ThemeService.cs`, `LayoutService.cs`, `AuxiliaryWindowPlacement.cs`, `AccessibilityService.cs`, `SoundService.cs`, `SecureStorage.cs`, `AiService.cs` 등)을 `AltKey.Settings/Services/`로 복사한다.
    - _요구사항: 2.2, 2.3, 2.4_

  - [ ] 3.3 AltKey.Settings App.xaml / App.xaml.cs 작성 (DI 컨테이너 + 단일 인스턴스)
    - `App.xaml.cs`에서 `OnStartup`을 구현한다:
      - Named Mutex `"AltKey.Settings_SingleInstance"` 로 중복 실행 감지
      - 중복 실행 시: Named Pipe로 `{"command":"activate"}` 메시지를 전송하고 현재 인스턴스를 종료한다.
      - DI 컨테이너에 다음 서비스를 등록한다:
        `ConfigService`, `ThemeService`, `LayoutService`, `IpcClientService`,
        `AccessibilityService`, `SoundService`, `AiService`,
        `LayoutEditorViewModel`, `UserDictionaryEditorViewModel`, `SettingsViewModel`
      - `config.json`에서 테마를 읽어 즉시 적용한다.
    - `OnExit`에서 Mutex 해제 및 서비스 정리를 수행한다.
    - _요구사항: 2.4, 2.6, 3.2, 3.5, 7.3_

  - [ ] 3.4 체크포인트 — AltKey.Settings 단독 빌드 및 창 표시 확인
    - 모든 테스트가 통과하는지 확인하고, 질문이 있으면 사용자에게 문의한다.

- [ ] 4. IPC 인프라 구현 — Named Pipe 서버(AltKey.App) 및 클라이언트(AltKey.Settings)
  - [ ] 4.1 IpcServerService 구현 (AltKey.App)
    - `AltKey/Services/IpcServerService.cs`를 신규 생성한다.
    - 파이프 이름: `"AltKey.SettingsPipe"`
    - `Start()`: 백그라운드 `Task`로 Named Pipe 서버를 실행한다 (UI 스레드 차단 없음).
    - 메시지 수신 루프: 연결 수락 → 메시지 읽기 → `HandleMessage()` 호출 → 다음 연결 대기
    - `HandleMessage(IpcMessage)`:
      - 메시지 바이트 크기가 4096 초과이면 무시하고 로그를 기록한다.
      - `command == "config_changed"`: `ConfigService.Update()`로 해당 속성을 반영하고 UI를 갱신한다.
      - `command == "activate"`: 기존 창을 포그라운드로 활성화한다.
    - `SendShutdownAsync()`: `{"command":"shutdown"}` 메시지를 클라이언트에 전송한다.
    - `Stop()` / `Dispose()`: 파이프 서버를 안전하게 종료한다.
    - `App.xaml.cs`의 `OnStartup`에서 `IpcServerService.Start()`를 호출하고, `OnExit`에서 `Stop()`을 호출한다.
    - _요구사항: 4.1, 4.2, 4.5, 4.6, 6.1_

  - [ ] 4.2 IpcClientService 구현 (AltKey.Settings)
    - `AltKey.Settings/Services/IpcClientService.cs`를 신규 생성한다.
    - `SendAsync(IpcMessage)`: Named Pipe 클라이언트로 연결하여 메시지를 JSON 직렬화 후 전송한다.
    - 연결 실패 시 조용히 실패하고 로그를 기록한다 (설정 저장 자체는 항상 성공해야 함).
    - `OnConnectionLost()`: 파이프 연결이 끊어지면 AltKey.App 프로세스 생존 여부를 확인한다.
    - _요구사항: 4.1, 6.3_

  - [ ] 4.3 AltKey.Settings SettingsViewModel에 IPC 전송 로직 연결
    - `SettingsViewModel`의 각 `partial void On...Changed` 핸들러에서 설정 저장 후 `IpcClientService.SendAsync()`를 호출하여 변경 내용을 AltKey.App에 전달한다.
    - 전송 실패는 무시한다 (폴백은 FileSystemWatcher가 담당).
    - _요구사항: 4.1, 10.5_

  - [ ]* 4.4 Property 3: IPC 메시지 직렬화 라운드트립 테스트 작성
    - **Property 3: IPC 메시지 직렬화 라운드트립**
    - 임의의 `command`, `propertyName`, `valueJson` 조합으로 생성된 `IpcMessage`를 직렬화 후 역직렬화한 결과가 원본과 동등함을 검증한다.
    - **검증 요구사항: 4.1**

  - [ ]* 4.5 Property 4: IPC 메시지 크기 검증 테스트 작성
    - **Property 4: IPC 메시지 크기 검증**
    - 임의 크기의 바이트 배열에 대해 4096바이트 초과 메시지는 반드시 무시되고, 이하 메시지만 처리됨을 검증한다.
    - **검증 요구사항: 4.6**

  - [ ] 4.6 체크포인트 — IPC 통신 동작 확인
    - 모든 테스트가 통과하는지 확인하고, 질문이 있으면 사용자에게 문의한다.

- [ ] 5. FileSystemWatcher 폴백 구현 (AltKey.App)
  - [ ] 5.1 FileSystemWatcherService 구현
    - `AltKey/Services/FileSystemWatcherService.cs`를 신규 생성한다.
    - `Start()`: `PathResolver.ConfigPath`를 감시하는 `FileSystemWatcher`를 시작한다.
    - `OnConfigFileChanged()`: 변경 이벤트 수신 시 300ms 디바운싱을 적용한 뒤 `ConfigService.Load()`를 호출한다.
      - 디바운싱은 `System.Threading.Timer` 또는 `CancellationTokenSource` 방식으로 구현한다.
    - `Stop()` / `Dispose()`: `FileSystemWatcher`를 안전하게 해제한다.
    - `App.xaml.cs`의 `OnStartup`에서 `FileSystemWatcherService.Start()`를 호출하고, `OnExit`에서 `Stop()`을 호출한다.
    - _요구사항: 4.3, 4.4, 4.5_

- [ ] 6. 고아 프로세스 방지 — 하트비트 및 종료 시그널
  - [ ] 6.1 AltKey.App 종료 시 shutdown 시그널 전송
    - `App.xaml.cs`의 `OnExit`에서 `IpcServerService.SendShutdownAsync()`를 호출한다.
    - _요구사항: 6.1_

  - [ ] 6.2 AltKey.Settings에 하트비트 검사 구현
    - `IpcClientService` 또는 별도 `HeartbeatService`에 30초 주기 타이머를 추가한다.
    - 타이머 콜백에서 AltKey.App 프로세스(`Process.GetProcessesByName("AltKey")`)의 생존 여부를 확인한다.
    - 프로세스가 없으면 `Application.Current.Shutdown()`을 호출하여 60초 이내에 자동 종료한다.
    - _요구사항: 6.3, 6.4, 6.5_

  - [ ] 6.3 AltKey.Settings에 shutdown 명령 수신 처리 구현
    - `IpcClientService`의 수신 루프(또는 별도 수신 서비스)에서 `command == "shutdown"` 메시지를 받으면 `Application.Current.Shutdown()`을 호출한다.
    - _요구사항: 6.2_

- [ ] 7. 원자적 파일 쓰기 강화 검증 및 통합
  - [ ] 7.1 AltKey.Tests에 FsCheck 패키지 추가 및 테스트 프로젝트 설정
    - `AltKey.Tests.csproj`에 `FsCheck 3.*`, `FsCheck.Xunit 3.*` NuGet 패키지를 추가한다.
    - `AltKey.Core` 프로젝트 참조를 추가한다.
    - 속성 기반 테스트 파일을 담을 `AltKey.Tests/PropertyTests/` 디렉터리를 생성한다.
    - _요구사항: 5.1~5.5, 9.3_

  - [ ] 7.2 ConfigServiceAtomicWriteTests.cs 작성
    - `Save()` 호출 후 `config.tmp` → `config.json` 교체가 이루어지는지 파일 시스템 mock으로 확인한다.
    - `Save()` 호출 후 `config.bak`이 생성되는지 확인한다.
    - 손상된 JSON 로드 시 기본값 `AppConfig`가 반환되는지 확인한다.
    - _요구사항: 5.1, 5.5, 9.4_

- [ ] 8. 시각적 일관성 및 UX — 페이드인, 창 위치, 활성화 강조
  - [ ] 8.1 AltKey.Settings 창 페이드인 애니메이션 구현
    - `SettingsWindow.xaml.cs`의 `Loaded` 이벤트 핸들러에서 `DoubleAnimation`을 사용하여 150ms 동안 `Opacity 0 → 1` 애니메이션을 적용한다.
    - `config.ReducedMotionEnabled == true`이면 애니메이션을 생략하고 즉시 `Opacity = 1`로 설정한다.
    - _요구사항: 7.1, 7.5_

  - [ ] 8.2 AltKey.Settings 초기 창 위치 설정
    - `AltKey.Settings/Services/`에 `AuxiliaryWindowPlacement.cs`를 복사하거나 AltKey.Core로 이동한다.
    - `App.xaml.cs`에서 `SettingsWindow`를 표시하기 전에 `AuxiliaryWindowPlacement.CenterNear()`를 호출하여 화면 중앙 근처에 배치한다.
    - AltKey.App 메인 창의 위치 정보를 IPC 또는 `config.json`의 `Window` 설정을 통해 참조한다.
    - _요구사항: 7.2_

  - [ ] 8.3 중복 실행 시 창 활성화 강조 표시 구현
    - `command == "activate"` 수신 시 창을 포그라운드로 이동하고 최소화 상태라면 복원한다.
    - 300ms 동안 창 테두리를 강조 표시하는 애니메이션(`BorderBrush` 또는 `DropShadow` 효과)을 적용한다.
    - `config.ReducedMotionEnabled == true`이면 강조 애니메이션을 생략한다.
    - _요구사항: 3.5, 7.4, 7.5_

  - [ ]* 8.4 Property 7: 창 위치 화면 내 배치 테스트 작성
    - **Property 7: 창 위치 화면 내 배치**
    - 임의의 메인 창 위치(Left, Top)와 화면 크기에 대해 설정 창의 초기 배치 위치가 항상 화면 경계 내에 있음을 검증한다.
    - `AuxiliaryWindowPlacement.CenterNear()` 또는 위치 계산 함수를 직접 테스트한다.
    - **검증 요구사항: 7.2**

- [ ] 9. 포터블 모드 지원 검증
  - [ ] 9.1 포터블/설치형 모드 PathResolver 동작 단위 테스트 작성
    - `exe` 옆에 `config.json`이 있을 때 `IsPortable == true`, `DataDir == exeDir`임을 확인한다.
    - `exe` 옆에 `config.json`이 없을 때 `IsPortable == false`, `DataDir == %AppData%\AltKey`임을 확인한다.
    - _요구사항: 8.1, 8.2, 8.3_

- [ ] 10. 최종 통합 및 접근성 검증
  - [ ] 10.1 AltKey.App에서 AltKey.Settings 실행 흐름 통합 확인
    - `SettingsViewModel.OpenSettings()` 및 `TrayService` 설정 메뉴에서 `Process.Start`로 `AltKey.Settings.exe`가 실행되는지 확인한다.
    - `AltKey.Settings.exe`가 없을 때 안내 메시지가 표시되는지 확인한다.
    - _요구사항: 3.1, 3.4_

  - [ ] 10.2 접근성 속성 유지 확인
    - 이관된 `SettingsWindow.xaml`의 모든 컨트롤에 `AutomationProperties.Name` 및 `AutomationProperties.HelpText`가 기존과 동일하게 유지되는지 검토한다.
    - 탭 순서(`KeyboardNavigation.TabIndex`)가 기존과 동일한지 확인한다.
    - `WS_EX_NOACTIVATE` 스타일이 설정 창에 적용되지 않았는지 확인한다.
    - _요구사항: 10.1, 10.2, 10.3_

  - [ ] 10.3 최종 체크포인트 — 전체 테스트 통과 확인
    - 모든 테스트가 통과하는지 확인하고, 질문이 있으면 사용자에게 문의한다.

---

## 참고 사항

- `*` 표시가 붙은 서브태스크는 선택 사항으로, MVP 구현 시 건너뛸 수 있습니다.
- 각 태스크는 이전 태스크의 결과물을 기반으로 하므로 순서대로 진행해야 합니다.
- 체크포인트 태스크에서 빌드 오류나 테스트 실패가 발생하면 다음 단계로 진행하기 전에 반드시 해결합니다.
- 속성 기반 테스트(Property *)는 FsCheck를 사용하며, 각 속성은 최소 100회 반복 실행합니다.
- 단위 테스트는 `AltKey.Tests` 프로젝트의 기능별 파일에 추가하되, 한 파일이 400줄을 넘지 않도록 유지합니다.
- 자동완성/한글 조합 관련 코드(`KoreanInputModule`, `HangulComposer` 등)는 이번 작업에서 수정하지 않습니다.
