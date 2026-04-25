# Wiki 매뉴얼 작성 메타 지시서

> 이 문서는 AI 에이전트에게 **Wiki 매뉴얼을 단계적으로 작성**하도록 지시하기 위한 메타 문서다.
> 에이전트는 이 문서만 읽고 **독립적으로** 매뉴얼 항목을 작성할 수 있어야 한다.

---

## 1. 원칙

1. **한국어로 작성** — 모든 Wiki 페이지는 한국어.
2. **사용자는 코딩 전문가가 아님** — 기술 용어는 최소화, 필요하면 간단히 설명.
3. **접근성 중심** — 모든 설명은 '이것이 접근성 향상에 어떻게 기여하는가'를 관점으로.
4. **독립 분업** — 각 에이전트는 자신에게 할당된 항목만 담당. 다른 항목은 건드리지 않음.
5. **진행 상태 추적** — 이 문서의 §4 체크리스트가 단일 진실 공급원(SSOT).

---

## 2. Wiki 디렉토리 구조

```
Wiki/AltKey.wiki/
├── Home.md                    # 위키 홈 (목차 + 전체 네비게이션)
├── 소개.md                    # ✅ 작성 완료
├── 시작하기.md                # ✅ 작성 완료
├── 화상키보드-기본.md          # ✅ 작성 완료 (Update됨)
├── 한국어-입력.md             # ✅ 작성 완료 (Update됨)
├── 자동완성.md                # ✅ 작성 완료 (Update됨)
├── 수식자-고정.md             # ✅ 작성 완료
├── 체류클릭.md                # ✅ 작성 완료
├── 키-반복.md                 # ✅ 작성 완료
├── 레이아웃-커스터마이징.md    # ✅ 작성 완료 (Update됨)
├── 앱별-레이아웃.md           # ✅ 작성 완료
├── 액션-키.md                 # ✅ 작성 완료
├── 이모지.md                  # ✅ 작성 완료
├── 클립보드-히스토리.md       # ✅ 작성 완료
├── 설정.md                    # ✅ 작성 완료 (Update됨)
├── 테마와-외관.md             # ✅ 작성 완료 (Update됨)
├── 시스템-트레이.md           # ✅ 작성 완료 (Update됨)
├── 업데이트.md                # ✅ 작성 완료
├── 관리자-권한.md             # ✅ 작성 완료
├── 문제해결.md                # ✅ 작성 완료
├── 단축키-모음.md             # ✅ 작성 완료
└── 접근성-탐색.md             # ✅ 작성 완료 (New)
```

---

## 3. 각 페이지 템플릿

에이전트는 아래 형식을 따라 Wiki 페이지를 작성한다.

```markdown
# {페이지 제목}

> **한 줄 요약**: {이 페이지에서 설명하는 기능을 1문장으로}

## 개요

{기능이 무엇인지, 왜 필요한지 2~3문장. 접근성 관점 포함}

## 사용 방법

{실제 조작 순서. 번호 매기기. 스크린샷 자리 표시자 포함}

1. ...
2. ...

## 설정

{관련 설정 항목이 있으면 나열. 설정 패널 위치, config.json 키 설명}

## 팁

{실사용 팁, 알아두면 좋은 점. 선택적으로 작성}

## 관련 페이지

- [이전 페이지](링크)
- [다음 페이지](링크)
```

### 작성 규칙

| 규칙 | 설명 |
|------|------|
| 분량 | 각 페이지 40~80줄 내외 (과도한 길이 금지) |
| 스크린샷 | `<!-- 스크린샷: {설명} -->` 형태로 자리 표시자만 남김 |
| 코드 블록 | JSON 예시, 단축키 표기만 사용. C# 코드 금지 |
| 링크 | Wiki 내부 링크는 `[텍스트](파일명)` 형식 (확장자 제외) |
| 용어 | 영문 기능명은 괄호 안에 첫 등록만 병기. 예: "수식자 고정 (Sticky Keys)" |

---

## 4. 진행 상태 체크리스트

에이전트는 **자신이 할당받은 항목**만 작성한다.
작성 완료 후 이 표의 상태를 `✅ 완료`로 변경하고, Wiki 파일명을 `산출물` 열에 기록한다.
(코드 변경으로 인해 업데이트가 필요한 항목은 `대기 중`으로 리셋함)

