<div align="center">

<img src="AltKey/Assets/icon.png" width="180" alt="AltKey Icon">

# AltKey

**한국어 사용자 전용 커스터마이징 화상 키보드**

Windows 환경에서 한국어 입력을 최적화하여 지원하는 경량 화상 키보드입니다. macOS 손쉬운 사용 키보드에서 영감을 받아 Windows 생태계와 한국어 입력 특성, 그리고 접근성 요구에 맞게 다시 설계했습니다.

[![Release](https://img.shields.io/github/v/release/CrowKing63/AltKey?style=flat-square&color=2563EB)](https://github.com/CrowKing63/AltKey/releases/latest)
[![License](https://img.shields.io/github/license/CrowKing63/AltKey?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)](https://dotnet.microsoft.com)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2B-0078D4?style=flat-square)](https://www.microsoft.com/windows)

[설치](#설치) · [기능](#기능) · [최근 반영](#최근-반영) · [커스터마이징](#커스터마이징) · [매뉴얼](https://github.com/CrowKing63/AltKey/wiki)

</div>

---

## 개요

AltKey는 마우스, 터치, 외부 스위치만으로도 물리 키보드를 대체할 수 있도록 만든 화상 키보드입니다. 한국어 조합 엔진을 앱 내부에 직접 포함하고 있어, 윈도우 한/영 상태가 흔들리기 쉬운 환경에서도 더 안정적으로 입력할 수 있습니다.

### 핵심 철학

| | |
|---|---|
| **한국어 최적화** | `KoreanInputModule` 기반 한글 조합, QuietEnglish(`가/A`) 서브모드, 한국어 중심 자동완성 |
| **접근성 우선** | TTS, 체류 클릭, 스위치 스캔, Sticky Keys, 키보드 탐색 기능 제공 |
| **실사용 중심** | 자동완성, 상용구, 앱별 레이아웃, 클립보드/이모지 패널로 실제 입력 부담 감소 |
| **안전한 커스터마이징** | 레이아웃/사전/프로필/AI 프롬프트를 독립 편집 도구에서 수정하고 즉시 반영 |

---

## 최근 반영

- **AltKey.Tools 분리**: 레이아웃 편집기, 사용자 단어 편집기, 프로필 매핑 편집기, AI 프롬프트 편집기가 메인 앱과 분리된 독립 도구로 동작합니다.
- **사용자 사전 관리 강화**: 학습된 단어뿐 아니라 자동완성 문맥에 쓰이는 데이터 정리 흐름을 더 안정적으로 다듬었습니다.
- **앱별 레이아웃 편집 개선**: 프로세스 이름과 레이아웃 매핑을 별도 편집 창에서 검증하며 수정할 수 있고, 저장 후 메인 앱에 즉시 반영됩니다.
- **AI 텍스트 처리 추가**: 상단바 버튼과 키 액션으로 AI 텍스트 처리 기능을 실행할 수 있고, 긴 한글 프롬프트는 독립 편집기에서 다룰 수 있습니다.
- **상단바 버튼 사용자화**: 자동완성(AC), 이모지, 클립보드, 설정, AI 버튼의 표시 여부와 좌우 배치를 사용자가 직접 조정할 수 있습니다.

---

## 기능

### 입력 및 조합

- **KoreanInputModule**: 유니코드 기반 한글 조합 엔진으로 정확한 입력 처리
- **QuietEnglish 서브모드**: 별도 영어 레이아웃 없이 한국어 레이아웃 안에서 `가/A` 토글로 영문 입력
- **Sticky Keys**: Shift·Ctrl·Alt·Win을 일회성 고정 또는 잠금 상태로 사용
- **직접 유니코드 입력**: 이모지, 특수문자, 상용구도 안정적으로 입력

### 접근성

- **TTS**: 키 이름과 상태를 음성으로 안내
- **체류 클릭**: 클릭 없이 커서를 머무르게 해 자동 입력
- **스위치 스캔**: 외부 스위치나 지정 키로 순차 탐색
- **키보드 내비게이션**: Tab/Enter/Space 등으로 가상 키보드 조작

### 지능형 입력 보조

- **빈도·빅그램 기반 자동완성**: 자주 쓰는 단어와 다음 단어 문맥을 학습
- **사용자 단어 편집기**: 학습된 단어를 직접 보고 정리
- **AI 텍스트 처리**: 선택 텍스트를 요약·정리·변환하는 AI 기능
- **이모지 & 클립보드 패널**: 반복 입력을 줄이는 빠른 패널

### UX/UI 및 관리

- **상단바 버튼 사용자화**: 자주 쓰는 도구 버튼만 남기고 순서 재배치
- **앱별 레이아웃 프로필**: 포그라운드 앱에 따라 레이아웃 자동 전환
- **독립 편집 도구(AltKey.Tools)**: 편집 작업을 메인 입력 창과 분리해 안정성 확보
- **자동 투명도 / 화면 가장자리 정렬** 지원

---

## 스크린샷

![AltKey Screenshot](docs/assets/AltKey00.png)

---

## 설치

### 설치 프로그램 (권장)

1. [latest release](https://github.com/CrowKing63/AltKey/releases/latest)에서 `AltKey-Setup-x.y.z.exe` 다운로드
2. 설치 프로그램을 실행해 안내에 따라 설치
3. 설치 버전에는 메인 앱과 함께 **`AltKey.Tools` 편집 도구**도 포함됩니다

### 포터블 (Portable)

1. [latest release](https://github.com/CrowKing63/AltKey/releases/latest)에서 `AltKey-Portable-x.y.z.zip` 다운로드
2. 원하는 위치에 압축 해제
3. `AltKey.exe`를 실행하면 같은 폴더 구조 안의 `Tools` 하위 편집 도구를 함께 사용할 수 있습니다

---

## 커스터마이징

### 독립 편집 도구

설정 창에서 다음 편집기를 각각 열 수 있습니다.

- **레이아웃 편집기**: 키 배치와 액션 수정
- **사용자 단어 편집기**: 자동완성 학습 단어 확인·정리
- **프로필 매핑 편집기**: 앱별 레이아웃 자동 전환 규칙 편집
- **AI 프롬프트 편집기**: 긴 한글 프롬프트를 별도 창에서 안정적으로 수정

편집기에서 저장하면 메인 앱이 재시작 없이 다시 읽어 반영하도록 설계되어 있습니다.

### 레이아웃 예시

레이아웃은 `layouts/` 폴더의 JSON 파일로 정의됩니다.

```jsonc
{
  "name": "나만의 레이아웃",
  "columns": [
    {
      "rows": [
        {
          "keys": [
            {
              "label": "📋 상용구",
              "action": { "type": "ClipboardPaste", "text": "자주 쓰는 문구" },
              "width": 2.0
            },
            {
              "label": "AI 요약",
              "action": { "type": "Ai", "prompt": "다음 문장을 짧고 공손한 한국어로 정리해줘." },
              "width": 2.0
            }
          ]
        }
      ]
    }
  ]
}
```

- **`width`**: 표준 키 대비 상대 너비
- **`label` / `shift_label`**: 기본 및 Shift 표시 텍스트
- **`english_label` / `english_shift_label`**: QuietEnglish 모드에서 보일 텍스트

---

## 프로젝트 구조

```text
AltKey/
├── AltKey/                          # 메인 앱
│   ├── App.xaml / .cs               # 앱 진입점, DI 및 서비스 초기화
│   ├── MainWindow.xaml / .cs        # 메인 윈도우와 런타임 제어
│   ├── Views/                       # 키보드, 설정, 패널 UI
│   ├── ViewModels/                  # MVVM 바인딩 로직
│   ├── Models/                      # AppConfig, Layout, KeyAction 등
│   ├── Services/
│   │   ├── InputLanguage/           # KoreanInputModule, InputSubmode
│   │   ├── AutoCompleteService.cs   # 자동완성 및 문맥 제안
│   │   ├── ToolsReloadSignalService.cs # AltKey.Tools와의 재로드 신호
│   │   ├── LayoutRepository.cs      # 레이아웃 저장소 경계
│   │   └── UserDictionaryRepository.cs # 사용자 사전 저장소 경계
│   └── Controls/                    # KeyButton, NumericAdjuster
├── AltKey.Tools/                    # 독립 편집 도구 앱
│   ├── MainWindow.xaml / .cs        # 도구 시작 화면 및 직접 진입 인자 처리
│   ├── ProfileMappingEditorWindow.* # 프로필 매핑 편집기
│   └── AiPromptEditorWindow.*       # AI 프롬프트 편집기
├── Wiki/AltKey.wiki/                # 사용자용 매뉴얼
├── docs/altkey-tools/               # 도구 분리 설계/유지보수 문서
└── layouts/                         # 기본 및 사용자 레이아웃
```

---

## 기술 스택

| 분류 | 기술 |
|---|---|
| **언어 및 런타임** | C# 12, .NET 8 |
| **UI 프레임워크** | WPF |
| **아키텍처** | MVVM (CommunityToolkit.Mvvm) |
| **입력 기술** | Win32 SendInput, Low-level Keyboard Hooks |
| **보안 저장** | Windows DPAPI |

---

## 라이선스

[MIT License](LICENSE) © 2025-2026 CrowKing63
