# 요구사항 문서

## 소개

AltKey의 설정 화면(`SettingsWindow`)을 메인 키보드 프로세스(`AltKey.App.exe`)에서 분리하여 독립 실행 파일(`AltKey.Settings.exe`)로 구현하는 기능입니다.

현재 구조에서는 가상 키보드와 설정 창이 동일 프로세스 내에 존재하기 때문에, 사용자가 자체 가상 키보드로 설정 항목을 입력할 때 포커스 전이 문제가 발생하여 한글 자모가 분리되는 UX 결함이 있습니다. 설정 앱을 별도 프로세스로 분리하면 가상 키보드가 설정 앱을 '외부 앱'으로 인식하게 되어 이 문제가 근본적으로 해결됩니다.

분리 후 프로젝트 구조:
- **AltKey.Core**: `AppConfig`, `WindowConfig` 등 데이터 모델과 `PathResolver`, `ConfigService`, 로거 등 공용 유틸리티를 담는 공유 라이브러리
- **AltKey.App**: 가상 키보드 핵심 UI 및 입력 서비스 (기존 메인 프로젝트)
- **AltKey.Settings**: 설정 UI 및 프로필 관리 전용 독립 앱

---

## 용어 정의

- **AltKey.App**: 가상 키보드 핵심 기능을 담당하는 메인 실행 파일 (`AltKey.App.exe`)
- **AltKey.Settings**: 설정 UI를 담당하는 독립 실행 파일 (`AltKey.Settings.exe`)
- **AltKey.Core**: 두 앱이 공유하는 데이터 모델 및 유틸리티 라이브러리 (`.dll`)
- **ConfigService**: `config.json`을 읽고 쓰며 변경 이벤트를 발행하는 서비스
- **PathResolver**: 포터블/설치형 환경에 따라 데이터 디렉터리 경로를 결정하는 정적 클래스
- **IPC (Inter-Process Communication)**: 두 프로세스 간 데이터를 주고받는 통신 메커니즘
- **Named Pipe**: Windows에서 프로세스 간 실시간 메시지 전달에 사용하는 IPC 채널
- **FileSystemWatcher**: 파일 변경을 감지하는 .NET 클래스. Named Pipe 장애 시 폴백으로 사용
- **원자적 쓰기 (Atomic Write)**: 임시 파일에 먼저 쓴 뒤 원본 파일을 교체하는 방식으로 파일 손상을 방지하는 기법
- **포터블 모드**: 설치 없이 실행 파일 옆에 `config.json`이 있으면 활성화되는 무설치 실행 모드
- **고아 프로세스 (Orphan Process)**: 부모 프로세스가 종료된 후에도 남아 있는 자식 프로세스
- **Mutex**: 단일 인스턴스 실행을 보장하기 위해 사용하는 Windows 동기화 객체
- **디바운싱 (Debouncing)**: 짧은 시간 내 중복 이벤트를 하나로 합치는 처리 기법

---

## 요구사항

### 요구사항 1: 공용 라이브러리(AltKey.Core) 구축

**사용자 스토리:** 개발자로서, 두 앱이 동일한 데이터 모델과 유틸리티를 공유할 수 있도록 공용 라이브러리를 구축하고 싶습니다. 그래야 코드 중복 없이 일관된 설정 구조를 유지할 수 있습니다.

#### 인수 기준

1. THE AltKey.Core SHALL `AppConfig`, `WindowConfig`, `HeaderButtonConfig`, `AccessibilityOptions` 등 설정 관련 데이터 모델 클래스를 포함한다.
2. THE AltKey.Core SHALL `PathResolver` 정적 클래스를 포함하며, 포터블 모드(`exe` 옆 `config.json` 존재 여부)와 설치형 모드(`%AppData%\AltKey`) 경로를 모두 지원한다.
3. THE AltKey.Core SHALL `ConfigService`의 파일 읽기·쓰기·변경 이벤트 발행 로직을 포함한다.
4. WHEN AltKey.App 또는 AltKey.Settings가 AltKey.Core를 참조할 때, THE AltKey.Core SHALL 단일 어셈블리(`.dll`)로 빌드되어 두 프로젝트 모두에서 사용 가능해야 한다.
5. THE AltKey.Core SHALL `JsonOptions` 직렬화 설정을 포함하여 두 앱이 동일한 JSON 형식으로 `config.json`을 읽고 쓸 수 있도록 한다.

---

### 요구사항 2: AltKey.Settings 독립 앱 프로젝트 생성