| # | 페이지 | 상태 | 산출물 | 비고 |
|---|--------|------|--------|------|
| W01 | 소개 | ✅ 완료 | 소개.md | 기존 작성됨 |
| W02 | 시작하기 | ✅ 완료 | 시작하기.md | 기존 작성됨 |
| W03 | 화상키보드-기본 | ✅ 완료 | 화상키보드-기본.md | 조작 핸들(드래그 제한), 서브 라벨 업데이트 완료 |
| W04 | 한국어-입력 | ✅ 완료 | 한국어-입력.md | QuietEnglish 서브모드 업데이트 완료 |
| W05 | 자동완성 | ✅ 완료 | 자동완성.md | 동적 제안, 사용자 사전 편집기 업데이트 완료 |
| W06 | 수식자-고정 | ✅ 완료 | 수식자-고정.md | Sticky Keys 3단계, UI 인디케이터 |
| W07 | 체류클릭 | ✅ 완료 | 체류클릭.md | Dwell Click 설정, 프로그레스 링 |
| W08 | 키-반복 | ✅ 완료 | 키-반복.md | Key Repeat 지연/간격 설정 |
| W09 | 레이아웃-커스터마이징 | ✅ 완료 | 레이아웃-커스터마이징.md | JSON 내 `english_label` 등 업데이트 완료 |
| W10 | 앱별-레이아웃 | ✅ 완료 | 앱별-레이아웃.md | 프로필 자동 전환 설정 |
| W11 | 액션-키 | ✅ 완료 | 액션-키.md | 10종 키 액션 종류별 설명 |
| W12 | 이모지 | ✅ 완료 | 이모지.md | 카테고리, 전송 방식 |
| W13 | 클립보드-히스토리 | ✅ 완료 | 클립보드-히스토리.md | 히스토리 패널, 붙여넣기 |
| W14 | 설정 | ✅ 완료 | 설정.md | 효과음, 접근성 탐색 등 업데이트 완료 |
| W15 | 테마와-외관 | ✅ 완료 | 테마와-외관.md | 테마 모드, 투명도, 효과음 업데이트 완료 |
| W16 | 시스템-트레이 | ✅ 완료 | 시스템-트레이.md | 트레이 아이콘, 자동 실행 업데이트 완료 |
| W17 | 업데이트 | ✅ 완료 | 업데이트.md | 자동 업데이트 흐름 |
| W18 | 관리자-권한 | ✅ 완료 | 관리자-권한.md | 권한 모드, 제한사항 |
| W19 | 문제해결 | ✅ 완료 | 문제해결.md | FAQ, 트러블슈팅 |
| W20 | 단축키-모음 | ✅ 완료 | 단축키-모음.md | 전체 단축키 정리표 |
| W21 | Home.md 갱신 | ✅ 완료 | Home.md | 모든 페이지 완료 후 목차 갱신 |
| W22 | 접근성-탐색 | ✅ 완료 | 접근성-탐색.md | L1/L3 모드 통합, 스위치 스캔 설명 업데이트 완료 (코드 변경 반영) |

---

## 5. 에이전트 작업 지시 프로토콜

### 5-1. 작업 시작 전

에이전트는 작업 시작 시 다음을 수행한다:

```
1. 이 문서(docs/wiki-meta-manual.md)를 읽는다.
2. §4 체크리스트에서 자신에게 할당된 항목 번호(# )를 확인한다.
3. §6 소스 매핑에서 해당 항목의 관련 소스 파일 목록을 확인한다.
4. 관련 소스 파일을 읽고 기능을 정확히 파악한다.
5. 이미 작성된 Wiki 페이지(✅ 완료 항목)는 읽지 않아도 된다.
```

### 5-2. 작업 수행

```
1. §3 템플릿에 따라 마크다운을 작성한다.
2. 파일은 Wiki/AltKey.wiki/{페이지이름}.md 로 저장한다.
3. 이 문서(§4)의 해당 항목 상태를 ✅ 완료로 변경한다.
4. 이 문서(§4)의 산출물 열에 파일명을 기록한다.
```

### 5-3. 작업 완료 후

```
1. 이 문서에 기록된 상태가 정확한지 확인한다.
2. Home.md는 W21 할당 에이전트만 갱신한다. 개별 에이전트는 건드리지 않는다.
```

---

## 6. 소스 매핑 — 항목별 참조 파일

에이전트는 자신에게 할당된 항목의 소스 파일만 읽으면 된다.

