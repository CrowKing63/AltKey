# AltKey - 프로젝트 블루프린트

> Windows용 커스텀 화상 키보드 · macOS 손쉬운 사용 키보드를 벤치마크한 고급 대체 입력 도구

## 1. 프로젝트 비전

AltKey는 Windows 환경에서 동작하는 **커스터마이징 가능한 화상 키보드**다.
macOS의 손쉬운 사용 키보드(Accessibility Keyboard)를 벤치마크하되,
Windows 생태계에 맞는 기능과 "가볍고, 무설치이며, 애플급 미학"을 목표로 한다.

### 핵심 원칙

- **가볍다** — 단일 실행 파일, 메모리 점유 최소화 (~20MB 유휴 목표)
- **무설치(Portable)** — 레지스트리 미사용, 설정은 로컬 JSON 파일
- **아름답다** — Windows 11 Acrylic/Mica 효과, 부드러운 애니메이션
- **확장 가능하다** — JSON 기반 레이아웃, 앱별 프로필, WPF 리소스 딕셔너리 테마

---

## 2. 기술 스택

| 계층 | 기술 | 선택 이유 |
|------|------|-----------|
| **언어** | C# 12, .NET 8 | Windows 네이티브 생태계, P/Invoke 편의성 |
| **UI 프레임워크** | WPF (Windows Presentation Foundation) | 투명 창, 포커스 제어, Win32 연동 사례 풍부 |
| **UI 라이브러리** | WPF-UI (lepoco/wpfui) | Acrylic/Mica 블러 효과, 모던 컨트롤 무료 제공 |
| **MVVM** | CommunityToolkit.Mvvm | Source generator 기반, 보일러플레이트 최소화 |
| **JSON** | System.Text.Json (내장) | 추가 의존성 없음 |
| **Win32 바인딩** | P/Invoke (직접 선언) | windows-rs 없이 C#에서 직접 Win32 API 호출 |
| **배포** | .NET 8 Single-file self-contained publish | 런타임 미설치 단일 .exe |

### 스택 선택 배경

Tauri + Rust + Svelte 조합도 검토했으나, **Windows 전용** 화상 키보드라는 용도에서 다음 이유로 WPF를 선택했다.

- `WS_EX_NOACTIVATE` 포커스 비침해 패턴이 WPF + P/Invoke로 이미 수백 개의 레퍼런스 존재
- Tauri는 WebView2(~80MB) 프로세스로 인해 진정한 경량화 불가
- WPF 기반 화상 키보드에서 SendInput 지연이 IPC 없이 직접 호출이므로 더 낮음
- `.NET 8 single-file publish`로 포터블 배포 완전 지원

### 알려진 제약사항 (설계 시 반영)

| 제약 | 원인 | 대응 전략 |
|------|------|-----------|
| **관리자 앱에 SendInput 무시됨** | 권한 레벨 불일치 | v1 스코프 외 명시. 선택적 "관리자 모드 실행" 옵션 제공 |
| **전체화면 독점 게임 위 WS_EX_TOPMOST 무력화** | DirectX exclusive mode | 안내 문구 표시 |
| **Acrylic blur, Win10 구버전 미지원** | SetWindowCompositionAttribute 미지원 | Win10 22H2+ / Win11 기준, 구버전은 단색 폴백 |
| **한글 입력 시 IME ↔ SendInput 충돌** | WPF IME 처리와 경합 | IME 전환 키는 별도 경로(keybd_event) 로 처리 |
| **WinEventHook ↔ 일부 보안SW 충돌** | EDR/백신 훅 감시 | 앱 전환 감지 실패 시 조용히 무시, 로그만 기록 |
| **DPI 스케일링** | 고DPI 모니터 | dpiAwareness 매니페스트 설정 + VisualTreeHelper 스케일 보정 |

---

## 3. 아키텍처 개요

```
┌─────────────────────────────────────────────┐
│               AltKey (.NET 8 WPF)            │
├──────────────┬──────────────────────────────┤
│  XAML Views  │        ViewModels            │
│  (WPF-UI)    │  (CommunityToolkit.Mvvm)     │
├──────────────┴──────────────────────────────┤
│                  Services                    │
│  ┌────────────┐ ┌───────────┐ ┌──────────┐ │
│  │InputService│ │WindowSvc  │ │LayoutSvc │ │
│  │(SendInput) │ │(HWND 조작)│ │(JSON)    │ │
│  └────────────┘ └───────────┘ └──────────┘ │
│  ┌────────────┐ ┌───────────┐ ┌──────────┐ │
│  │ConfigSvc   │ │ProfileSvc │ │TraySvc   │ │
│  │(JSON R/W)  │ │(WinEvent) │ │(NotifyIcon)│ │
│  └────────────┘ └───────────┘ └──────────┘ │
├─────────────────────────────────────────────┤
│           Platform/Win32.cs                  │
│  (P/Invoke: SendInput, SetWindowLongW,      │
│   WinEventHook, SetWindowCompositionAttr)   │
├─────────────────────────────────────────────┤
│             Windows OS (Win32 API)           │
└─────────────────────────────────────────────┘
```

