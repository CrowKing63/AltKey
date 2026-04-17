# 05 — `KeyboardViewModel` 정리: 3상태 필드 제거, 단일 분기로 축약

> **소요**: 3~4시간
> **선행**: 04
> **후행**: 06, 07, 08, 09
> **관련 기획**: [refactor-unif-serialized-acorn.md §4-2](../refactor-unif-serialized-acorn.md)

---

## 0. 이 태스크의 목표 한 줄

`KeyboardViewModel`에서 **3상태 필드**(`_isKoreanInput`·`_layoutSupportsKorean`·`_lastImeKorean`)와 3-way 분기(`HandleKoreanLayoutKey`·`HandleEnglishLayoutKey`·`HandleEnglishSubMode`)를 모두 제거하고, 키 입력 경로를 `_autoComplete.OnKey(slot, ctx)` 1회 호출로 축소한다. `KeySlotVm.GetLabel`은 `(upperCase, submode)`의 함수로 확장.

---

## 1. 전제 조건

- 01~04 완료.
- `KoreanInputModule`·`AutoCompleteService.OnKey(slot, ctx)`가 동작.
- `IInputLanguageModule`이 DI에 등록됨.

---

## 2. 현재 상태

### 2-1. `AltKey/ViewModels/KeyboardViewModel.cs`

- `_isKoreanInput` (line 64)
- `_layoutSupportsKorean` (line 67)
- `_lastImeKorean` (line 71)
- `KeyPressed` (line 138)
- `HandleKoreanLayoutKey` (line 165)
- `HandleEnglishLayoutKey` (폐기 대상)
- `HandleEnglishSubMode` (line 242)
- `FinalizeKoreanComposition`
- `UpdateImeState` (line 336-349)
- `KeySlotVm.GetLabel` (line 33 근방)
- `KeySlotVm.IsDimmed` 등 표시 프로퍼티

### 2-2. `LoadLayout` 내부의 한국어 판별

```csharp
_layoutSupportsKorean = layout.Rows.Any(r =>
    r.Keys.Any(k =>
        k.Action is SendKeyAction { Vk: "VK_HANGUL" } ||
        k.HangulLabel is not null));
_isKoreanInput = _layoutSupportsKorean;
```

---

## 3. 작업 내용

### 3-1. 필드 제거

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs`

제거:
- `private bool _isKoreanInput = true;` (line 64)
- `private bool _layoutSupportsKorean;` (line 67)
- `private bool _lastImeKorean = true;` (line 71)
- 이들을 참조하던 모든 get/set·초기화·조건문.

유지:
- `ShowUpperCase` (Shift sticky 표시용).
- `Rows` 같은 렌더링 관련.

### 3-2. `KeyPressed` 축약

**변경 전** (대략):
```csharp
public void KeyPressed(KeySlot slot)
{
    if (!_config.Current.AutoCompleteEnabled)
    {
        _inputService.HandleAction(slot.Action);
        return;
    }

    if (_layoutSupportsKorean)
    {
        if (_isKoreanInput)
        {
            if (HandleKoreanLayoutKey(slot)) return;
        }
        else
        {
            if (HandleEnglishSubMode(slot, _inputService.HasActiveModifiers)) return;
        }
    }
    else
    {
        HandleEnglishLayoutKey(slot);
        return;
    }

    _inputService.HandleAction(slot.Action);
}
```

**변경 후**:
```csharp
public void KeyPressed(KeySlot slot)
{
    // "가/A" 토글 액션은 모듈 토글만 수행
    if (slot.Action is ToggleKoreanSubmodeAction)
    {
        _autoComplete.ToggleKoreanSubmode();
        return;
    }

    // 단어 분리자면 모듈에 신호만
    if (IsSeparatorKey(slot))
    {
        _autoComplete.OnSeparator();
        _inputService.HandleAction(slot.Action);
        return;
    }

    // 일반 키: 모듈에 위임. true면 HandleAction 스킵.
    var ctx = new KeyContext(
        ShowUpperCase:                     ShowUpperCase,
        HasActiveModifiers:                _inputService.HasActiveModifiers,
        HasActiveModifiersExcludingShift:  _inputService.HasActiveModifiersExcludingShift,
        InputMode:                         _inputService.Mode,
        TrackedOnScreenLength:             _inputService.TrackedOnScreenLength);

    bool handled = _autoComplete.OnKey(slot, ctx);
    if (!handled) _inputService.HandleAction(slot.Action);
}

