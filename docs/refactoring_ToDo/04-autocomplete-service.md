# 04 — `AutoCompleteService` 단일 `OnKey()`로 축약

> **소요**: 1~2시간
> **선행**: 03
> **후행**: 05
> **관련 기획**: [refactor-unif-serialized-acorn.md §4-1](../refactor-unif-serialized-acorn.md)

---

## 0. 이 태스크의 목표 한 줄

`AutoCompleteService`가 직접 한글/영문을 분기 처리하던 것을 **`KoreanInputModule`에 위임**만 하게 축약. 외부 API는 유지하되 내부는 한 모듈로의 얇은 파사드로 만든다.

---

## 1. 전제 조건

- 03 태스크 완료: `KoreanInputModule` 동작함.
- `KoreanDictionary`·`EnglishDictionary`가 02에서 분리됨.

---

## 2. 현재 상태

### 2-1. `AltKey/Services/AutoCompleteService.cs` (131줄)

주요 공개 API:
- `string CurrentWord { get; }`
- `event Action<IReadOnlyList<string>>? SuggestionsChanged`
- `void OnHangulInput(string jamo)`
- `void OnHangulBackspace()`
- `void OnKeyInput(VirtualKeyCode vk)`
- `(int, string) AcceptSuggestion(string suggestion)`
- `void CompleteCurrentWord()`
- `void ResetState()`

내부:
- `HangulComposer _hangul`
- `string _currentWord`
- `bool _isHangulMode`
- `KoreanDictionary _koreanDict`
- `EnglishDictionary _englishDict`
- `WordFrequencyStore _store`

### 2-2. 호출자

- `AltKey/ViewModels/KeyboardViewModel.cs`
- `AltKey/ViewModels/SuggestionBarViewModel.cs` (`AcceptSuggestion`)
- `AltKey/Services/InputService.cs`의 `SetAutoComplete()` (App.xaml.cs에서 호출)

---

## 3. 작업 내용

### 3-1. `AutoCompleteService` 재설계

**파일**: `AltKey/Services/AutoCompleteService.cs`

신규 시그니처:

```csharp
using AltKey.Models;
using AltKey.Services.InputLanguage;

namespace AltKey.Services;

public sealed class AutoCompleteService
{
    private readonly IInputLanguageModule _module;

    public AutoCompleteService(IInputLanguageModule module)
    {
        _module = module;
        _module.SuggestionsChanged += list => SuggestionsChanged?.Invoke(list);
    }

    public string CurrentWord => _module.CurrentWord;

    public event Action<IReadOnlyList<string>>? SuggestionsChanged;

    /// KeyboardViewModel.KeyPressed가 호출.
    /// true면 호출자가 HandleAction 스킵.
    public bool OnKey(KeySlot slot, KeyContext ctx) => _module.HandleKey(slot, ctx);

    /// 자동완성 제안 수락.
    public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
        => _module.AcceptSuggestion(suggestion);

    /// 공백/엔터/탭 등 단어 분리자 도달 시 호출.
    public void OnSeparator() => _module.OnSeparator();

    /// 레이아웃 전환·"가/A" 토글 등으로 상태 초기화가 필요할 때.
    public void ResetState() => _module.Reset();

    /// 과거 호환용 — 현재 조합을 학습시키고 상태를 flush.
    /// 내부적으로 OnSeparator와 동일. 과거 호출자 유지 목적.
    public void CompleteCurrentWord() => _module.OnSeparator();

    /// Submode 토글을 외부에 노출 — "가/A" 버튼 액션에서 사용.
    /// 인터페이스가 노출 않는 경우 KoreanInputModule에 직접 접근.
    public void ToggleKoreanSubmode() => _module.ToggleSubmode();

    public InputSubmode ActiveSubmode => _module.ActiveSubmode;
    public string ComposeStateLabel => _module.ComposeStateLabel;
}
```

> **원칙**: 필드로 모듈 하나만 보유. 상태 보유 금지. 모든 메서드는 모듈에 위임.

### 3-2. 제거할 것들

- `OnHangulInput`, `OnHangulBackspace`, `OnKeyInput` (구 시그니처).
  - 호출자는 05에서 전부 `OnKey(slot, ctx)` 또는 `OnSeparator()`로 교체.
- `_hangul`, `_currentWord`, `_isHangulMode`, `_koreanDict`, `_englishDict`, `_store` 필드.
- `VkToChar`, `IsWordSeparator` 같은 내부 헬퍼(있다면 제거 또는 `KoreanInputModule`/`KeyboardViewModel`로 이동).

### 3-3. `SuggestionBarViewModel.cs` 호환 확인

**파일**: `AltKey/ViewModels/SuggestionBarViewModel.cs`

기존 `AcceptSuggestion` 구현을 보자:

```csharp
// 기존 (대략)
var (bsCount, fullWord) = _autoComplete.AcceptSuggestion(suggestion);
if (_inputService.Mode == InputMode.Unicode)
{
    int onScreenLen = _autoComplete.CurrentWord.Length;   // ← AcceptSuggestion 호출 후엔 ""
    _inputService.SendAtomicReplace(onScreenLen, fullWord);
}
else
{
    for (int i = 0; i < bsCount; i++)
        _inputService.SendKeyPress(VirtualKeyCode.VK_BACK);
    _inputService.SendUnicode(fullWord);
}
```

