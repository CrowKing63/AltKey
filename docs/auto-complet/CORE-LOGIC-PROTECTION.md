# 한국어 자동완성 핵심 로직 보호 문서

> **이 문서의 역할**: 한국어 자동완성 코드를 만질 수 있는 범위와 만지면 안 되는 범위를 선긋는다. 자동완성 기능은 설계가 4번 갈아엎혔고, 지금의 "Unicode 우회 + HangulComposer + Submode 토글" 구조에 도달하기까지 수많은 회귀 버그를 치렀다. 이 문서를 이해하지 못하면 **어떤 한 줄도 고치지 말라**.
>
> **갱신 정책**: 밑의 "절대 건드리지 말 것" 목록은 실제로 기능이 더 안정화되어 제약이 완화되었을 때만 축소한다. 임의 삭제·재해석 금지.

---

## 0. TL;DR (에이전트가 먼저 읽을 30초 요약)

- 자동완성은 **`KoreanInputModule` 하나**에 모든 한글/조용한 영어 로직이 모여 있다. 바깥에서 직접 자모를 `HangulComposer`에 주입하는 코드 경로를 새로 만들지 말라.
- 키 입력 파이프라인: `KeyboardViewModel.KeyPressed()` → `AutoCompleteService.OnKey()` → `KoreanInputModule.HandleKey()` → 반환값으로 `InputService.HandleAction()` 스킵 여부 결정. **이 순서를 뒤집지 말 것**.
- Unicode 모드(비관리자)에서는 **`HangulComposer`가 OS 화면과 1:1로 동기화**된다. `InputService.TrackedOnScreenLength`와 `HangulComposer` 내부 상태가 어긋나면 즉시 깜빡임·중복 입력·전체 삭제가 발생한다.
- 모든 키 이벤트는 주 스레드(WPF Dispatcher)에서 처리된다. 다른 스레드에서 `HangulComposer`를 건드리지 말라.

---

## 1. 기능의 본질 — 이 코드가 "무엇"을 하는지

AltKey는 Windows 가상 키보드다. 한국어를 입력할 때 OS IME에 VK 코드를 보내면 IME가 조합을 해 주는 게 보통이지만, AltKey는 **IME를 우회해서 한글을 직접 유니코드로 조립해 송신**한다. 그래야 IME 상태를 모르고도 "지금 내가 무엇을 조합 중인지" 확신할 수 있고, 이 조합 상태를 가지고 자동완성 제안을 만든다.

핵심 구성 요소:

1. **`HangulComposer`** (`AltKey/Services/HangulComposer.cs`)
   두벌식 자모 시퀀스를 받아 "완성된 음절들 + 조합 중 음절"을 유니코드로 조립. 4번 재설계 끝에 초성·중성·종성·겹받침·겹모음·종성→다음초성 이동·겹받침 분해까지 처리한다.

2. **`KoreanInputModule`** (`AltKey/Services/InputLanguage/KoreanInputModule.cs`)
   서브모드(`HangulJamo` / `QuietEnglish`) 스위치, 키 → 자모 추출, Unicode/VirtualKey 모드별 분기, 제안 갱신, 단어 학습, 제안 수락 BS 카운트 계산을 모두 담당한다.

3. **`InputService.SendAtomicReplace(prevLen, newOutput)`**
   화면에 이미 전송한 조합 문자열(길이 `prevLen`)을 **한 번의 `SendInput` 호출로** 백스페이스하고 새 조합을 유니코드로 쏴서 깜빡임 없이 교체한다. 이 "원자성"이 깨지면 조합 중 텍스트 포커스가 튀거나 이벤트 중간에 다른 앱이 끼어든다.

4. **`KoreanDictionary` + `EnglishDictionary` + `WordFrequencyStore`** (`AltKey/Services/`)
   내장 빈도 사전(리소스 txt) + 사용자별 JSON 저장소. 언어별로 파일이 분리되어 있고 한글은 `UnsafeRelaxedJsonEscaping`으로 저장해야 `\uXXXX` 이스케이프를 피한다.

