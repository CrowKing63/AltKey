# 03 — `KoreanInputModule` 신규 작성 + "해+ㅆ → 해T" 버그 수정

> **소요**: 3~5시간 (이 리팩토링의 핵심 덩어리)
> **선행**: 01, 02
> **후행**: 04, 05, 10
> **관련 기획**:
> - [refactor-unif-serialized-acorn.md §2, §4-1, §4-2, §6](../refactor-unif-serialized-acorn.md)
> - [feature-korean-autocomplete.md](../feature-korean-autocomplete.md)
> - [ime-korean-detection-problem.md §6.5](../ime-korean-detection-problem.md)

---

## 0. 이 태스크의 목표 한 줄

지금 `KeyboardViewModel.HandleKoreanLayoutKey`(line 165~)와 `HandleEnglishSubMode`(line 242~)에 엉켜 있는 **한국어 모듈 로직을 `KoreanInputModule` 한 클래스로 옮긴다**. 옮기는 과정에서 "해+ㅆ → 해T" 버그를 같이 잡는다. 알고리즘은 **이동만**, 수정은 버그 한 군데뿐.

---

## 1. 전제 조건

- [00-overview.md](00-overview.md), [01](01-models-interfaces.md), [02](02-wordstore-split.md) 완료.
- `IInputLanguageModule`, `InputSubmode`, `KeyContext`가 존재.
- `KoreanDictionary`·`EnglishDictionary`가 언어별 `WordFrequencyStore`를 보유.

---

## 2. 현재 상태 (복사해 올 대상 로직)

다음 세 곳의 코드를 **이해한 다음** 모듈로 옮긴다.

### 2-1. `AltKey/ViewModels/KeyboardViewModel.cs:165~240` (HandleKoreanLayoutKey)

6가지 로직이 얽혀 있음:
1. VK_HANGUL 키 감지 + 내부 `_isKoreanInput` 토글 + `FinalizeKoreanComposition()`.
2. `isComboKey = _inputService.HasActiveModifiers` 판별 — **이것이 "해+ㅆ" 버그의 원인**.
3. Unicode 모드 / VirtualKey 모드 분기.
4. 자모 추출 (`GetHangulJamoFromSlot` @ line 312-319).
5. `HangulComposer.Feed` → `SendAtomicReplace` (Unicode 모드).
6. `return true/false`로 `HandleAction` 스킵 여부 결정.

### 2-2. `AltKey/ViewModels/KeyboardViewModel.cs:242~` (HandleEnglishSubMode)

- Shift sticky 등 modifier 없을 때 영문 알파벳을 `SendUnicode('q')`로 전송.
- `AutoCompleteService.OnKeyInput(vk)`로 자동완성 처리.
- `return true`로 `HandleAction` 스킵.

### 2-3. `AltKey/Services/AutoCompleteService.cs`

- `OnHangulInput(string jamo)`: `_hangul.Feed(jamo)` + `_koreanDict.GetSuggestions(_hangul.Current)` → 이벤트 발생.
- `OnHangulBackspace()`: `_hangul.Backspace()` + 제안 갱신.
- `OnKeyInput(VirtualKeyCode vk)`: 영문 추적.
- `CompleteCurrentWord()`: 현재 조합을 `_store.RecordWord`로 학습.

이 로직을 모듈로 이전. 모듈은 자체적으로 `HangulComposer`를 소유하고, 두 사전을 Submode에 따라 선택.

---

## 3. 작업 내용

### 3-1. `InputService`에 `HasActiveModifiersExcludingShift` 추가

**파일**: `AltKey/Services/InputService.cs` (소폭)

기존 `HasActiveModifiers`(313줄 파일의 어딘가):
```csharp
public bool HasActiveModifiers => _stickyKeys.Count > 0 || _lockedKeys.Count > 0;
```

아래 프로퍼티 **추가** (기존은 그대로 유지):
```csharp
/// Shift만 활성된 경우는 false. 한국어 쌍자음/쌍모음 입력과 "조합키"를 구분하기 위해 사용.
public bool HasActiveModifiersExcludingShift
{
    get
    {
        bool hasNonShift = false;
        foreach (var vk in _stickyKeys)
        {
            if (vk != VirtualKeyCode.VK_LSHIFT && vk != VirtualKeyCode.VK_RSHIFT && vk != VirtualKeyCode.VK_SHIFT)
            {
                hasNonShift = true;
                break;
            }
        }
        if (!hasNonShift)
        {
            foreach (var vk in _lockedKeys)
            {
                if (vk != VirtualKeyCode.VK_LSHIFT && vk != VirtualKeyCode.VK_RSHIFT && vk != VirtualKeyCode.VK_SHIFT)
                {
                    hasNonShift = true;
                    break;
                }
            }
        }
        return hasNonShift;
    }
}
```

