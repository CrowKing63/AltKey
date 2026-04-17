# 00 — 공통 컨텍스트 (모든 태스크 참조)

> **이 문서는 읽기 전용 참조용**이다. 각 태스크 문서의 "전제 조건" 섹션이 이 문서를 가리킨다.
> **최종 갱신**: 2026-04-17

---

## 1. 프로젝트 한 줄 요약

AltKey는 **Windows용 WPF 포터블 가상 키보드**다. 한국어 사용자의 Narrator·Dwell 친화적 입력을 목표로 한다. 관리자 권한 없이 실행 시 `SendInput` + 유니코드 직접 입력으로 OS IME를 우회하고, 직접 한글 자모를 조합해 자동완성 제안을 생성한다.

---

## 2. 핵심 용어

| 용어 | 뜻 |
|---|---|
| **Submode** | 키보드 내부 입력 상태. 값은 `HangulJamo`(한글 조합) / `QuietEnglish`(조용한 영어, OS IME 건드리지 않음). 이번 리팩토링에서 신규 도입 |
| **"가/A" 버튼** | 키보드 레이아웃 안의 Submode 토글 버튼. "가"→HangulJamo, "A"→QuietEnglish. OS IME에는 영향 없음 |
| **상단바 한/영 버튼** | 타이틀바 영역에 있는 VK_HANGUL 비상 버튼. 누르면 OS IME에 `VK_HANGUL` 전송. AltKey 내부 Submode는 건드리지 않음 |
| **Unicode 모드** | `InputService.Mode == InputMode.Unicode`. `SendInput`으로 유니코드 문자를 직접 전송. OS IME를 우회. 자동완성 ON일 때 활성 |
| **VirtualKey 모드** | `InputService.Mode == InputMode.VirtualKey`. 가상 키 코드를 전송하고 OS IME가 조합. 관리자 권한이 있거나 자동완성 OFF일 때 활성 |
| **PrimaryLanguage** | 과거 개념. **이번 리팩토링에서 제거**. 한국어 전용이므로 항상 `ko` |
| **`_layoutSupportsKorean`** | 과거 게이트. **이번 리팩토링에서 제거**. 레이아웃은 언제나 한국어 |

---

## 3. 아키텍처 변경 요약 (Before → After)

### Before

```
KeyboardViewModel
 ├ _isKoreanInput   (VK_HANGUL 로 토글)
 ├ _layoutSupportsKorean (layout 내용 검사)
 ├ _lastImeKorean   (IMM32 폴링 결과)
 └ KeyPressed()
     ├ HandleKoreanLayoutKey()  ← 6가지 로직 얽힘
     ├ HandleEnglishSubMode()   ← 조용한 영어
     └ HandleEnglishLayoutKey() ← 폐기 대상

AutoCompleteService
 ├ _isHangulMode
 ├ OnHangulInput(jamo)
 └ OnKeyInput(vk)
```

### After

```
KeyboardViewModel
 └ KeyPressed()
     └ _autoComplete.OnKey(slot, ctx)   ← 단일 호출

AutoCompleteService
 └ OnKey(slot, ctx)  → _module.HandleKey(slot, ctx)

KoreanInputModule            (신규)
 ├ HangulComposer _composer
 ├ KoreanDictionary _koDict
 ├ EnglishDictionary _enDict
 ├ InputSubmode _submode = HangulJamo
 ├ HandleKey(slot, ctx)  ← 이전 HandleKoreanLayoutKey + HandleEnglishSubMode
 └ ToggleSubmode()       ← "가/A" 버튼이 호출
```

---

## 4. 디렉토리 구조 (요약)