5. **"가/A" 토글 버튼 → `ToggleKoreanSubmodeAction`** (`AltKey/Models/KeyAction.cs`)
   이 액션만이 `KoreanInputModule.ToggleSubmode()`를 호출할 수 있는 유일한 경로다. OS IME의 한/영은 상단바 VK_HANGUL 비상 버튼이 담당하고, AltKey 내부 서브모드와는 **독립적**이다.

---

## 2. 절대 건드리지 말 것 (Hard Freeze)

| 대상 | 이유 |
|---|---|
| `HangulComposer`의 **내부 조합 알고리즘** (`Feed`, `Backspace`, `FinalizeCurrent`, `ComposeCurrentSyllable`) | 4번 재설계로 안정화. 겹받침 분해·종성→다음초성 이동 등 모든 엣지를 테스트가 잡고 있다. 알고리즘을 바꾸려면 반드시 `HangulComposerTests` 전체 녹색 + "화사 3회 BS → 빈 필드" 같은 OS IME 재현 시나리오를 재검증. |
| `HangulComposer.CompositionDepth` 속성 의미 | 제안 수락 시 BS 횟수 계산의 근거. 의미를 바꾸면 "문문제" 회귀. |
| `InputService.SendAtomicReplace`의 **단일 `SendInput` 호출** 구조 | BS와 유니코드 전송이 한 호출에 묶여야 깜빡임이 없다. 두 번의 호출로 쪼개지 말 것. |
| `SendAtomicReplace` 끝의 `ReleaseTransientModifiers()` 호출 | Shift sticky가 해제되어야 다음 키가 소문자로 해석된다. 제거하면 "해+ㅆ → 해ㅆ" 회귀. |
| `InputService.Mode` 결정 로직 (`CheckElevated()` / 시작 시 배정) | 관리자 → VirtualKey, 일반 → Unicode. 보안앱 호환성과 직결. 단, `TrySetMode`로 사용자가 자동완성을 껐을 때 VirtualKey로 내려가는 것은 허용(`06-autocomplete-toggle.md` 참조). |
| `WordFrequencyStore`의 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping` 옵션 | 한글이 `\uXXXX`로 저장되는 문제의 유일한 해결책. 기본값으로 돌리지 말 것. |
| `KoreanInputModule.HandleKey`의 **조합키 판별이 `HasActiveModifiersExcludingShift` 쓰는 것** | Shift+쌍자음(ㅆ·ㄲ…)을 정상 자모 경로로 보내기 위해 도입. `HasActiveModifiers`로 바꾸면 "해+ㅆ → 해T" 회귀. |
| `KoreanInputModule.HandleKey`에서 **`jamo == null`일 때 `FinalizeComposition` 호출하지 않는 것** | 호출하면 `TrackedOnScreenLength`가 0으로 리셋되어 이후 조합의 BS 계산이 망가진다(버그 목록 #2). |
| `AutoCompleteService`가 **`IInputLanguageModule`에 단순 위임**만 하는 얇은 래퍼라는 사실 | 과거에는 여기에 `_isHangulMode`, `_hangul`, `OnHangulInput`/`OnKeyInput` 분기가 섞여 있었음. 다시 합치지 말 것. |
| `KoreanInputModule` 내부에서 `InputService.SendAtomicReplace` / `SendUnicode`만 호출하는 규율 | 모듈이 `SendKeyPress`를 직접 쏘면 Unicode 모드의 TrackedOnScreenLength와 어긋난다. |
| `KeyboardViewModel.IsSeparatorKey`가 SPACE/RETURN/TAB **세 개만** 보는 것 | 구두점은 모듈 내부(`KoreanInputModule.IsSeparator`)에서 별개로 처리한다. 두 곳을 합치려 하지 말 것. |
| `qwerty-ko.json`의 "가" 버튼 슬롯 `ToggleKoreanSubmode` 액션 | VK_HANGUL이 아닌 별도 액션이다. OS IME에 VK_HANGUL을 보내는 것은 상단바 비상 버튼의 몫. |
| 자동완성이 **기본값 OFF**라는 사실 (`AppConfig.AutoCompleteEnabled`) | 관리자 모드로 실행되는 사용자가 켜는 순간 Unicode 모드로 전환을 못 해서 기능이 깨지는 케이스를 막는다. |

---

## 3. 만져도 되는 영역 (조심스럽게)

| 대상 | 조건 |
|---|---|
| `KoreanDictionary` / `EnglishDictionary`의 제안 병합 규칙 | 반환 개수·중복 제거·랭킹을 바꿔도 안전. 단, 내장 사전을 수정하려면 `.txt` 리소스를 바꾸거나 사용자 학습 우선순위 규칙은 건드리지 말 것. |
| `WordFrequencyStore`의 저장 빈도·프루닝 경계치 | 성능·사전 품질 관점 개선은 허용. `Save()` 에러 무시를 로깅으로 바꾸는 것도 허용. 단, `UnsafeRelaxedJsonEscaping`은 유지. |
| `KoreanInputModule`의 **영어 prefix 대소문자 정규화** | 현재 영문은 내부적으로 소문자로 저장. 학습 키 대소문자 규칙을 바꾸는 건 허용하되 `RecordWord(word.ToLowerInvariant())` 현재 계약을 지킬 것. |
| 단일 자모 학습 방지 같은 품질 필터 | `FinalizeComposition`의 `_composer.Current.Length > 0` 조건을 `> 1` 또는 "조합 완성된 한글 음절 존재"로 강화해도 괜찮다. |
| 제안 수락(`AcceptSuggestion`) 이후의 `TrackedOnScreenLength` 처리 | **여기는 실제로 버그가 있음.** [TASK-01-accept-tracked-length-reset.md](TASK-01-accept-tracked-length-reset.md) 참조. |
| `IsImeKorean()` / IMM32 호출 경로 | 이미 모든 호출자가 제거됨(dead code). 정리 허용. [TASK-02-dead-code-imm-ime.md](TASK-02-dead-code-imm-ime.md) 참조. |
| UI 라벨·ARIA·제안 바 표시 규칙 | `KeySlotVm.RefreshDisplay` 계열은 자유롭게 수정 가능. 단, `ToggleKoreanSubmode` 액션 라벨 "가/A"는 접근성 announcement와 동기화되어야 한다. |

---

## 4. 핵심 시퀀스 — 이 흐름을 바꾸지 말 것

### 4.1 자모 입력 (Unicode 모드, HangulJamo 서브모드)

```
User taps "ㅎ" 키
  → KeyboardViewModel.KeyPressed(slot)
     → AutoCompleteService.OnKey(slot, ctx)
        → KoreanInputModule.HandleKey(slot, ctx)
           · isComboKey = ctx.HasActiveModifiersExcludingShift   (false)
           · jamo = "ㅎ"
           · prevLen = ctx.TrackedOnScreenLength                  (예: 0)
           · _composer.Feed("ㅎ")
           · newOutput = _composer.Current                        ("ㅎ")
           · _input.SendAtomicReplace(0, "ㅎ")                    ← 화면 갱신 + TrackedOnScreenLength=1
           · SuggestionsChanged?.Invoke(_koDict.GetSuggestions("ㅎ"))
           · return true
     → handled=true → HandleAction 스킵
