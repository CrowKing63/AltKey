# 자동완성 통합 리팩토링 — 최종 계획 (v2)

> 브랜치: `feature/unified-autocomplete`
> 원본 논의: [docs/refactor-unified-autocomplete.md](docs/refactor-unified-autocomplete.md)
> v1 확정: 2026-04-17 / v2 개정: 2026-04-17 (사용자 피드백 반영)

---

## 0. Context — 왜 이 변경을 하는가

현재 `AutoCompleteService`는 `_isHangulMode` 하나로 한/영을 이분법 분기하고, [AltKey/ViewModels/KeyboardViewModel.cs](AltKey/ViewModels/KeyboardViewModel.cs)는 `_isKoreanInput`(line 64) · `_layoutSupportsKorean`(line 67) · `_lastImeKorean`(line 71) 세 상태가 얽힌 채 레이아웃 내용(VK_HANGUL/HangulLabel 유무)만으로 언어 모드를 게이팅한다. 커스텀·혼합 레이아웃·다국어 확장에 모두 취약하며, Shift sticky 중 자모 키 처리 누락 같은 실사용 치명 버그("해+ㅆ → 해T")를 만든다. IME 상태 감지는 [docs/ime-korean-detection-problem.md](docs/ime-korean-detection-problem.md)에서 네 차례 시도 끝에 포기, 지금은 Unicode 우회 방식.

사용자(비전공자) 결론([docs/refactor-unified-autocomplete.md §7](docs/refactor-unified-autocomplete.md) + 이번 대화의 추가 피드백):

- Unicode 방식 유지, 한국어 기반에 영어를 덧붙이는 구조 대신 **한국어 사용자 전용 소프트웨어**로 재편.
- **Windows 8 터치키보드 모델 재해석**: 레이아웃은 한 장만 있고, 키의 라벨과 "가/A" 토글 버튼 라벨이 내부 입력 상태(한/영)에 따라 **전환 표시**된다(알파벳·한글 병기 아님). 사용자가 누르는 것은 이 "가/A" 버튼이다.
- OS IME 한/영 키(VK_HANGUL)는 "거의 누를 일 없는 비상 버튼"으로, 레이아웃 하단이 아니라 **상단바**로 격하.
- 한국어 사용자의 영어 입력은 보조 기능. IME 동기화 문제는 감수.
- 유니코드 방식은 자동 완성이 켜져 있을 때만. 자동 완성이 꺼져 있을 때는 기존 가상 키 방식 사용. 자동 완성(키 입력 방식)은 상단 바에서 손쉽게 토글. 이렇게 하면 유니코드 방식으로 입력이 안 되는 앱에서 빠른 조치 가능. 기본값은 오프.

제약: 포터블 1바이너리([docs/BLUEPRINT.md §1](docs/BLUEPRINT.md)), 접근성 최우선(Narrator·Dwell·인지 부담 최소화), `HangulComposer`/`SendAtomicReplace`/`CompositionDepth` 등 안정화된 알고리즘 보존.

의도한 결과: 언어 모듈이 다형성으로 격리되어 새 언어 추가가 "모듈 하나 추가"로 끝나고, 사용자는 설치 시 자기 모국어만 정하면 이후 앱이 **단일 레이아웃 + 내부 상태 토글**이라는 단일 멘탈 모델로 동작하며, 스크린 리더로도 키 라벨과 모드 전환이 일관되게 전달되며, "해+ㅆ" 같은 치명 버그가 회귀 없이 수정된다.

---

## 1. 사용자 확정 결정 사항 (v2, 2026-04-17)

| # | 결정 | 함의 |
|---|---|---|
| D1 | 단일 EXE | 포터블 유지. |
| **D2(v2)** | **"가/A" 토글 버튼**이 사용자의 실질 한/영 전환. 레이아웃은 단일, 내부 "InputSubmode" 상태에 따라 키 라벨과 버튼 라벨이 **자모↔알파벳 / 가↔A**로 전환 표시. 입력 경로·자동완성 사전도 동시에 스위치 | 한국어 사용자의 주 모드는 언제나 qwerty-ko 하나 |
| **D3(v2)** | OS IME 한/영 키(VK_HANGUL)는 **상단바**로 이동. 레이아웃 하단 바에서 제거. 접근성 이유로 `AutomationProperties.Name="OS IME 한영 전환"` + 최소 키 크기 유지. 거의 쓰지 않는 비상용 | 현재 [AltKey/layouts/qwerty-ko.json:81](AltKey/layouts/qwerty-ko.json:81)의 한/영 키를 제거하고 MainWindow 타이틀바 근처로 이동 |