| # | 항목 | 주요 소스 파일 (C:/Users/UITAEK/AltKey/AltKey/ 기준) |
|---|------|--------------------------------------|
| W03 | 화상키보드-기본 | `Views/KeyboardView.xaml` (드래그 핸들), `Controls/KeyButton.xaml`, `ViewModels/KeyboardViewModel.cs` (서브 라벨, 레이아웃 메트릭), `Services/LiveRegionService.cs` |
| W04 | 한국어-입력 | `Services/InputLanguage/KoreanInputModule.cs`, `Services/InputLanguage/InputSubmode.cs`, `Services/HangulComposer.cs`, `Services/InputLanguage/JamoNameResolver.cs` |
| W05 | 자동완성 | `Services/AutoCompleteService.cs`, `Services/KoreanDictionary.cs`, `Services/WordFrequencyStore.cs`, `ViewModels/SuggestionBarViewModel.cs`, `ViewModels/UserDictionaryEditorViewModel.cs` |
| W06 | 수식자-고정 | `Services/InputService.cs` (StickyKeys, LockedKeys), `Controls/KeyButton.xaml.cs` (IsSticky, IsLocked), `ViewModels/KeyboardViewModel.cs` |
| W07 | 체류클릭 | `Controls/KeyButton.xaml.cs` (DwellProgress), `ViewModels/MainViewModel.cs`, `Models/AppConfig.cs` (DwellTimeMs) |
| W08 | 키-반복 | `Controls/KeyButton.xaml.cs` (KeyRepeat관련), `Models/AppConfig.cs` (KeyRepeatDelayMs, IntervalMs) |
| W09 | 레이아웃-커스터마이징 | `Services/LayoutService.cs`, `Models/LayoutConfig.cs`, `Models/KeySlot.cs` (`english_label` 포함), `ViewModels/LayoutEditorViewModel.cs`, `layouts/Bagic.json` |
| W10 | 앱별-레이아웃 | `Services/ProfileService.cs`, `ViewModels/SettingsViewModel.cs` (Profiles), `ViewModels/MainViewModel.cs` (OnForegroundAppChanged) |
| W11 | 액션-키 | `Models/KeyAction.cs`, `Services/InputService.cs` (HandleAction), `Services/KeyNotationParser.cs` |
| W12 | 이모지 | `ViewModels/EmojiViewModel.cs`, `Views/EmojiPanel.xaml`, `Assets/emoji.json` |
| W13 | 클립보드-히스토리 | `Services/ClipboardService.cs`, `ViewModels/ClipboardViewModel.cs`, `Views/ClipboardPanel.xaml` |
| W14 | 설정 | `ViewModels/SettingsViewModel.cs`, `Services/ConfigService.cs`, `Models/AppConfig.cs` (신규 설정값 전반), `Controls/NumericAdjuster.xaml.cs` |
| W15 | 테마와-외관 | `Services/ThemeService.cs`, `Services/SoundService.cs` (효과음), `ViewModels/SettingsViewModel.cs` (OpacityIdle, KeyFontScalePercent) |
| W16 | 시스템-트레이 | `Services/TrayService.cs`, `Services/StartupService.cs` (자동 실행), `Services/HotkeyService.cs` |
| W17 | 업데이트 | `Services/UpdateService.cs`, `Services/DownloadService.cs`, `Services/InstallerService.cs` |
| W18 | 관리자-권한 | `Services/InputService.cs` (IsElevated, Mode), `Platform/Win32.cs`, `ViewModels/SettingsViewModel.cs` |
| W19 | 문제해결 | `Platform/Win32.cs` (제약), `docs/BLUEPRINT.md`, `docs/ime-korean-detection-problem.md` |
| W20 | 단축키-모음 | `Services/HotkeyService.cs`, `layouts/Bagic.json` |
| W22 | 접근성-탐색 | `Services/AccessibilityNavigationService.cs`, `ViewModels/KeyboardViewModel.cs` (MoveA11yFocus, ActivateA11yFocused), `Models/AppConfig.cs` (KeyboardA11yNavigationEnabled) |

---

## 7. 사용자 → 에이전트 지시 예시

사용자는 다음과 같이 간단히 지시하면 된다:

```
"W04 한국어-입력 Wiki 페이지 업데이트해줘"
"W22 접근성-탐색 Wiki 페이지 새로 작성해줘"
"W14 설정 페이지에 효과음이랑 자동 실행 설명 추가해줘"
```

---

## 8. 주의사항

1. **자동완성/한글 조합 코드는 분석만 하고 수정하지 않는다.** (`docs/auto-complet/CORE-LOGIC-PROTECTION.md` 참조)
2. **소스 코드를 Wiki에 복사하지 않는다.** 사용자 관점 설명만 작성.
3. **존재하지 않는 기능을 설명하지 않는다.** 소스 코드에 실제 구현된 것만.
4. **스크린샷은 자리 표시자로만 남긴다.** 실제 이미지 삽입은 사용자가 수동.
5. **여러 에이전트가 동시 작업 시 같은 파일을 덮어쓰지 않도록 주의.** Home.md는 W21 전담.
6. **NumericAdjuster**를 사용하는 설정 항목은 슬라이더 대신 수치 조절기라고 표현한다.
