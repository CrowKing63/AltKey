<div align="center">

# AltKey

**한국어 사용자 전용 커스터마이징 화상 키보드**

Windows 환경에서 한국어 입력을 지원하는 경량·무설치 가상 키보드. macOS 손쉬운 사용 키보드에서 영감을 받아 Windows 생태계에 맞게 재설계되었다.

[![Release](https://img.shields.io/github/v/release/CrowKing63/AltKey?style=flat-square&color=2563EB)](https://github.com/CrowKing63/AltKey/releases/latest)
[![License](https://img.shields.io/github/license/CrowKing63/AltKey?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D4?style=flat-square)](https://www.microsoft.com/windows)

[설치](#설치) · [기능](#기능) · [스크린샷](#스크린샷) · [레이아웃 커스텀](#레이아웃-커스텀)

</div>

---

## 개요

AltKey는 마우스나 터치 입력만으로 물리적 키보드를 완전히 대체할 수 있는 화상 키보드입니다.
물리적 키보드가 없거나 특정 키 입력이 어려운 상황에서도 빠르고 유연한 텍스트 입력이 가능합니다.

### 핵심 철학

| | |
|---|---|
| **가벼움** | 단일 실행 파일, 대기 시 약 200MB 메모리 |
| **포터블** | 설치 불필요; 설정은 로컬 JSON 파일로 관리 |
| **아름다움** | Windows 11 Acrylic 블러 효과, 부드러운 애니메이션 |
| **확장성** | JSON 기반 레이아웃 에디터, 앱별 레이아웃 프로필 |

---

## 기능

### 입력

- **SendInput API** — virtually all applications과 호환되는 정밀한 키 전송
- **Sticky Keys** — Shift·Ctrl·Alt·Win을 한 번 탭하면 일회성 고정, 두 번 탭하면 영구 잠금, 세 번 탭하면 해제
- **체류 클릭** — 키 위에 마우스를 올려놓으면 설정한 시간 후 자동 클릭 (손떨림이나 운동 장애가 있는 사용자 지원)
- **직접 유니코드 입력** — 이모지를 포함한 모든 유니코드 문자 지원

### 레이아웃

- **QWERTY 한국어** 기본 포함 (영어 서브모드 "가/A" 토글)
- **JSON 레이아웃 에디터** — GUI에서 키 배치와 액션 직접 편집
- **앱별 레이아웃 프로필** — 특정 앱이 포그라운드 될 때 자동으로 레이아웃 전환
- **인스턴트 레이아웃 전환** — 헤더 드롭다운으로 한 번의 클릭으로 레이아웃 변경

### UX/UI

- **Windows 11 Acrylic 블러** 배경 (Windows 10 22H2+ 지원)
- **다크/라이트/시스템 테마** 자동 적용
- **자동 투명도** — 미사용 시 반투명, 호버 시 불투명
- **화면 가장자리 이동 버튼** — 헤더의 ← ↑ ↓ → 버튼으로 키보드를 화면 가장자리에 즉시 이동
- **자유 크기 조절** — 종횡비 고정 드래그 핸들로 크기 조절
- **접기/펼치기** — 키보드 본체를 숨기고 헤더만 표시
- **트레이 아이콘** — 시스템 트레이로 최소화; 전역 단축키로 표시 전환

### 확장 액션

각 키에는 다양한 액션을 할당할 수 있습니다:

| 액션 유형 | 설명 |
|---|---|
| `SendKey` | 가상 키코드 단일 전송 |
| `SendCombo` | 키 조합 전송 (예: Ctrl+C) |
| `ToggleSticky` | 수식자 키 Sticky 상태 토글 |
| `SwitchLayout` | 다른 레이아웃으로 전환 |
| `RunApp` | 앱 실행 (경로 + 인수) |
| `Boilerplate` | 직접 유니코드 입력으로 상용구 텍스트 입력 |
| `ShellCommand` | CMD / PowerShell 명령 실행 |
| `VolumeControl` | 볼륨 올리기·내리기·음소거 |
| `ClipboardPaste` | 텍스트를 클립보드에 복사 후 붙여넣기 |

### 추가 기능

- **키 클릭 음향** — WAV 파일 교체 가능
- **이모지 패널** — 자주 쓰는 이모지 빠른 접근
- **클립보드 히스토리 패널** — 최근 복사한 항목 확인 및 붙여넣기
- **유니코드 기반 한국어 자동완성** — 빈도 기반 한국어 단어 추천 (선택적)
- **자동 업데이트 알림** — GitHub Releases 연동, 새 버전 배너 안내
- **Windows 시작 시 실행** — 레지스트리로 시작 프로그램 등록/해제
- **관리자 권한으로 재시작** — elevated 권한이 필요한 사용 사례로
- **OS IME 한글 버튼** — 상단 바 버튼으로 시스템 IME 한글/영어 전환

---

## 스크린샷

> 스크린샷 준비 중입니다.

---

## 설치

### 설치 프로그램 (권장)

1. [latest release](https://github.com/CrowKing63/AltKey/releases/latest)에서 `AltKey-Setup-x.y.z.exe` 다운로드
2. 설치 프로그램 실행

### 포터블

1. [latest release](https://github.com/CrowKing63/AltKey/releases/latest)에서 `AltKey-Portable-x.y.z.zip` 다운로드
2. 원하는 폴더에 압축 해제
3. `AltKey.exe` 실행

### 사전 요구사항

- **Windows 10 22H2 이상** (Acrylic 블러 권장)
- 별도 .NET 런타임 불필요 (self-contained 배포)

---

## 레이아웃 커스텀

레이아웃은 `layouts/` 폴더의 JSON 파일로 정의됩니다.

```jsonc
{
  "name": "나만의 레이아웃",
  "language": "ko",
  "rows": [
    {
      "keys": [
        {
          "label": "📋",
          "action": { "type": "ClipboardPaste", "text": "자주 쓰는 문구" },
          "width": 2.0
        },
        {
          "label": "메모장",
          "action": { "type": "RunApp", "path": "notepad.exe" },
          "width": 2.0
        }
      ]
    }
  ]
}
```

**`width`** — 키 너비 (1.0 = 표준 키 너비 하나)  
**`label`** — 키에 표시되는 텍스트 (이모지 및 유니코드 지원)  
**`shift_label`** — Shift 활성화 시 표시되는 레이블  
**`hangul_label`** / **`hangul_shift_label`** — 한글 입력 모드의 Secondary 레이블

GUI 에디터는 설정 → **레이아웃 에디터**에서 열 수 있습니다.

---

## 개발 환경 설정

### 요구사항

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 이상
- Visual Studio 2022 또는 Rider (권장), 또는 VS Code + C# Dev Kit

### 빌드 및 실행

```bash
# 레포지토리克隆
git clone https://github.com/CrowKing63/AltKey.git
cd AltKey/AltKey

# 실행 (개발 빌드)
dotnet run

# 릴리스 빌드
dotnet build AltKey/AltKey.csproj -c Release

# 단일 self-contained 실행 파일로 배포
dotnet publish AltKey/AltKey.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

### 자동화된 릴리스 (GitHub Actions)

`AssemblyVersion`을 설정한 후 버전 태그를 푸시하면 — GitHub Actions이 자동으로 빌드, 패키징, 릴리스를 생성합니다.

```bash
git tag v1.2.3
git push origin --tags
```

```powershell
# 1. 패치 버전 업데이트 (0.1.6 -> 0.1.7)
./scripts/release.ps1 -VersionType patch

# 2. 마이너 버전 업데이트 (0.1.6 -> 0.2.0)
./scripts/release.ps1 -VersionType minor

# 3. 특정 버전 지정
./scripts/release.ps1 -CustomVersion "1.0.0"
'''

---

## 프로젝트 구조

```text
AltKey/
├── App.xaml / App.xaml.cs          # 앱 진입점, DI 컨테이너
├── MainWindow.xaml / .cs           # 메인 창 (투명, NoActivate, Acrylic)
│
├── Views/                          # XAML 뷰
│   ├── KeyboardView.xaml           # 키보드 UI
│   ├── SettingsView.xaml           # 설정 패널
│   ├── LayoutEditorWindow.xaml    # 레이아웃 에디터
│   ├── ActionBuilderView.xaml      # 액션 빌더
│   ├── EmojiPanel.xaml             # 이모지 패널
│   ├── ClipboardPanel.xaml        # 클립보드 히스토리 패널
│   └── SuggestionBar.xaml        # 자동완성 추천 바
│
├── ViewModels/                    # MVVM 뷰모델 (CommunityToolkit.Mvvm)
├── Models/                        # 데이터 모델 (레이아웃 & 설정 구조체)
├── Services/                     # 핵심 서비스
│   ├── InputService.cs           # SendInput 래퍼, Sticky Keys, 액션 디스패처
│   ├── LayoutService.cs           # JSON 레이아웃 로딩, 세이브, 캐싱
│   ├── ConfigService.cs          # 설정 JSON 읽기/쓰기
│   ├── ProfileService.cs          # WinEventHook 포그라운드 앱 감지
│   ├── ThemeService.cs           # 다크/라이트/시스템 테마 적용
│   ├── HotkeyService.cs          # 전역 단축키 등록
│   ├── StartupService.cs          # 시작 프로그램 레지스트리 관리
│   ├── SoundService.cs           # 키 클릭 음향 재생
│   ├── AutoCompleteService.cs    # 단어 자동완성
│   └── UpdateService.cs         # GitHub Releases 업데이트 확인
├── Controls/                      # 커스텀 컨트롤 (KeyButton 등)
├── Platform/                     # Win32 P/Invoke 선언
├── Themes/                       # WPF 리소스 딕셔너리 (색상, 스타일)
└── layouts/                       # 기본 레이아웃 JSON 파일
```

---

## 기술 스택

| 레이어 | 기술 |
|---|---|
| 언어 | C# 12, .NET 8 |
| UI | WPF + WPF-UI (lepoco/wpfui) |
| MVVM | CommunityToolkit.Mvvm |
| JSON | System.Text.Json |
| Win32 | P/Invoke (직접 선언) |
| 배포 | .NET 8 Single-file self-contained |

---

## 알려진 제한사항

| 증상 | 원인 | 참고 |
|---|---|---|
| 키 입력이 elevated 앱에 전송 안 됨 | SendInput 권한 수준 불일치 | 설정의 "관리자 권한으로 재시작" 사용 |
| 독점 전체 화면 게임에서 창이 가려짐 | DirectX 독점 모드 | 창 모드 게임에서 사용 |
| 구버전 Windows에서 Acrylic 블러 안 됨 | Windows 10 22H2 미만 | 우아하게 단색으로 대체 |

---

## 라이선스

[MIT License](LICENSE) © 2025 CrowKing63