**사용자 스토리:** 사용자로서, 설정 앱이 별도 실행 파일로 분리되어 있어서 가상 키보드로 설정 항목을 입력할 때 한글 자모가 분리되지 않기를 원합니다.

#### 인수 기준

1. THE AltKey.Settings SHALL 독립적인 WPF 프로젝트로 생성되며, `AltKey.Settings.exe`로 빌드된다.
2. THE AltKey.Settings SHALL 기존 `SettingsWindow.xaml`, `SettingsViewModel.cs` 및 관련 뷰(SwitchScanSettingsWindow, FocusA11ySettingsWindow 등)를 이관받아 동작한다.
3. THE AltKey.Settings SHALL AltKey.Core를 프로젝트 참조로 사용하여 `ConfigService`, `PathResolver`, 데이터 모델에 접근한다.
4. WHEN AltKey.Settings가 시작될 때, THE AltKey.Settings SHALL 의존성 주입(DI) 컨테이너를 구성하고 필요한 서비스(`ConfigService`, `ThemeService`, `LayoutService` 등)를 등록한다.
5. THE AltKey.Settings SHALL 단일 파일 배포(`PublishSingleFile`)로 빌드 가능해야 하며, 포터블 환경에서 `AltKey.App.exe`와 동일 디렉터리에 위치할 수 있다.
6. WHEN AltKey.Settings가 시작될 때, THE AltKey.Settings SHALL 현재 테마(`config.json`의 `Theme` 값)를 읽어 즉시 적용한다.

---

### 요구사항 3: AltKey.App에서 설정 앱 실행 및 인스턴스 제어

**사용자 스토리:** 사용자로서, 트레이 메뉴나 상단바 설정 버튼을 클릭했을 때 설정 창이 즉시 열리고, 이미 열려 있으면 중복 실행 없이 기존 창이 앞으로 나오기를 원합니다.

#### 인수 기준

1. WHEN 사용자가 트레이 메뉴의 "설정" 항목 또는 상단바 설정 버튼을 클릭할 때, THE AltKey.App SHALL `Process.Start`를 사용하여 `AltKey.Settings.exe`를 실행한다.
2. WHEN AltKey.Settings가 이미 실행 중인 상태에서 다시 실행 요청이 들어올 때, THE AltKey.Settings SHALL Named Mutex를 통해 중복 실행을 방지하고 기존 창을 포그라운드로 활성화한다.
3. THE AltKey.App SHALL `AltKey.Settings.exe`의 경로를 `PathResolver`를 통해 결정하며, 포터블 모드에서는 `exe` 옆 경로를, 설치형 모드에서는 동일 디렉터리 경로를 사용한다.
4. IF `AltKey.Settings.exe` 파일이 존재하지 않을 때, THEN THE AltKey.App SHALL 사용자에게 파일을 찾을 수 없다는 안내 메시지를 표시하고 실행을 중단한다.
5. WHEN AltKey.Settings가 활성화 요청을 받을 때, THE AltKey.Settings SHALL 창이 최소화 상태라면 복원하고 포그라운드로 이동한다.

---

### 요구사항 4: 프로세스 간 통신(IPC) — 실시간 설정 동기화

**사용자 스토리:** 사용자로서, 설정 앱에서 테마나 창 크기를 변경했을 때 가상 키보드에 즉시 반영되기를 원합니다. 설정 앱을 닫고 다시 열어야 반영되는 방식은 불편합니다.

#### 인수 기준

1. WHEN AltKey.Settings에서 설정 값이 변경될 때, THE AltKey.Settings SHALL Named Pipe를 통해 변경된 속성 이름과 값을 AltKey.App에 전송한다.
2. WHEN AltKey.App이 Named Pipe 메시지를 수신할 때, THE AltKey.App SHALL 해당 설정을 메모리에 즉시 반영하고 UI를 갱신한다.
3. WHILE Named Pipe 연결이 끊어진 상태일 때, THE AltKey.App SHALL `FileSystemWatcher`를 통해 `config.json` 변경을 감지하고 설정을 재로드한다.
4. WHEN `FileSystemWatcher`가 `config.json` 변경 이벤트를 수신할 때, THE AltKey.App SHALL 300ms 디바운싱을 적용하여 중복 이벤트를 하나로 합친 뒤 설정을 재로드한다.
5. THE AltKey.App SHALL Named Pipe 서버를 백그라운드 스레드에서 실행하며, 파이프 연결 실패가 메인 UI 스레드를 차단하지 않도록 한다.
6. WHEN AltKey.App이 Named Pipe 메시지를 수신할 때, THE AltKey.App SHALL 수신된 메시지를 역직렬화하기 전에 최대 길이(4096바이트)를 초과하는지 검증하고, 초과 시 해당 메시지를 무시한다.