| D5 | 이번 릴리스 접근성: **AutomationProperties(Narrator)** + **LiveRegion 모드 변경 안내** 만. TTS·고대비·큰 텍스트는 후속 | v1과 동일 |
| **D6(신규)** | **"해+ㅆ → 해T" 버그 이번 릴리스에 포함**. 원인 가설은 §6. 단위 테스트 추가 필수 | v1에서는 후속 과제였음. 승격 |
| D7 | 기존 `user-words.json`은 무시, 새로 시작. 새 규약: `user-words.{ko|en}.json` | v1과 동일 |

---

## 2. 아키텍처 — `KoreanInputModule` + 내부 Submode

런타임에 **정확히 하나**의 한국어 모듈이 활성. 한국어 모듈은 내부에 두 Submode(한글 조합 / 조용 영어)를 두고 "가/A" 토글로 전환한다. 레이아웃은 바뀌지 않고 라벨 렌더링과 입력 경로만 전환된다.

```
// 기존 IInputLanguageModule 계획은 폐기. KoreanInputModule을 메인으로 리팩토링 
IInputLanguageModule
├─ string LanguageCode            // "ko", "en"
├─ string DisplayName
├─ IDictionaryProvider Dictionary // 현재 활성 사전
├─ InputMode PreferredMode        // 힌트(InputService.Mode가 최종 결정자)
├─ InputSubmode ActiveSubmode     // ko=HangulJamo|QuietEnglish, en=English만
├─ string ComposeStateLabel       // "가" / "A" — 토글 버튼 표시용
├─ bool HandleKey(KeySlot, KeyContext)
├─ void ToggleSubmode()           // "가/A" 버튼이 호출
├─ void OnSeparator()
└─ void Reset()
// 메인 모듈
KoreanInputModule
├─ HangulComposer _composer       // 변경 없음, 내부 그대로 보존
├─ KoreanDictionary _koDict
├─ EnglishDictionary _enDict
├─ InputSubmode _submode = HangulJamo
├─ bool ToggleSubmode() → HangulJamo ↔ QuietEnglish
└─ Dictionary 선택: submode에 따라 _koDict / _enDict
// 폐기
EnglishInputModule
├─ EnglishDictionary _dict
└─ VirtualKey 경로 단일, Submode 토글 불가
```

### 2-1. Submode별 동작 규칙 (KoreanInputModule)

| Submode | 키 라벨 렌더링 | 입력 경로 | 자동완성 |
|---|---|---|---|
| `HangulJamo` (기본) | `slot.Label` (ㅂ/ㅈ/ㄷ…) | Unicode + HangulComposer + SendAtomicReplace (현재와 동일) | KoreanDictionary |
| `QuietEnglish` | `slot.HangulLabel` (q/w/e…) — 기존 필드 재해석 | `HandleEnglishSubMode` 경로 그대로(SendUnicode, OS IME는 한국어 유지) | EnglishDictionary (user-words.en.json) |

"가/A" 버튼 자체의 라벨은 `ComposeStateLabel`을 바인딩: HangulJamo=`"가"`, QuietEnglish=`"A"`.

### 2-2. 핵심 포인트 — 왜 이 구조가 기존 로직과 궁합이 좋은가

현재 [AltKey/ViewModels/KeyboardViewModel.cs:177-178](AltKey/ViewModels/KeyboardViewModel.cs:177)의 `HandleEnglishSubMode` 분기가 이미 `QuietEnglish` submode의 동작과 **같다**. 즉 새 설계는 이 분기를 **모듈 내부로 이동 + "가/A" 버튼이 유일한 진입로**로 제한한 것이며, 기존 알고리즘은 그대로 보존된다.

---

## 3. 설정·스키마 변경



### 3-3. 키 슬롯 스키마 — 기존 필드 재해석 (신규 필드 없음)

현재 [AltKey/layouts/qwerty-ko.json:26](AltKey/layouts/qwerty-ko.json:26) 같은 슬롯:
```json
{ "label": "ㅂ", "shift_label": "ㅃ", "hangul_label": "q", "action": {"type":"SendKey","vk":"VK_Q"} }
```