private static bool IsSeparatorKey(KeySlot slot) => slot.Action switch
{
    SendKeyAction { Vk: "VK_SPACE" }  => true,
    SendKeyAction { Vk: "VK_RETURN" } => true,
    SendKeyAction { Vk: "VK_TAB" }    => true,
    _ => false,
};
```

### 3-3. 메서드 제거

다음을 전부 **삭제**:
- `HandleKoreanLayoutKey(...)`
- `HandleEnglishLayoutKey(...)`
- `HandleEnglishSubMode(...)`
- `FinalizeKoreanComposition(...)` — 이제 `_autoComplete.OnSeparator()` 가 대신.
- `GetHangulJamoFromSlot(...)` — 모듈로 이전됨.
- `ShouldSkipHandleAction(...)` (있다면).

### 3-4. `UpdateImeState` 처리

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs` (line 336-349)

기존 로직은 `VirtualKey` 모드에서만 IMM32 폴링을 돌린다. 리팩토링 후:

- **Unicode 모드**: 이미 폴링 가드가 있음. 유지.
- **VirtualKey 모드**: `_lastImeKorean`이 제거됐으므로 이 폴링이 이제 무엇을 한 번지? — `UpdateImeState`가 내부 `_isKoreanInput`을 갱신하는 유일한 목적이었다면 **메서드 전체를 제거**.
  - 단, `_autoComplete.CompleteCurrentWord()`가 IME 변경 시 호출되는 역할은 유지할 가치가 있을 수 있음. 만약 그렇다면 `UpdateImeState`는 남겨두되 `_isKoreanInput` 업데이트 라인만 제거.
- 기획 문서 §4-2에 "`UpdateImeState()`는 관리자 모드 전용으로 남김(현상 유지)"라고 되어 있음. 따라서 **VirtualKey 모드 전용 폴링은 유지**하되, 한국어 모듈에 대한 `ResetState`/`OnSeparator` 호출만 남기고 나머지 내부 상태 의존은 제거.

최종 판단: 단순화 우선이라면 `UpdateImeState` 전체를 제거하는 것도 허용. 결정은 구현자.

### 3-5. `KeySlotVm.GetLabel` 확장

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs` (line 33 근방)

기존 시그니처는 `GetLabel(bool upperCase)`일 가능성. 이를 `GetLabel(bool upperCase, InputSubmode submode)`로 확장.

의사 코드:
```csharp
public sealed class KeySlotVm : ObservableObject
{
    public KeySlot Slot { get; }

    public string GetLabel(bool upperCase, InputSubmode submode)
    {
        // QuietEnglish 상태에서는 EnglishLabel을 메인 라벨로
        if (submode == InputSubmode.QuietEnglish && Slot.EnglishLabel is { Length: > 0 } eng)
        {
            string baseLabel = upperCase
                ? (Slot.EnglishShiftLabel ?? eng.ToUpperInvariant())
                : eng;
            return baseLabel;
        }

        // HangulJamo 상태(기본): 기존 로직
        return upperCase && Slot.ShiftLabel is { Length: > 0 } s
            ? s
            : Slot.Label;
    }

    public bool GetIsDimmed(InputSubmode submode)
        => submode == InputSubmode.QuietEnglish && Slot.EnglishLabel is null;

    // "가/A" 토글 키 — 모듈의 ComposeStateLabel을 라벨로 사용
    public bool IsKoreanSubmodeToggle => Slot.Action is ToggleKoreanSubmodeAction;
}
```

> 바인딩: XAML에서 `Text="{Binding DisplayLabel}"` 형태로 렌더링한다면, `DisplayLabel` 같은 계산 프로퍼티를 두고 `ShowUpperCase`와 `AutoCompleteService.ActiveSubmode` 양쪽의 변경 이벤트를 구독해 `OnPropertyChanged(nameof(DisplayLabel))` 발행.
> "가/A" 토글 키의 경우 `DisplayLabel` 계산에서 `IsKoreanSubmodeToggle` 체크 후 `_autoComplete.ComposeStateLabel`을 반환.

### 3-6. Submode 변경 이벤트 구독

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs`

```csharp
public KeyboardViewModel(..., AutoCompleteService autoComplete, ...)
{
    _autoComplete = autoComplete;
    // Submode 변경 시 모든 KeySlotVm의 DisplayLabel 재계산
    if (autoComplete is INotifyPropertyChanged npc)
    {
        npc.PropertyChanged += OnAutoCompletePropertyChanged;
    }
    // 또는 AutoCompleteService에 별도의 SubmodeChanged 이벤트를 노출하고 구독.
}

private void OnSubmodeChanged(InputSubmode submode)
{
    foreach (var row in Rows)
        foreach (var keyVm in row.Keys)
            keyVm.RefreshDisplay();  // DisplayLabel · IsDimmed 재평가
}
```