---

### 요구사항 5: 원자적 파일 쓰기 및 파일 잠금 방지

**사용자 스토리:** 개발자로서, 두 프로세스가 동시에 `config.json`에 접근할 때 파일이 손상되거나 데이터가 유실되지 않기를 원합니다.

#### 인수 기준

1. WHEN ConfigService가 `config.json`을 저장할 때, THE ConfigService SHALL 임시 파일(`config.tmp`)에 먼저 쓴 뒤 `File.Replace`를 사용하여 원본 파일을 원자적으로 교체한다.
2. WHEN `File.Replace` 호출이 실패할 때, THE ConfigService SHALL 최대 3회 재시도하며, 각 재시도 사이에 300ms 대기한다.
3. WHEN ConfigService가 `config.json`을 읽을 때, THE ConfigService SHALL 파일이 다른 프로세스에 의해 잠겨 있으면 최대 3회 재시도하며, 각 재시도 사이에 100ms 대기한다.
4. IF 모든 재시도 후에도 읽기에 실패할 때, THEN THE ConfigService SHALL 마지막으로 성공적으로 로드된 설정 값을 유지하고 오류를 로그에 기록한다.
5. THE ConfigService SHALL 백업 파일(`config.bak`)을 원자적 교체 전에 생성하여, 교체 실패 시 복구 수단을 제공한다.

---

### 요구사항 6: 고아 프로세스 방지 및 생명주기 동기화

**사용자 스토리:** 사용자로서, AltKey 메인 앱을 종료했을 때 설정 앱도 함께 종료되기를 원합니다. 설정 앱이 혼자 남아 있으면 혼란스럽습니다.

#### 인수 기준

1. WHEN AltKey.App이 정상 종료될 때, THE AltKey.App SHALL Named Pipe를 통해 AltKey.Settings에 종료 시그널(`{"command":"shutdown"}`)을 전송한다.
2. WHEN AltKey.Settings가 종료 시그널을 수신할 때, THE AltKey.Settings SHALL 열려 있는 모든 창을 닫고 프로세스를 종료한다.
3. WHEN AltKey.Settings가 Named Pipe 연결이 끊어진 것을 감지할 때, THE AltKey.Settings SHALL AltKey.App 프로세스가 실행 중인지 확인하고, 실행 중이 아니면 스스로 종료한다.
4. THE AltKey.Settings SHALL 30초마다 AltKey.App 프로세스의 생존 여부를 확인하는 하트비트(heartbeat) 검사를 수행한다.
5. WHEN AltKey.App이 비정상 종료될 때, THE AltKey.Settings SHALL 하트비트 검사 실패를 감지하고 60초 이내에 스스로 종료한다.

---

### 요구사항 7: 시각적 일관성 및 UX 연속성

**사용자 스토리:** 사용자로서, 설정 앱이 별도 프로세스임에도 불구하고 메인 키보드 앱과 시각적으로 일관된 경험을 제공받기를 원합니다. 창이 갑자기 번쩍이거나 엉뚱한 위치에 나타나면 불편합니다.

#### 인수 기준

1. WHEN AltKey.Settings가 처음 표시될 때, THE AltKey.Settings SHALL 페이드인(Fade-in) 애니메이션(지속 시간 150ms, 불투명도 0→1)을 적용하여 창을 표시한다.
2. WHEN AltKey.Settings가 처음 표시될 때, THE AltKey.Settings SHALL AltKey.App 메인 창의 위치를 기준으로 화면 중앙 근처에 배치된다.
3. WHEN AltKey.App이 현재 적용 중인 테마(라이트/다크/고대비/시스템)가 있을 때, THE AltKey.Settings SHALL 시작 시 동일한 테마를 `config.json`에서 읽어 즉시 적용한다.
4. WHEN 이미 실행 중인 AltKey.Settings에 활성화 요청이 들어올 때, THE AltKey.Settings SHALL 창을 포그라운드로 이동시키고 300ms 동안 창 테두리를 강조 표시하여 사용자가 창 위치를 인지할 수 있도록 한다.
5. WHERE `ReducedMotionEnabled` 설정이 활성화된 경우, THE AltKey.Settings SHALL 페이드인 애니메이션 및 강조 표시 애니메이션을 생략하고 즉시 표시한다.