```

그다음 "ㅐ":
```
  · prevLen = 1
  · _composer.Feed("ㅐ")   → 초성+중성 → "해"
  · SendAtomicReplace(1, "해")    ← BS 1번 + "해" 유니코드
  · TrackedOnScreenLength = 1
```

### 4.2 단어 구분자

```
User taps Space
  → KeyboardViewModel.IsSeparatorKey(slot) == true
     → AutoCompleteService.OnSeparator()
        → KoreanInputModule.OnSeparator() → FinalizeComposition()
           · _koDict.RecordWord("해")
           · _composer.Reset()
           · _input.ResetTrackedLength()                          ← 반드시 호출!
           · SuggestionsChanged?.Invoke(empty)
     → InputService.HandleAction(SendKey VK_SPACE)
```

### 4.3 제안 수락

```
User taps "해달" 제안 버튼
  → SuggestionBarViewModel.AcceptSuggestion("해달")
     → AutoCompleteService.AcceptSuggestion("해달")
        → KoreanInputModule.AcceptSuggestion("해달")
           · bsCount = _composer.CompletedLength + _composer.CompositionDepth   (예: 0 + 2 = 2)
           · _koDict.RecordWord("해달")
           · _composer.Reset()
           · return (2, "해달")
     · Mode == Unicode:
        · _inputService.SendAtomicReplace(2, "해달")   ← BS 2번 + "해달" 유니코드
        · _inputService.TrackedOnScreenLength = 2     (SendAtomicReplace 내부와 중복 설정)