```
AltKey/
├── Services/
│   ├── AutoCompleteService.cs        (수정)
│   ├── InputService.cs               (소폭: HasActiveModifiersExcludingShift 추가, Mode setter 노출 검토)
│   ├── HangulComposer.cs             (변경 없음 — 알고리즘 보존)
│   ├── KoreanDictionary.cs           (생성자 유지, 언어별 WordFrequencyStore 받기)
│   ├── EnglishDictionary.cs          (동상)
│   ├── WordFrequencyStore.cs         (언어별 인스턴스 분리)
│   └── InputLanguage/
│       ├── IInputLanguageModule.cs   (신규)
│       ├── InputSubmode.cs           (신규 enum)
│       └── KoreanInputModule.cs      (신규 — 기존 로직 이전)
├── ViewModels/
│   ├── KeyboardViewModel.cs          (대폭 수정)
│   ├── SuggestionBarViewModel.cs     (소폭)
│   └── MainViewModel.cs              (소폭: 자동완성 토글 연동)
├── Models/
│   ├── KeyAction.cs                  (ToggleKoreanSubmodeAction 추가)
│   ├── KeySlot.cs                    (HangulLabel → EnglishLabel 리네이밍)
│   └── AppConfig.cs                  (AutoCompleteEnabled 기본값 false 로)
├── Controls/
│   └── KeyButton.xaml                (AutomationProperties 바인딩)
├── Views/
│   ├── KeyboardView.xaml             (LiveRegion TextBlock 추가)
│   └── MainWindow.xaml               (상단바: 자동완성 토글 + VK_HANGUL 비상 버튼)
├── layouts/
│   ├── qwerty-ko.json                (VK_HANGUL 슬롯 → ToggleKoreanSubmode "가")
│   └── qwerty-en.json                (삭제)
└── Assets/Data/
    ├── ko-words.txt                  (변경 없음)
    └── en-words.txt                  (변경 없음 — QuietEnglish 서브모드용)
AltKey.Tests/
└── InputLanguage/
    └── KoreanInputModuleTests.cs     (신규)
```

---

## 5. 현재 상태 (리팩토링 전 기준선)

- `.NET 8.0-windows` / WPF + WindowsForms 혼용.
- `CommunityToolkit.Mvvm`, `Microsoft.Extensions.DependencyInjection`, `WPF-UI` 사용.
- `AltKey/Services/AutoCompleteService.cs` 131줄.
- `AltKey/Services/InputService.cs` 313줄.
- `AltKey/Services/HangulComposer.cs` 333줄.
- `AltKey/ViewModels/KeyboardViewModel.cs`에 `_isKoreanInput`(64), `_layoutSupportsKorean`(67), `_lastImeKorean`(71).
- `qwerty-ko.json:81`에 VK_HANGUL 슬롯 존재.
- 테스트 프로젝트 `AltKey.Tests` 존재(`HangulComposerTests.cs`).

---

## 6. 건드리면 안 되는 것

- **`HangulComposer` 내부 알고리즘** — 4번의 설계 반복 끝에 안정화됨(`CompositionDepth` 포함).
- **`SendAtomicReplace` 원자적 교체 로직** — 깜빡임 없는 조합 교체의 핵심.
- **`InputService.Mode` 결정 로직**(`CheckElevated()`) — 관리자 권한 감지.
  - 단, 06 태스크에서 **사용자가 수동으로 자동완성 토글을 끄면 VirtualKey로 전환**하는 setter를 추가하는 것은 허용.
- **`WordFrequencyStore`의 JSON 직렬화 설정**(`UnsafeRelaxedJsonEscaping`) — 한글이 `\uXXXX`로 저장되는 문제 해결책.

---

## 7. 반드시 읽어야 할 문서

- [docs/refactor-unif-serialized-acorn.md](../refactor-unif-serialized-acorn.md) §2, §4, §5, §6 (설계 결정 근거)
- [docs/ime-korean-detection-problem.md](../ime-korean-detection-problem.md) §6.5 (Unicode 우회 방식 채택 배경)
- [AGENTS.md](../../AGENTS.md) (자동완성 코드 수정 시 주의사항)

---

## 8. 코드 컨벤션

- `record` / `record struct`는 불변 모델에 사용.
- ViewModel은 `[ObservableProperty]` / `[RelayCommand]` 속성 활용.
- 의존성 주입은 `App.xaml.cs`의 `ServiceCollection`에 singleton으로 등록.
- 주석은 "왜"만. "무엇"은 쓰지 않는다.
- 파일 끝 줄바꿈 유지.

---

## 9. 용어 혼선 방지

- **"한글" vs "한국어"**: "한글"은 문자 체계. "한국어"는 언어. 이 문서에서는 언어 맥락에서 "한국어", 자모 맥락에서 "한글"을 사용.
- **"모드" vs "Submode"**: 키보드 입력 경로(Unicode/VirtualKey)는 **Mode**, 언어 모듈 내부 토글(HangulJamo/QuietEnglish)은 **Submode**.
- **"한/영"**: 상단바 버튼 맥락에서 OS IME의 한/영 전환을 의미. AltKey 내부 Submode와 무관.