---

### 요구사항 8: 포터블(무설치) 환경 지원

**사용자 스토리:** 사용자로서, USB 드라이브에 AltKey를 넣어 다니는 포터블 환경에서도 설정 앱이 정상적으로 동작하기를 원합니다. 레지스트리나 `%AppData%`에 의존하지 않아야 합니다.

#### 인수 기준

1. WHEN AltKey.Settings가 시작될 때, THE AltKey.Settings SHALL `PathResolver.IsPortable` 값을 확인하여 포터블 모드와 설치형 모드를 자동으로 구분한다.
2. WHERE 포터블 모드인 경우, THE AltKey.Settings SHALL `config.json`, 레이아웃 파일 등 모든 데이터를 `exe` 파일과 동일한 디렉터리에서 읽고 쓴다.
3. WHERE 설치형 모드인 경우, THE AltKey.Settings SHALL 모든 데이터를 `%AppData%\AltKey` 디렉터리에서 읽고 쓴다.
4. THE AltKey.Settings SHALL 레지스트리 접근 없이 동작하며, 자동 실행(RunOnStartup) 설정은 AltKey.App에서만 관리한다.
5. THE AltKey.Settings SHALL 단일 파일 배포(`PublishSingleFile=true`, `SelfContained=false`) 방식으로 빌드되어 별도 런타임 설치 없이 실행 가능하다.

---

### 요구사항 9: 설정 파일 파싱 및 직렬화 무결성

**사용자 스토리:** 개발자로서, 두 앱이 `config.json`을 읽고 쓸 때 항상 동일한 형식을 사용하여 데이터 손실이나 형식 불일치가 발생하지 않기를 원합니다.

#### 인수 기준

1. WHEN AltKey.Core의 `ConfigService`가 `AppConfig` 객체를 직렬화할 때, THE ConfigService SHALL `JsonOptions.Default` 설정을 사용하여 일관된 JSON 형식으로 출력한다.
2. WHEN AltKey.Core의 `ConfigService`가 `config.json`을 역직렬화할 때, THE ConfigService SHALL 알 수 없는 JSON 속성을 무시하고 누락된 속성에는 기본값을 적용한다.
3. FOR ALL 유효한 `AppConfig` 객체에 대해, 직렬화 후 역직렬화한 결과는 원본 객체와 동등해야 한다 (라운드트립 속성).
4. WHEN `config.json`이 손상되어 역직렬화에 실패할 때, THE ConfigService SHALL 기본값 `AppConfig` 객체를 사용하고 오류를 로그에 기록한다.
5. THE ConfigService SHALL 이전 버전 형식(`Width`/`Height` 기반 `WindowConfig`)을 최신 형식(`Scale` 기반)으로 자동 마이그레이션한다.

---

### 요구사항 10: 접근성 유지

**사용자 스토리:** 접근성 보조 기기를 사용하는 사용자로서, 설정 앱이 독립 프로세스로 분리된 후에도 스위치 스캔, TTS, 포커스 탐색 등 모든 접근성 기능이 정상적으로 동작하기를 원합니다.

#### 인수 기준

1. THE AltKey.Settings SHALL 기존 `SettingsWindow`의 모든 `AutomationProperties`(이름, 도움말 텍스트) 속성을 유지하여 스크린 리더와의 호환성을 보장한다.
2. THE AltKey.Settings SHALL 탭 순서(`KeyboardNavigation.TabIndex`)를 기존과 동일하게 유지하여 키보드 탐색이 가능하도록 한다.
3. WHEN AltKey.Settings가 표시될 때, THE AltKey.Settings SHALL `WS_EX_NOACTIVATE` 스타일을 적용하지 않아 포커스를 정상적으로 받을 수 있도록 한다 (설정 창은 포커스를 받아야 함).
4. WHERE `TtsEnabled` 설정이 활성화된 경우, THE AltKey.Settings SHALL 설정 항목 변경 시 변경된 값을 TTS로 읽어준다.
5. WHEN AltKey.Settings에서 `SwitchScanEnabled` 또는 `KeyboardA11yNavigationEnabled` 설정이 변경될 때, THE AltKey.Settings SHALL Named Pipe를 통해 AltKey.App에 즉시 전달하여 접근성 모드가 실시간으로 전환되도록 한다.