v2 해석(스키마 변경 없음, 코드 해석만 변경):

| 필드 | HangulJamo 상태 표시 | QuietEnglish 상태 표시 |
|---|---|---|
| `label` / `shift_label` | 이 값(한글 자모) | 사용 안 함 |
| `hangul_label` 필드명 리네이밍(`english_label`로) | 서브라벨로 표시(또는 숨김) | 이 값(영문 알파벳) 을 **메인 라벨**로 | 

즉 기존 `hangul_label`은 이름과 달리 사실상 "영어 입력 상태의 알파벳 라벨"로 재해석된다. `hangul_label` 필드명 리네이밍(`english_label`로).

[KeySlotVm.GetLabel](AltKey/ViewModels/KeyboardViewModel.cs:33)는 `(upperCase, submode)` 두 변수의 함수로 확장.

### 3-4. `user-words.{ko|en}.json` 분리

- [AltKey/Services/WordFrequencyStore.cs](AltKey/Services/WordFrequencyStore.cs) 생성자에 `languageCode` 매개변수.
- 파일명 `user-words.{languageCode}.json`.
- PrimaryLanguage=ko 사용자는 둘 다 주입, =en 사용자는 영어만.
- 기존 `user-words.json`은 읽지도 지우지도 않음(D7).

---

## 4. 파일별 수정 범위

### 4-1. [AltKey/Services/AutoCompleteService.cs](AltKey/Services/AutoCompleteService.cs)

- `_isHangulMode` 플래그와 `OnHangulInput` / `OnKeyInput` 이분법 제거.

- 단일 메서드 `OnKey(KeySlot slot, KeyContext ctx)` — 모듈 위임.
- `AcceptSuggestion(string)` 시그니처 유지. BS 계산은 모듈이 `GetAcceptInfo()` 반환.
- `CurrentWord`·`SuggestionsChanged`·`ResetState()` 인터페이스는 그대로.

### 4-2. [AltKey/ViewModels/KeyboardViewModel.cs](AltKey/ViewModels/KeyboardViewModel.cs)

- **제거**: `_layoutSupportsKorean`([line 67], [line 118-121]). PrimaryLanguage가 진실.
- **제거**: `_isKoreanInput`([line 64]). InputSubmode는 `KoreanInputModule` 내부에서 관리.
- **게이트 축약**: `KeyPressed`([line 138])의 3-way 분기(`HandleKoreanLayoutKey` · `HandleEnglishLayoutKey` · `HandleEnglishSubMode`)가 `_autoComplete.OnKey(slot, ctx)` 1회 호출로 축소.
- **이사**: [HandleKoreanLayoutKey](AltKey/ViewModels/KeyboardViewModel.cs:165)의 6가지 로직(Unicode/VirtualKey 분기·SendAtomicReplace·CompositionDepth BS·modifier 스킵·`FinalizeKoreanComposition`·separator 처리)을 `KoreanInputModule.HandleKey` 내부로 **줄 단위 그대로 이전**. 알고리즘 수정 없음.
- **이사**: [HandleEnglishSubMode](AltKey/ViewModels/KeyboardViewModel.cs:242)도 `KoreanInputModule` 내부 `QuietEnglish` submode 처리로 이전.
- **VK_HANGUL 처리(D3)**: 레이아웃 하단에서 제거되므로 이 분기 자체 불필요해짐. 상단바 버튼이 전용 이벤트 핸들러로 직접 `InputService.SendKeyPress(VK_HANGUL)` 호출.
- **유지**: `UpdateImeState()`([line 336-349])는 관리자 모드 전용으로 남김(현상 유지).
- **KeySlotVm 확장**: `GetLabel`이 `(bool upperCase, InputSubmode submode)`를 받도록 변경. `submode==QuietEnglish` && `slot.HangulLabel is not null`이면 `HangulLabel`을 메인 라벨로 반환, 대문자는 `ShowUpperCase`에 맞춰 `ToUpperInvariant()`. 그 외 기존 로직.
- **KeySlotVm.IsDimmed**: `submode==QuietEnglish` && `slot.HangulLabel is null`일 때 true. [KeyButton.xaml](AltKey/Controls/KeyButton.xaml)에서 Opacity 바인딩으로 시각적 비활성 표시.


### 4-4. [AltKey/Views/MainWindow.xaml](AltKey/Views/MainWindow.xaml) + 상단바 VK_HANGUL 버튼 (D3)