> 실제 `VirtualKeyCode` enum 값 이름은 `AltKey/Models/VirtualKeyCode.cs`에서 확인 후 맞출 것. Shift 변종이 `VK_SHIFT` 하나만 있을 수도 있음.

### 3-2. `KoreanInputModule` 클래스 신규 생성

**파일**: `AltKey/Services/InputLanguage/KoreanInputModule.cs` (신규)

```csharp
using AltKey.Models;

namespace AltKey.Services.InputLanguage;

public sealed class KoreanInputModule : IInputLanguageModule
{
    private readonly InputService     _input;
    private readonly HangulComposer   _composer = new();
    private readonly KoreanDictionary _koDict;
    private readonly EnglishDictionary _enDict;

    private InputSubmode _submode = InputSubmode.HangulJamo;
    private string _englishPrefix = "";   // QuietEnglish 서브모드용 prefix 추적

    public KoreanInputModule(InputService input, KoreanDictionary koDict, EnglishDictionary enDict)
    {
        _input  = input;
        _koDict = koDict;
        _enDict = enDict;
    }

    public string LanguageCode => "ko";
    public InputSubmode ActiveSubmode => _submode;
    public string ComposeStateLabel => _submode == InputSubmode.HangulJamo ? "가" : "A";

    public string CurrentWord => _submode == InputSubmode.HangulJamo
        ? _composer.Current
        : _englishPrefix;

    public event Action<IReadOnlyList<string>>? SuggestionsChanged;

    public bool HandleKey(KeySlot slot, KeyContext ctx) { /* §3-3 */ }

    public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion) { /* §3-4 */ }

    public void ToggleSubmode() { /* §3-5 */ }

    public void OnSeparator() { /* §3-6 */ }

    public void Reset()
    {
        _composer.Reset();
        _englishPrefix = "";
        _submode = InputSubmode.HangulJamo;
        SuggestionsChanged?.Invoke(Array.Empty<string>());
    }
}
```

### 3-3. `HandleKey` — 기존 `HandleKoreanLayoutKey` + `HandleEnglishSubMode` 로직 이전

**의사 코드(실제 코드는 기존 VM에서 줄 단위 복사 + `ctx` 적응)**:

```csharp
public bool HandleKey(KeySlot slot, KeyContext ctx)
{
    // 1) QuietEnglish Submode
    if (_submode == InputSubmode.QuietEnglish)
    {
        return HandleQuietEnglish(slot, ctx);
    }

    // 2) HangulJamo Submode (기본)
    // 2-1) 조합키 판별 — **Shift 제외** (버그 수정 포인트)
    bool isComboKey = ctx.HasActiveModifiersExcludingShift;

    // 2-2) modifier + 알파벳 조합키라면 자모 처리 스킵
    if (isComboKey)
    {
        FinalizeComposition();
        return false;   // HandleAction으로 진행 (Ctrl+C 등)
    }

    // 2-3) 자모 추출
    string? jamo = GetHangulJamoFromSlot(slot, ctx.ShowUpperCase);
    if (jamo is null)
    {
        // 자모 아닌 키(Space/Enter 등) — 분리자 체크는 호출자가 OnSeparator 호출
        return false;
    }

    // 2-4) Unicode 모드: 조합 후 원자적 교체
    if (ctx.InputMode == InputMode.Unicode)
    {
        int prevLen = ctx.TrackedOnScreenLength;
        _composer.Feed(jamo);
        string newOutput = _composer.Current;
        _input.SendAtomicReplace(prevLen, newOutput);
        SuggestionsChanged?.Invoke(_koDict.GetSuggestions(newOutput));
        return true;  // HandleAction 스킵
    }

    // 2-5) VirtualKey 모드: 조합은 OS IME가 함, 자동완성은 하지 않음
    _composer.Feed(jamo);  // 내부 추적만
    SuggestionsChanged?.Invoke(_koDict.GetSuggestions(_composer.Current));
    return false;  // HandleAction이 VK 전송
}

private bool HandleQuietEnglish(KeySlot slot, KeyContext ctx)
{
    // 기존 HandleEnglishSubMode 로직과 동일.
    // slot.EnglishLabel / EnglishShiftLabel을 읽어서 SendUnicode로 전송.
    // modifier 활성 시 스킵 (Ctrl+C 등).
    if (ctx.HasActiveModifiers)
    {
        return false;  // HandleAction으로
    }

    string? letter = ctx.ShowUpperCase
        ? (slot.EnglishShiftLabel ?? slot.EnglishLabel?.ToUpperInvariant())
        : slot.EnglishLabel;
    if (letter is null) return false;

    _input.SendUnicode(letter);
    _englishPrefix += letter;
    SuggestionsChanged?.Invoke(_enDict.GetSuggestions(_englishPrefix));
    return true;
}

private static string? GetHangulJamoFromSlot(KeySlot slot, bool showUpperCase)
{
    // 기존 KeyboardViewModel.GetHangulJamoFromSlot(line 312-319) 그대로 이전.
    // showUpperCase && slot.ShiftLabel is 자모(U+3131~U+3163) → ShiftLabel
    // 아니면 slot.Label이 자모인지 검사
}

private void FinalizeComposition()
{
    if (!_composer.HasComposition && _englishPrefix.Length == 0) return;

    if (_submode == InputSubmode.HangulJamo && _composer.Current.Length > 0)
    {
        _koDict.RecordWord(_composer.Current);
    }
    else if (_submode == InputSubmode.QuietEnglish && _englishPrefix.Length >= 2)
    {
        _enDict.RecordWord(_englishPrefix);
    }

    _composer.Reset();
    _englishPrefix = "";
    SuggestionsChanged?.Invoke(Array.Empty<string>());
}
```