**이슈**: `CurrentWord`를 `AcceptSuggestion` 호출 **후에** 읽으면 이미 리셋되어 0이다. 기존 구현이 이를 어떻게 다루고 있는지 확인 후, 모듈이 반환하는 `bsCount`가 **Unicode 모드에서도 올바른 원자 교체 길이**인지 검증.

해결안:
- `AcceptSuggestion`이 `(int bsCount, string fullWord)` 반환. `bsCount`는 이전 조합 전체를 지우기 위한 값(Unicode 모드에서는 `TrackedOnScreenLength`와 같아야 함).
- Unicode 모드: `SendAtomicReplace(bsCount, fullWord)` — `prevLen` 자리에 `bsCount`를 넘김.
- VirtualKey 모드: `for(i<bsCount) SendKeyPress(VK_BACK); SendUnicode(fullWord);`

### 3-4. `InputService.SetAutoComplete()` 호출 처리

**파일**: `AltKey/App.xaml.cs`

`InputService`가 자동완성을 아는 API가 있다면(`SetAutoComplete(autoComplete)`), 이 메서드의 책임을 확인:
- 원래 용도: `HandleAction` 내부에서 자동완성 호출을 위해.
- 03 리팩토링 이후: 자동완성 호출은 `KeyboardViewModel`이 `OnKey(slot, ctx)`로 통일하므로 **`InputService`는 자동완성을 알 필요가 없다**.
- 따라서 `SetAutoComplete()` 호출을 App.xaml.cs에서 제거하고, `InputService`에서도 해당 메서드·필드를 제거할 수 있는지 확인.

> 제거가 다른 호출자를 깨뜨리면 일단 두고 09 태스크에서 정리.

### 3-5. DI 등록

**파일**: `AltKey/App.xaml.cs`

```csharp
// 현재 (예상)
services.AddSingleton<WordFrequencyStore>();   // (02에서 제거됨)
services.AddSingleton<KoreanDictionary>();
services.AddSingleton<EnglishDictionary>();
services.AddSingleton<AutoCompleteService>();  // (시그니처 변경됨)

// 변경
services.AddSingleton<Func<string, WordFrequencyStore>>(_ => lang => new WordFrequencyStore(lang));
services.AddSingleton<KoreanDictionary>();
services.AddSingleton<EnglishDictionary>();
services.AddSingleton<KoreanInputModule>();
services.AddSingleton<IInputLanguageModule>(sp => sp.GetRequiredService<KoreanInputModule>());
services.AddSingleton<AutoCompleteService>();
```

### 3-6. 컴파일 정리

`AutoCompleteService`의 내부 헬퍼(`VkToChar`, `IsWordSeparator`)를 제거한 뒤, `KeyboardViewModel.KeyPressed`에서 분리자 판별을 어디서 할지 결정.

제안: `KeyboardViewModel.KeyPressed`에서:
```csharp
if (IsSeparatorKey(slot))
{
    _autoComplete.OnSeparator();
}
else
{
    bool handled = _autoComplete.OnKey(slot, ctx);
    if (!handled) _inputService.HandleAction(slot.Action);
}
```

`IsSeparatorKey`는 slot의 액션이 `VK_SPACE`/`VK_RETURN`/`VK_TAB` 등인지 보는 간단한 함수. 05에서 정리.

---

## 4. 검증

1. 빌드 녹색.
2. 기존 호출자(`KeyboardViewModel`·`SuggestionBarViewModel`)가 새 API(`OnKey`, `OnSeparator`)로 빌드되는지 확인.
3. 런타임에서:
   - qwerty-ko 로딩 후 한글 자모 몇 개 타자.
   - 자동완성 제안 표시 확인.
   - 제안 클릭 → 본문에 단어 완성.

---

## 5. 함정 / 주의

- **`_isHangulMode` 완전 제거**: 이 필드가 `AutoCompleteService`에 남아 있으면 상태 중복.
- **`CurrentWord` 의미**: 모듈에 위임하므로 외부 코드가 `AutoCompleteService.CurrentWord`를 읽으면 현재 Submode에 맞는 prefix(한글 `Current` 또는 영문 prefix)가 반환된다. 호출자 중 Unicode 모드 계산에서 이를 쓰는 곳이 있는지 확인(특히 `SuggestionBarViewModel`).
- **이벤트 체이닝**: 모듈의 `SuggestionsChanged`를 구독해 외부에 재발행하므로, 이벤트 구독 해제(`Dispose`) 누락에 주의. 싱글톤이라 생명주기는 앱과 동일하므로 실용상 문제 없음.

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/Services/AutoCompleteService.cs` | **대폭 단순화** |
| `AltKey/App.xaml.cs` | 수정 (DI 등록) |
| `AltKey/ViewModels/SuggestionBarViewModel.cs` | 소폭 (API 매핑 확인) |
| `AltKey/Services/InputService.cs` | 확인 (가능하면 SetAutoComplete 제거) |

---

## 7. 커밋 메시지 초안

```
refactor(ko-only): AutoCompleteService is now a thin facade

- Remove _isHangulMode, _currentWord, _hangul, _koreanDict, _englishDict.
- Delegate everything to IInputLanguageModule (KoreanInputModule).
- New single entrypoint: OnKey(slot, ctx).
- OnHangulInput/OnKeyInput/OnHangulBackspace removed (callers migrated).
- InputService no longer knows about autocomplete (SetAutoComplete removed).
```