- 타이틀바(또는 제목 근처 영역)에 작은 버튼 하나 추가: 라벨 `"한/영"` 혹은 작은 아이콘.
- 핸들러: `_inputService.SendKeyPress(VK_HANGUL)` — **InputSubmode와 무관**. OS IME 토글만.
- 접근성:
  - `AutomationProperties.Name="OS IME 한영 전환"`
  - `AutomationProperties.HelpText="거의 사용할 일이 없는 비상용 버튼입니다"`
  - 버튼 크기 최소 `KeyUnit × 0.9` 보장 — 너무 작지 않게.

### 4-5. [AltKey/Views/KeyboardView.xaml](AltKey/Views/KeyboardView.xaml) + "가/A" 토글 (D2 핵심)

- 기존 VK_HANGUL 슬롯 위치([qwerty-ko.json:81](AltKey/layouts/qwerty-ko.json:81))에 새 슬롯.
- 새 액션 variant `ToggleKoreanSubmodeAction`([AltKey/Models/KeyAction.cs](AltKey/Models/KeyAction.cs)에 `JsonDerivedType` 추가).
- JSON 예:
  ```json
  { "label": "가", "action": { "type": "ToggleKoreanSubmode" }, "width": 1.25 }
  ```
- 키 표시 라벨은 `KeySlotVm`이 `ComposeStateLabel`을 구독해 동적으로 `"가"`/`"A"`로 렌더링.
- 너비 1.25(일반 키보다 넓음) — Dwell 오조작 방지.

### 4-6. [AltKey/Services/WordFrequencyStore.cs](AltKey/Services/WordFrequencyStore.cs)

- 생성자에 `languageCode` 추가. `RecordWord`의 `IsLatin` 분기 제거(스토어가 언어별이라 필요 없음).
- 최소 길이 규칙은 각 `IDictionaryProvider` 구현체로 이동.

### 4-7. [AltKey/App.xaml.cs](AltKey/App.xaml.cs)

- DI 등록:
  - `WordFrequencyStore("ko")`, `WordFrequencyStore("en")` 2개 싱글톤(PrimaryLanguage=ko 기준). =en이면 영어만.
  - `KoreanDictionary`, `EnglishDictionary` 그대로.
  - `AutoCompleteService`는 모듈 주입 받도록 시그니처 변경.
- 첫 실행 다이얼로그 호출.

### 4-8. [AltKey/Models/KeyAction.cs](AltKey/Models/KeyAction.cs)

- `[JsonDerivedType(typeof(ToggleKoreanSubmodeAction), "ToggleKoreanSubmode")]` 추가.
- `public record ToggleKoreanSubmodeAction() : KeyAction;` 신설.

### 4-9. [AltKey/layouts/qwerty-ko.json](AltKey/layouts/qwerty-ko.json)

- [line 81](AltKey/layouts/qwerty-ko.json:81)의 `{ "label": "한/영", "action":{"type":"SendKey","vk":"VK_HANGUL"} }` 슬롯을 **제거 또는 교체**:
  ```json
  { "label": "가", "action": { "type": "ToggleKoreanSubmode" }, "width": 1.25 }
  ```
- Space 너비 3.0 → 2.75 정도 미세 조정 가능(가로 폭 맞춤).
- OS IME 한/영 키는 **레이아웃 파일에 존재하지 않음**(상단바로 이동).

---

## 5. "가/A" 토글 + 상단바 VK_HANGUL — 동작 상세

### 5-1. "가/A" 버튼을 누르면

1. `KoreanInputModule.ToggleSubmode()` 호출.
2. 이전 submode의 조합 상태 플러시(`HangulComposer.Reset()` 또는 `CompleteCurrentWord()`).
3. InputSubmode 플래그 반전. `ComposeStateLabel` = 반대편 값.
4. PropertyChanged 이벤트로 모든 `KeySlotVm.GetLabel`이 재평가되어 UI가 자모↔알파벳 라벨로 전환.
5. LiveRegion 공지 TextBlock이 `"영어 입력 상태"` / `"한국어 입력 상태"`로 갱신 → Narrator 낭독.
6. **OS IME에 신호 보내지 않음** — 사용자 PC의 OS IME 상태는 변화 없음.

### 5-2. 상단바 VK_HANGUL 버튼을 누르면