> **알고리즘 보존 원칙**: `SendAtomicReplace`, `HangulComposer.Feed/Backspace/CompositionDepth`의 호출 **순서와 인자**는 기존 `HandleKoreanLayoutKey`와 정확히 동일해야 한다. 눈으로 diff 비교하며 옮길 것.

### 3-4. `AcceptSuggestion`

기존 `AutoCompleteService.AcceptSuggestion`은 `(backspaceCount, fullWord)`를 반환한다. 이 로직을 모듈로 이전.

```csharp
public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
{
    int bsCount;
    if (_submode == InputSubmode.HangulJamo)
    {
        // 기존 AutoCompleteService.AcceptSuggestion의 한글 경로:
        // bsCount = _hangul.CompletedLength + _hangul.CompositionDepth
        bsCount = _composer.CompletedLength + _composer.CompositionDepth;
        _koDict.RecordWord(suggestion);
        _composer.Reset();
    }
    else
    {
        bsCount = _englishPrefix.Length;
        _enDict.RecordWord(suggestion);
        _englishPrefix = "";
    }
    SuggestionsChanged?.Invoke(Array.Empty<string>());
    return (bsCount, suggestion);
}
```

### 3-5. `ToggleSubmode`

```csharp
public void ToggleSubmode()
{
    // 이전 Submode의 조합 상태 플러시
    FinalizeComposition();

    _submode = _submode == InputSubmode.HangulJamo
        ? InputSubmode.QuietEnglish
        : InputSubmode.HangulJamo;

    SuggestionsChanged?.Invoke(Array.Empty<string>());
    // ComposeStateLabel·ActiveSubmode 변경 알림은 PropertyChanged로 KeySlotVm 재평가됨.
    // 하지만 이 클래스는 INotifyPropertyChanged가 아니므로, KeyboardViewModel이 별도 이벤트를 구독하게 한다.
    SubmodeChanged?.Invoke(_submode);
}

public event Action<InputSubmode>? SubmodeChanged;
```

> `SubmodeChanged` 이벤트를 `IInputLanguageModule` 인터페이스에도 추가하거나, `KoreanInputModule` 한정 이벤트로 둔다. 인터페이스에 두는 게 미래 확장에 유리하지만 이번 릴리스는 한국어 전용이므로 어느 쪽이든 허용.

### 3-6. `OnSeparator`

공백/엔터/탭/구두점 등을 누르면 호출된다. 이 시점에 현재 조합 중인 단어를 학습시키고 상태를 리셋.

```csharp
public void OnSeparator() => FinalizeComposition();
```

### 3-7. 백스페이스 처리

기존 `HandleKoreanLayoutKey`에는 백스페이스 분기가 있다. 모듈에도 이를 이식.

```csharp
// HandleKey 내부, VK_BACK 키 감지 시:
if (slot.Action is SendKeyAction { Vk: "VK_BACK" })
{
    if (_submode == InputSubmode.HangulJamo && _composer.HasComposition)
    {
        if (ctx.InputMode == InputMode.Unicode)
        {
            int prevLen = ctx.TrackedOnScreenLength;
            _composer.Backspace();
            _input.SendAtomicReplace(prevLen, _composer.Current);
            SuggestionsChanged?.Invoke(_koDict.GetSuggestions(_composer.Current));
            return true;
        }
        _composer.Backspace();
        SuggestionsChanged?.Invoke(_koDict.GetSuggestions(_composer.Current));
        return false;  // VK가 OS IME로 백스페이스 전송
    }
    if (_submode == InputSubmode.QuietEnglish && _englishPrefix.Length > 0)
    {
        _englishPrefix = _englishPrefix[..^1];
        SuggestionsChanged?.Invoke(_enDict.GetSuggestions(_englishPrefix));
        // Unicode 전송은 호출자가 HandleAction으로 처리하도록 return false.
        return false;
    }
    return false;
}
```