> `AutoCompleteService`에 `event Action<InputSubmode>? SubmodeChanged`를 노출. `KoreanInputModule.SubmodeChanged`를 프록시해서 재발행.

### 3-7. `LoadLayout` 단순화

```csharp
public void LoadLayout(LayoutConfig layout)
{
    Rows.Clear();
    // ... 기존의 KeySlotVm 생성 루프 ...
    _autoComplete.ResetState();   // Submode·조합 상태 초기화
}
```

- `_layoutSupportsKorean`, `_isKoreanInput`, `_lastImeKorean` 초기화 라인 전부 제거.

### 3-8. `_inputService.ElevatedAppDetected` 등 주변 이벤트

기획에는 언급 없음. 기존 동작 유지. 이벤트 구독이 `_isKoreanInput`을 건드렸다면 해당 라인만 제거.

---

## 4. 검증

1. 빌드 녹색.
2. 런타임:
   - qwerty-ko 로딩 → 키 라벨이 한글 자모로 표시(기본 Submode HangulJamo).
   - 한글 자모 타자 → 정상 조합·자동완성 표시.
   - "가/A" 키 누름(07 태스크 전이므로 액션이 아직 없다면 이 단계에서 스모크 확인만).
3. Shift sticky + ㅆ 입력 → "ㅆ"가 정상 표시(T 아님). 03 버그 수정의 end-to-end 검증.
4. 관리자 권한으로 실행 → VirtualKey 모드 → 기존 동작 유지(OS IME가 조합).
5. 기존 `HandleEnglishLayoutKey` 호출 경로가 전부 사라졌는지 grep:
   ```
   grep -n "HandleEnglishLayoutKey\|HandleKoreanLayoutKey\|HandleEnglishSubMode\|_isKoreanInput\|_layoutSupportsKorean\|_lastImeKorean" AltKey/ViewModels/KeyboardViewModel.cs
   ```
   → 0건.

---

## 5. 함정 / 주의

- **이벤트 미구독으로 라벨 고정**: Submode 변경 시 모든 키의 `DisplayLabel`이 재평가되어야 한다. `RefreshDisplay` 호출 누락 주의.
- **DI 순환**: `KoreanInputModule`이 `InputService`를 받고, `KeyboardViewModel`이 `AutoCompleteService`를 받고, `AutoCompleteService`가 `IInputLanguageModule`을 받는 구조. `KoreanInputModule`은 `InputService` 외에 다른 VM·서비스를 참조해서는 안 된다.
- **`IsSeparatorKey`와 `OnSeparator` 이중 호출**: 분리자 키를 처리한 뒤 `HandleAction`도 호출해야 한다(공백 문자를 실제로 전송). `_autoComplete.OnSeparator()` 먼저 → `_inputService.HandleAction(slot.Action)` 나중.
- **Unicode 모드에서 공백 전송**: 기존 코드가 공백을 어떻게 보냈는지 확인. `HandleAction`이 `SendKeyAction` VK_SPACE를 Unicode 모드에서도 올바르게 처리해야 한다. 기존 동작을 변경하지 말 것.
- **`ShowUpperCase` 업데이트**: Sticky/Locked 상태 변경 시 `ShowUpperCase`가 갱신되고, 이에 따라 키 라벨이 재평가되는 기존 로직은 유지. `GetLabel` 확장 시 `ShowUpperCase`와 `Submode` 두 축이 모두 트리거되도록.

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/ViewModels/KeyboardViewModel.cs` | **대폭 수정** |
| `AltKey/Services/AutoCompleteService.cs` | 소폭 (`SubmodeChanged` 이벤트 재발행) |
| `AltKey/Services/InputLanguage/KoreanInputModule.cs` | 소폭 (`SubmodeChanged` 이벤트 공개) |

---

## 7. 커밋 메시지 초안

```
refactor(ko-only): collapse KeyboardViewModel to a single OnKey path

- Remove _isKoreanInput, _layoutSupportsKorean, _lastImeKorean.
- KeyPressed now delegates to AutoCompleteService.OnKey(slot, ctx).
- HandleKoreanLayoutKey / HandleEnglishLayoutKey / HandleEnglishSubMode
  removed; logic lives in KoreanInputModule.
- KeySlotVm.GetLabel takes (upperCase, submode); DisplayLabel recomputes
  on SubmodeChanged.
- LoadLayout no longer computes a "supports Korean" flag.
```