1. `InputService.SendKeyPress(VK_HANGUL)` 단 1회 호출.
2. OS IME가 한/영 전환 신호 수신 → OS 수준의 한/영 토글.
3. AltKey의 InputSubmode는 **건드리지 않음**(동기화 불가능한 문제이므로 사용자 판단에 맡김).
4. LiveRegion 공지: `"OS IME 한영 전환 신호 전송됨"`.


### 5-4. 멘탈 모델 요약

| 누르는 것 | 바뀌는 것 | OS IME | 레이아웃 |
|---|---|---|---|
| "가/A" 버튼 | 키 라벨(자모↔알파벳), 입력 경로, 자동완성 사전 | 건드리지 않음 | 유지(qwerty-ko) |
| 상단바 한/영 버튼 | OS IME 한/영 | OS 수준 토글 | 유지 |

---

## 6. "해+ㅆ → 해T" 버그 수정 (D6)

### 6-1. 원인 가설 (코드 추적 결과)

재현 시나리오: 사용자가 "ㅎ+ㅐ"로 "해"를 조합 완료한 뒤, Shift sticky를 누르고 VK_T를 눌러 ㅆ을 입력하려 하면 결과가 "해T"로 찍힌다.

원인 추적:

1. [KeyboardViewModel.cs:175](AltKey/ViewModels/KeyboardViewModel.cs:175): `bool isComboKey = _inputService.HasActiveModifiers;`
2. Shift sticky가 활성이면 `HasActiveModifiers`가 **true 반환**(VK_SHIFT 포함).
3. [line 177-178](AltKey/ViewModels/KeyboardViewModel.cs:177): `_isKoreanInput==true`지만 isComboKey 분기가 먼저.
4. [line 183-188](AltKey/ViewModels/KeyboardViewModel.cs:183): isComboKey=true → `FinalizeKoreanComposition()` 후 return false → `HandleAction`이 VK_T를 가상키로 전송.
5. Unicode 모드에서는 OS IME가 AltKey의 "한국어 모드" 상태와 무관하게 작동하므로, OS IME가 영어 상태(사용자 PC의 기본)면 VK_SHIFT+VK_T = "T"가 찍힌다. 즉 자모 ㅆ 처리 경로를 아예 타지 못함.

**결론**: `isComboKey` 판별이 Shift 단독일 때까지 true로 처리되어 **쌍자음/쌍모음 입력이 구조적으로 막혀 있다**. Shift는 한국어 자모의 일반적 수식자(ㅃ/ㅉ/ㄸ/ㄲ/ㅆ/ㅒ/ㅖ 등)이지 "조합키"가 아니다.
**의문점**: shift 키와 알파벳 조합키를 사용하는 사례가 없나?

### 6-2. 수정안 (모듈 이전과 함께 적용)

- `KoreanInputModule.HandleKey`(현 `HandleKoreanLayoutKey`에서 이전)에서 조합키 판별을 **"Shift를 제외한 modifier가 하나라도 활성이면 조합키"**로 좁힌다:
  ```csharp
  // 의사 코드
  bool isComboKey = _inputService.HasActiveModifiersExcludingShift();
  ```
- `InputService`에 신규 프로퍼티 `HasActiveModifiersExcludingShift`(혹은 `HasNonShiftModifiers`) 추가. 기존 `HasActiveModifiers`는 그대로 유지(다른 경로에서 아직 사용).
- 자모 키 판별은 기존 `GetHangulJamoFromSlot`([line 312-319](AltKey/ViewModels/KeyboardViewModel.cs:312)) 로직 유지 — 이미 `ShowUpperCase && slot.ShiftLabel is 자모`이면 ShiftLabel을 반환하도록 되어 있으므로 Shift sticky 상태에서 ㅆ/ㅃ 등이 정상 감지됨.

### 6-3. 단위 테스트

신규 테스트 파일: `AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs`(프로젝트 없으면 작게 신규).

다음 시나리오:
- `Feed("ㅎ"), Feed("ㅐ"), Separator()` → Current="해".
- 이어서 `Feed("ㅆ")` (Shift 활성 상태 시뮬레이션) → Current는 "해" 뒤에 "ㅆ" 단독 음절(초성 단독 혹은 새 조합 시작)로 기록되고, 실제 SendInput 시퀀스는 `BS + "해" + "ㅆ"` 또는 적절한 원자 교체.
- `HasActiveModifiersExcludingShift`가 Shift만 활성일 때 false를 반환하는지 InputService 단위 테스트.
- Ctrl+Shift+T 같은 진짜 조합키에서는 여전히 자모 경로를 타지 않는지 음성 케이스 검증.