> 정확한 처리는 기존 `KeyboardViewModel`의 백스페이스 분기를 **그대로 이전**할 것. 위는 개요.

### 3-8. DI 등록

**파일**: `AltKey/App.xaml.cs`

```csharp
services.AddSingleton<KoreanInputModule>();
services.AddSingleton<IInputLanguageModule>(sp => sp.GetRequiredService<KoreanInputModule>());
```

### 3-9. "해+ㅆ → 해T" 버그 — 단일 변경 지점

`HandleKey` 내부 조합키 판별을 `ctx.HasActiveModifiersExcludingShift`로 함. 이것이 유일한 **로직 변경**이다. 나머지는 전부 이동.

Sanity check:
- Shift 단독 sticky + VK_T → `HasActiveModifiersExcludingShift == false` → 조합키 아님 → 자모 추출 경로 진입 → `slot.ShiftLabel == "ㅆ"` 반환 → `HangulComposer.Feed("ㅆ")` → 정상.
- Ctrl+Shift+T → `HasActiveModifiersExcludingShift == true` → 조합키 경로 → `FinalizeComposition()` + `return false` → OS에 가상키 전송 → 정상.

---

## 4. 검증

1. 빌드 녹색.
2. 신규 테스트(10에서 자세히)는 일단 다음을 임시 실행:
   ```csharp
   // 임시 스모크 테스트
   var module = new KoreanInputModule(mockInput, mockKoDict, mockEnDict);
   module.HandleKey(ㅎ_slot, ctx);
   module.HandleKey(ㅐ_slot, ctx);
   module.OnSeparator();
   Assert.Equal("해", lastRecordedWord);
   ```
3. VM이 아직 리팩토링 전이어도 런타임에는 영향 없음(모듈은 생성만 되고 호출되지 않음 — 04에서 연결).

---

## 5. 함정 / 주의

- **알고리즘을 고치지 말 것**. 옮기기만. 예외: `isComboKey` → `HasActiveModifiersExcludingShift` 한 줄.
- **`TrackedOnScreenLength` 읽기 타이밍**: `SendAtomicReplace` 호출 *직전*에 읽어야 한다. 기존 코드의 순서를 흐트러뜨리지 말 것.
- **`HangulComposer.Reset` vs `FinalizeComposition`**: Reset은 상태만 비움. Finalize는 **기록 후 비움**. 분리자·Submode 토글에서는 Finalize.
- **`KoreanDictionary.RecordWord` 호출**: 02에서 사전이 직접 `WordFrequencyStore`에 위임하도록 바꿨으므로, 모듈은 사전의 `RecordWord`만 호출한다. `WordFrequencyStore`를 직접 호출하지 않는다.
- **`HasActiveModifiersExcludingShift` 값은 KeyboardViewModel이 채워서 `KeyContext`로 전달**한다. 모듈이 `InputService`에 직접 접근하지 않도록 설계(생성자 의존성은 허용하지만 조합키 판별은 `ctx`를 쓴다). 이유: 테스트 용이성.
- **`InputService` 의존성**: 모듈은 `SendAtomicReplace`·`SendUnicode` 호출을 위해 `InputService`를 받는다. 이는 순수 함수로 떨어뜨리기 어려우므로 허용. 테스트 시에는 `InputService`의 mock 인터페이스가 없으므로 **통합 테스트**로 검증(10 참조).

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/Services/InputService.cs` | 소폭 (`HasActiveModifiersExcludingShift` 추가) |
| `AltKey/Services/InputLanguage/KoreanInputModule.cs` | **신규** |
| `AltKey/App.xaml.cs` | 소폭 (모듈 DI 등록) |

**참고 (읽기 전용)**:
- `AltKey/ViewModels/KeyboardViewModel.cs` — 기존 `HandleKoreanLayoutKey`, `HandleEnglishSubMode`, `GetHangulJamoFromSlot` (복사해 올 원본).
- `AltKey/Services/AutoCompleteService.cs` — 기존 `OnHangulInput`, `OnHangulBackspace`, `AcceptSuggestion` (복사해 올 원본).

---

## 7. 커밋 메시지 초안

```
refactor(ko-only): extract KoreanInputModule, fix 해+ㅆ→해T

- New KoreanInputModule owns HangulComposer, both dictionaries,
  and InputSubmode (HangulJamo / QuietEnglish).
- Logic moved line-by-line from KeyboardViewModel.HandleKoreanLayoutKey
  and HandleEnglishSubMode; algorithm preserved.
- Add InputService.HasActiveModifiersExcludingShift.
- isComboKey now uses HasActiveModifiersExcludingShift so that
  Shift + ㅆ-like keys route through HangulComposer as intended.
```