### 프로젝트 디렉토리 구조

```
AltKey/
├── AltKey.csproj
├── App.xaml / App.xaml.cs          # 앱 엔트리포인트, DI 컨테이너
├── app.manifest                    # dpiAwareness, UAC 설정
├── MainWindow.xaml / .cs           # 메인 창 (투명, NoActivate)
│
├── Views/
│   ├── KeyboardView.xaml           # 키보드 전체 UI
│   └── SettingsView.xaml           # 설정 패널 오버레이
│
├── ViewModels/
│   ├── MainViewModel.cs            # 창 상태, 테마, 레이아웃 전환
│   ├── KeyboardViewModel.cs        # 키 렌더링 데이터, Sticky 상태
│   └── SettingsViewModel.cs        # 설정 값 바인딩
│
├── Models/
│   ├── LayoutConfig.cs             # JSON 레이아웃 구조체
│   ├── KeySlot.cs                  # 개별 키 데이터
│   ├── KeyAction.cs                # 키 동작 열거형
│   ├── VirtualKeyCode.cs           # VK 코드 매핑
│   └── AppConfig.cs                # 설정 파일 구조체
│
├── Services/
│   ├── InputService.cs             # SendInput 래퍼, Sticky Keys 상태 관리
│   ├── WindowService.cs            # HWND 조작 (NoActivate, TopMost, 투명도)
│   ├── LayoutService.cs            # JSON 레이아웃 로드/저장
│   ├── ConfigService.cs            # config.json CRUD
│   ├── ProfileService.cs           # WinEventHook, 앱별 프로필 전환
│   └── TrayService.cs              # NotifyIcon, 트레이 메뉴
│
├── Controls/
│   └── KeyButton.xaml / .cs        # 커스텀 키 컨트롤 (DwellClick 포함)
│
├── Platform/
│   └── Win32.cs                    # P/Invoke 선언부 (SendInput, HWND 등)
│
├── Themes/
│   ├── Generic.xaml                # 기본 컨트롤 스타일
│   ├── LightTheme.xaml             # 라이트 테마 리소스
│   ├── DarkTheme.xaml              # 다크 테마 리소스
│   └── Converters.cs               # IValueConverter 모음
│
├── layouts/
│   ├── qwerty-ko.json
│   └── qwerty-en.json
│
└── config.json                     # 사용자 설정 (포터블 모드 시 exe 옆)
```

---

## 4. 핵심 기능 명세

### 4.1 기본 화상 키보드
- 표준 QWERTY 레이아웃 렌더링 (JSON 기반)
- 각 키 클릭 시 `SendInput`으로 타겟 앱에 키 입력 전달
- Shift / Caps Lock 상태에 따른 키 라벨 동적 변경

### 4.2 포커스 비침해 (No-Focus Window)
- `WS_EX_NOACTIVATE` + `WS_EX_TOOLWINDOW` 스타일 적용
- WPF에서는 `Window.ShowActivated = false` + P/Invoke 조합
- 태스크바 미표시

### 4.3 항상 위 (Always on Top)
- `Topmost="True"` (WPF 빌트인) 또는 `SetWindowPos(HWND_TOPMOST)`
- 사용자 토글 가능

### 4.4 Acrylic/Mica 블러 배경
- WPF-UI의 `WindowBackdropType.Acrylic` (Win10 22H2+)
- 미지원 환경: 반투명 단색 폴백
- 투명도 수준 설정 가능

### 4.5 자동 페이딩
- 마우스가 창 밖으로 나간 후 N초 경과 시 `Window.Opacity` 감소
- 마우스 재진입 시 즉시 복귀
- `DispatcherTimer` + `MouseEnter` / `MouseLeave` 이벤트

### 4.6 JSON 기반 커스텀 레이아웃
```json
{
  "name": "기본 QWERTY",
  "rows": [
    {
      "keys": [
        { "label": "Q", "shift_label": null, "action": { "SendKey": "VK_Q" }, "width": 1.0 }
      ]
    }
  ]
}
```

### 4.7 고정 키 (Sticky Keys)
- 수식자 키 단일 클릭 → 일회성 고정
- 더블클릭 → 영구 잠금
- UI 인디케이터 (닷 + 잠금 아이콘)

### 4.8 체류 클릭 (Dwell Click)
- 마우스 키 위 N ms 유지 → 자동 클릭
- 프로그레스 링 애니메이션 피드백