### 6-4. 회귀 방지

- 기존 동작이 있다면 확인: "쌍자음/쌍모음" 키가 실제로 어떻게 입력되고 있었는가? qwerty-ko에서 `shift_label="ㅃ"` 등이 선언된 키들이 지금 어떻게 작동하는지 수동 확인 필요(아마 동일 버그로 깨져 있을 가능성 높음).
- 수정 후: 모든 `ShiftLabel` 자모 키(ㅃ/ㅉ/ㄸ/ㄲ/ㅆ/ㅒ/ㅖ)를 수동 입력 테스트.

---

## 7. 접근성 — 이번 릴리스 범위 (D5)

### 7-1. AutomationProperties — [AltKey/Controls/KeyButton.xaml](AltKey/Controls/KeyButton.xaml)

현재 `ContentPresenter`([line 42])에 UIA 바인딩이 전혀 없음. 추가:

- `Style.Setter`로:
  - `AutomationProperties.Name="{Binding AccessibleName}"`
  - `AutomationProperties.HelpText="{Binding AccessibleHelp}"`
  - `AutomationProperties.AutomationId="{Binding AutomationId}"`
- `KeySlotVm`에 추가(InputSubmode 의존):
  - `AccessibleName`: 
    - HangulJamo 상태, 자모 키 → 한국어 자모명("비읍", "이응" 등) — 신규 `JamoName(string)` 헬퍼.
    - QuietEnglish 상태 → `HangulLabel`의 알파벳(예: "큐", 영문 키 이름). 간단히 "Q 키" 수준으로.
    - 상태 키(Shift/Ctrl/Enter/Tab/Space) → 한국어 명칭.
    - "가/A" 토글 버튼 → `"한국어 입력 중"`/`"영어 입력 중, 누르면 한국어로 돌아감"` 상태형 문장.
  - `AccessibleHelp`: Sticky/Locked/Modifier 상태를 문장으로.
- 서브라벨 TextBlock([line 33])은 `AutomationProperties.AccessibilityView="Raw"`로 이중 낭독 차단.

### 7-2. LiveRegion — [AltKey/Views/KeyboardView.xaml](AltKey/Views/KeyboardView.xaml)

- `Opacity=0`, `IsHitTestVisible=False`, `AutomationProperties.LiveSetting="Polite"`인 `TextBlock x:Name="ModeAnnouncer"`.
- 갱신 트리거:
  - "가/A" 토글 → `"영어 입력 상태"` / `"한국어 입력 상태"`.
  - 상단바 한/영 → `"OS IME 한영 전환 신호 전송됨"`.
  - PrimaryLanguage 최초 선택 → `"한국어 키보드 설정이 적용되었습니다"`.
- 각 갱신 시 `AutomationPeer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged)`.

### 7-3. Dwell — 이번 릴리스 최소 대응

- 2단계 확인은 후속 과제.
- "가/A" 버튼 너비 1.25, 상단바 한/영 버튼 최소 `KeyUnit × 0.9`, 첫 실행 모달 버튼 1.5배. 오조작 확률 낮춤.

---

## 8. 검증 방법

스크린 리더 테스트 포함이므로 자동화로 전부 잡히지 않는다. 아래 단계를 손으로.

1. **빌드** — `dotnet build` 녹색, 기존 테스트 전부 통과.
2. **새 단위 테스트**(§6-3) — `dotnet test`로 "해+ㅆ" 시나리오 통과.
4. **HangulComposer 회귀**(알고리즘 보존):
   - "ㅎ+ㅐ"로 "해", "ㄷ+ㅏ+ㄺ"으로 "닭" 등 기본 조합.
   - 조합 중 Backspace → CompositionDepth 기반 원자 교체 정상.
   - **"해+ㅆ → 해ㅆ"**(T가 아니라 ㅆ으로) 실제 확인.
   - 쌍자음/쌍모음 전체(ㅃ/ㅉ/ㄸ/ㄲ/ㅆ/ㅒ/ㅖ) 수동 타자.