```

**⚠️ 주의**: 이 시점 `TrackedOnScreenLength`가 `fullWord.Length`로 남아 있으면, 사용자가 바로 다음 자모를 누를 때 `prevLen=2`가 돼서 **"해달"을 통째로 지우고 새 자모만** 남긴다. 이 부분은 [TASK-01](TASK-01-accept-tracked-length-reset.md)에서 다룬다.

### 4.4 서브모드 토글 ("가/A")

```
User taps "가" 버튼
  → KeyboardViewModel.KeyPressed
     · slot.Action is ToggleKoreanSubmodeAction
     · AutoCompleteService.ToggleKoreanSubmode()
        → KoreanInputModule.ToggleSubmode()
           · FinalizeComposition()                                ← 기존 조합 먼저 확정·학습
           · _submode 반전
           · SuggestionsChanged(empty) + SubmodeChanged
     · UpdateModifierState() → 라벨 갱신
```

OS IME 한/영과는 완전히 독립. 이 사이에 `VK_HANGUL`을 끼워 넣지 말 것.

---

## 5. 수정 시 체크리스트 (PR 전)

- [ ] `AltKey.Tests`의 `HangulComposerTests`와 `KoreanInputModuleTests` 전체 녹색?
- [ ] "해+ㅆ → 했", "ㄷㅏㄹㄱ → 닭", "화사에서 BS 3회 → 빈 필드", "엔터 조합 중 → 다음 줄 정상" 네 가지 수동 시나리오 재현?
- [ ] Unicode 모드(비관리자)와 VirtualKey 모드(관리자) 둘 다 재현 확인?
- [ ] `qwerty-ko.json`의 "가" 슬롯 액션이 `ToggleKoreanSubmode`이고 상단바 VK_HANGUL과 섞이지 않았는지 확인?
- [ ] `WordFrequencyStore.Save`의 JSON 옵션이 바뀌지 않았는지 확인? 저장된 `user-words.ko.json`을 열어 한글이 그대로 보이는지?
- [ ] `SendAtomicReplace`가 여전히 **단일 `SendInput` 호출**인지?

---

## 6. 회귀 위험 냄새 (리뷰 시 레드 플래그)

- `HangulComposer`에 public setter가 새로 생겼다 → 상태 일관성 깨짐 위험.
- `_isHangulMode`, `_isKoreanInput`, `_layoutSupportsKorean`, `_lastImeKorean` 같은 **제거된 이름이 다시 등장** → 과거 설계로 회귀 중.
- `AutoCompleteService`가 `HangulComposer`를 직접 소유 → 모듈 경계 파괴.
- `KoreanInputModule.HandleKey` 내부에서 `_input.SendKeyPress(...)`를 직접 호출 → VirtualKey 경로와 뒤섞임.
- `KeyboardViewModel.IsSeparatorKey`에 구두점/마침표 추가 → 모듈 내부 `IsSeparator`와 이중 플러시.
- `SendAtomicReplace`가 `SendUnicode`와 별도 `SendInput`로 분리 → 깜빡임 복귀.
- 새 자동완성 토글이 `config.Save()`만 호출하고 `Update()`를 안 부름 → 창 높이 버그 #6 재발.

---

## 7. 참고 문서 (이 문서와 함께 읽을 것)

- [../ime-korean-detection-problem.md](../ime-korean-detection-problem.md) §6.5 — Unicode 우회 채택 배경
- [../refactor-unified-autocomplete.md](../refactor-unified-autocomplete.md) — 통합 설계 결정
- [../refactor-unif-serialized-acorn.md](../refactor-unif-serialized-acorn.md) §2, §4, §6 — 모듈 분리 이유
- [../refactoring_ToDo/00-overview.md](../refactoring_ToDo/00-overview.md) §6 "건드리면 안 되는 것"
- [../refactoring_ToDo/버그 목록.md](../refactoring_ToDo/버그%20목록.md) — 최근 고친 회귀들의 맥락
- [findings-overview.md](findings-overview.md) — 이 폴더의 새 분석 요약