### 4.9 앱별 프로필 자동 전환
- `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` 로 포그라운드 앱 감지
- `config.json`의 `profiles` 매핑 → 레이아웃 자동 전환

### 4.10 시스템 트레이
- `System.Windows.Forms.NotifyIcon` (WPF에서 사용)
- 우클릭 컨텍스트 메뉴: 보이기/숨기기, 레이아웃 전환, 설정, 종료

### 4.11 전역 단축키
- `RegisterHotKey` Win32 API 로 전역 등록
- 기본: Ctrl+Alt+K, 사용자 변경 가능

### 4.12 테마 시스템
- WPF 리소스 딕셔너리로 라이트/다크 정의
- Windows 시스템 테마 자동 감지 (`SystemParameters`)
- 테마 전환 시 `Application.Current.Resources.MergedDictionaries` 교체

---

## 5. 설정 파일 구조

```
altkey/                     # 실행 파일과 같은 폴더 (포터블)
├── AltKey.exe
├── config.json
├── layouts/
│   ├── qwerty-ko.json
│   └── qwerty-en.json
└── themes/                 # 사용자 커스텀 리소스 딕셔너리 (옵션)
```

### config.json 예시

```json
{
  "version": "1.0.0",
  "language": "ko",
  "default_layout": "qwerty-ko",
  "always_on_top": true,
  "opacity_idle": 0.4,
  "opacity_active": 1.0,
  "fade_delay_ms": 5000,
  "dwell_enabled": false,
  "dwell_time_ms": 800,
  "sticky_keys_enabled": true,
  "theme": "system",
  "global_hotkey": "Ctrl+Alt+K",
  "auto_profile_switch": true,
  "profiles": {
    "photoshop.exe": "photoshop",
    "Code.exe": "vscode"
  },
  "window": {
    "left": 100,
    "top": 700,
    "width": 900,
    "height": 320
  }
}
```

---

## 6. 개발 단계 (Phase)

| Phase | 이름 | 산출물 |
|-------|------|--------|
| **0** | 프로젝트 초기화 | 빌드 가능한 WPF 창 |
| **1** | 핵심 윈도우 관리 | NoActivate + TopMost + Acrylic + 드래그 |
| **2** | 입력 엔진 | 키 클릭 → 타겟 앱 입력, Sticky Keys |
| **3** | 레이아웃 시스템 | JSON 파싱 + 동적 렌더링 + 레이아웃 전환 |
| **4** | UI/UX | 테마, 애니메이션, 피드백 |
| **5** | 고급 기능 | 체류 클릭, 앱 프로필, 트레이, 전역 단축키 |
| **6** | 배포 / 최적화 | 단일 exe 포터블 빌드, CI/CD |

---

## 7. 참고 자료

- [WPF-UI 라이브러리](https://github.com/lepoco/wpfui) — Acrylic/Mica 효과
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — MVVM 보일러플레이트
- [SendInput MSDN](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
- [Virtual-Key Codes](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes)
- [SetWindowCompositionAttribute (비공식)](https://github.com/riverar/sample-win32-acrylicblur) — Acrylic P/Invoke 레퍼런스
- [OptiKey](https://github.com/OptiKey/OptiKey) — C# 화상 키보드 오픈소스 레퍼런스

---

## 8. 태스크 파일 인덱스

| 파일 | Phase | 난이도 | 태스크 수 |
|------|-------|--------|----------|
| [TASKS-00-setup.md](./TASKS-00-setup.md) | 0 - 프로젝트 초기화 | ★☆☆ | 8 |
| [TASKS-01-window.md](./TASKS-01-window.md) | 1 - 핵심 윈도우 관리 | ★☆☆ | 10 |
| [TASKS-02-input.md](./TASKS-02-input.md) | 2 - 입력 엔진 | ★☆☆ | 11 |
| [TASKS-03-layout.md](./TASKS-03-layout.md) | 3 - 레이아웃 시스템 | ★☆☆ | 11 |
| [TASKS-04-ui.md](./TASKS-04-ui.md) | 4 - UI/UX 디자인 | ★☆☆ | 10 |
| [TASKS-05-features.md](./TASKS-05-features.md) | 5 - 고급 기능 | ★★☆ | 12 |
| [TASKS-06-distribution.md](./TASKS-06-distribution.md) | 6 - 배포 및 최적화 | ★★☆ | 8 |
| [TASKS-07-bugfixes.md](./TASKS-07-bugfixes.md) | 7 - 버그 수정 | ★☆☆ | 7 |
| [TASKS-08-improvements.md](./TASKS-08-improvements.md) | 8 - UX 개선 | ★★☆ | 5 |
| [TASKS-09-advanced.md](./TASKS-09-advanced.md) | 9 - 고급 기능 | ★★★ | 5 |