5. **"가/A" 토글(D2)**:
   - 초기 상태: 키 라벨이 한글 자모, 버튼은 "가".
   - 버튼 누름 → 키 라벨 알파벳, 버튼 "A"
   - 이 상태에서 `abc` 입력 → 메모장에 유니코드 영문 그대로 찍힘, 영어 자동완성 제안 출현.
   - 다시 누름 → 한국어 복귀.
6. **상단바 VK_HANGUL(D3)**:
   - 상단바 버튼 → 작업표시줄 IME 인디케이터가 EN↔KO로 토글.
   - AltKey 내부 라벨/Submode 변화 없음(의도된 격리).
8. **Narrator**:
   - Ctrl+Win+Enter로 Narrator. 키 포커스 이동 시 자모명·알파벳·모디파이어 상태가 올바르게 읽히는지.
   - "가/A" 전환 시 LiveRegion 안내가 낭독되는지.
   - 상단바 버튼에 포커스 이동 시 HelpText("거의 사용할 일이 없는 비상용…")가 읽히는지.

---

## 9. 열린 위험 / 후속 과제

- [ ] **상단바 버튼 위치·크기 UX**: MainWindow 타이틀바에 직접 꽂을지(공간 작음), 키보드 상단 전용 바를 만들지(세로 공간 증가) 실제 렌더링 후 결정.
- [ ] Dwell 2단계 확인("가/A" 오조작 방지) — 후속.
- [ ] TTS·고대비·큰 텍스트([docs/feature-accessibility.md](docs/feature-accessibility.md))는 후속.
- [ ] 하드웨어 한/영 키 훅(`WH_KEYBOARD_LL`) — 후속.

---

## 10. Critical Files (수정 대상)

| 파일 | 수정 유형 | 비고 |
|---|---|---|
| [AltKey/Services/AutoCompleteService.cs](AltKey/Services/AutoCompleteService.cs) | 대폭 수정 | `_isHangulMode` 제거, 모듈 위임, 단일 `OnKey` |
| [AltKey/ViewModels/KeyboardViewModel.cs](AltKey/ViewModels/KeyboardViewModel.cs) | 대폭 수정 | 3상태 필드 제거, 6가지 로직 모듈로 이사, `KeySlotVm.GetLabel` 확장 |
| [AltKey/Services/WordFrequencyStore.cs](AltKey/Services/WordFrequencyStore.cs) | 수정 | 언어별 파일 분리 |
| [AltKey/Services/InputService.cs](AltKey/Services/InputService.cs) | 소폭 | `HasActiveModifiersExcludingShift` 추가 (§6-2) |
| [AltKey/Models/KeyAction.cs](AltKey/Models/KeyAction.cs) | 수정 | `ToggleKoreanSubmodeAction` JsonDerivedType 추가 |
| [AltKey/Controls/KeyButton.xaml](AltKey/Controls/KeyButton.xaml) | 수정 | AutomationProperties 바인딩 |
| [AltKey/Views/KeyboardView.xaml](AltKey/Views/KeyboardView.xaml) | 수정 | LiveRegion 공지 TextBlock |
| [AltKey/Views/MainWindow.xaml](AltKey/Views/MainWindow.xaml) | 수정 | 상단바 VK_HANGUL 버튼 |
| [AltKey/layouts/qwerty-ko.json](AltKey/layouts/qwerty-ko.json) | 수정 | VK_HANGUL 슬롯 → `ToggleKoreanSubmode` "가" 슬롯 |
| [AltKey/layouts/qwerty-en.json](AltKey/layouts/qwerty-en.json) | 변경 없음 | "가/A" · 한/영 둘 다 없음 |
| `AltKey/Services/InputLanguage/KoreanInputModule.cs` | 신규 | HangulComposer·두 Dictionary·Submode |
| `AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs` | 신규 | "해+ㅆ" 회귀 테스트 등 |

**재사용 대상(내부 수정 없음)**:
- [AltKey/Services/HangulComposer.cs](AltKey/Services/HangulComposer.cs) — 알고리즘 보존.
- [AltKey/Services/KoreanDictionary.cs](AltKey/Services/KoreanDictionary.cs) / [AltKey/Services/EnglishDictionary.cs](AltKey/Services/EnglishDictionary.cs) — `IDictionaryProvider` 구현 추가만.
- [AltKey/Services/InputService.cs](AltKey/Services/InputService.cs)의 `Mode`·`SendAtomicReplace`·`SendKeyPress`·`HasActiveModifiers` — 그대로.
