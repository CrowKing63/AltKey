# TASK-01 — 제안 수락 직후 이어쓰기 시 제안 전체가 삭제되는 버그

> **심각도**: 높음 (한국어 조사·어미 이어쓰기 시 상시 발생)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) §2, §4.3 반드시 완독
> **영향 범위**: Unicode 모드(비관리자)에서의 HangulJamo / QuietEnglish 서브모드 모두
> **예상 소요**: 30~60분 (수정 자체는 한두 줄, 회귀 방지 테스트가 메인)

---

## 1. 증상

사용자 재현 절차:

1. Unicode 모드로 실행(비관리자). 자동완성 ON.
2. 메모장을 열고 포커스.
3. AltKey에서 `ㅎㅐ` 입력 → 화면에 "해" 표시, 제안에 "해달", "해결"... 뜸.
4. 제안 "해달" 클릭.
5. **곧바로** `ㅎㅏ` 입력.

**기대 결과**: 화면에 "해달하" 또는 곧 "해달하..." 표시.
**실제 결과**: 화면에서 "해달"이 사라지고 "하"만 남는다.

영어 QuietEnglish에서도 동일한 패턴("hello" accept → 곧바로 "w" 입력 → "hello" 사라지고 "w"만 남음).

---

## 2. 근본 원인

`AltKey/ViewModels/SuggestionBarViewModel.cs:60~64`

```csharp
if (_inputService.Mode == InputMode.Unicode)
{
    _inputService.SendAtomicReplace(bsCount, fullWord);
    _inputService.TrackedOnScreenLength = fullWord.Length;
}
```

`SendAtomicReplace` 내부(`AltKey/Services/InputService.cs:323`)에서 이미 `TrackedOnScreenLength = newOutput.Length`를 설정하는데, `SuggestionBarViewModel`이 한 번 더 `fullWord.Length`로 덮어쓴다. **두 값은 같으므로 여기까진 무해.**

문제는 그다음이다. `KoreanInputModule`은 `AcceptSuggestion` 완료 후 아무도 `TrackedOnScreenLength`를 0으로 되돌리지 않는다. 따라서 사용자가 바로 다음 자모를 누르면:

```
KoreanInputModule.HandleKey 내부:
  prevLen = ctx.TrackedOnScreenLength;   // = fullWord.Length (예: 2)
  _composer.Feed(jamo);                  // composer는 Reset된 상태였으므로 newOutput = 1글자 자모
  string newOutput = _composer.Current;  // "ㅎ"
  _input.SendAtomicReplace(2, "ㅎ");     // ← BS 2번 + "ㅎ" 유니코드
```

즉 **화면에 남아 있던 "해달"을 2번 BS로 지우고 "ㅎ"만 찍는다**. 사용자 관점에서는 방금 수락한 제안이 증발한 것처럼 보인다.

핵심은 **제안 수락 직후의 상태 의미론**이다:
- 화면에는 fullWord가 고정 텍스트로 남아 있어야 하고(사용자가 확정한 단어),
- 다음 자모 입력은 **새로운 조합의 시작**(`prevLen = 0`)이어야 한다.

현재 코드는 "fullWord는 아직 조합 중인 텍스트"인 것처럼 TrackedOnScreenLength를 유지한다.

---

## 3. 해결 방향 두 가지

### 방향 A — `KoreanInputModule.AcceptSuggestion` 안에서 리셋 (권장)

모듈이 상태 일관성의 책임을 진다. Reset 타이밍을 한 곳에 모으는 설계가 이미 `FinalizeComposition()` 패턴(`_input.ResetTrackedLength()`)으로 확립되어 있다.

```csharp
// AltKey/Services/InputLanguage/KoreanInputModule.cs
public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
{
    int bsCount;
    if (_submode == InputSubmode.HangulJamo)
    {
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

**변경 지점**: 반환 직전에 **화면 상태 리셋**이 필요함을 호출자에게 맡기는 대신, 모듈이 여기서 `_input.ResetTrackedLength()`를 호출한다.

단, 중요한 타이밍 제약이 있다. `SuggestionBarViewModel`이 아직 `SendAtomicReplace(bsCount, fullWord)`를 호출하기 **전에** 모듈이 `ResetTrackedLength()`를 부르면, `SendAtomicReplace` 내부는 `prevLen`을 외부에서 받으므로 영향 없음. 그러나 `SendAtomicReplace`가 끝나면서 `TrackedOnScreenLength = newOutput.Length` 로 다시 fullWord 길이가 되어버린다. 따라서 모듈에서 리셋해도 호출자가 원상복귀시킨다.

그래서 실제 수정은 **`SuggestionBarViewModel.AcceptSuggestion`에서 `SendAtomicReplace` 호출 후**에 `ResetTrackedLength()`를 한 번 더 호출해야 한다. 또는 `SendAtomicReplace`가 "확정 전송" 모드와 "조합 전송" 모드를 구분하도록 시그니처를 둘로 나눈다.

### 방향 B — `SuggestionBarViewModel`에서 명시적 리셋 (최소 변경)

```csharp
// AltKey/ViewModels/SuggestionBarViewModel.cs
[RelayCommand]
private void AcceptSuggestion(string suggestion)
{
    var (bsCount, fullWord) = _autoComplete.AcceptSuggestion(suggestion);
    if (_inputService.Mode == InputMode.Unicode)
    {
        _inputService.SendAtomicReplace(bsCount, fullWord);
        _inputService.ResetTrackedLength();   // ← 추가
    }
    else
    {
        for (int i = 0; i < bsCount; i++)
            _inputService.SendKeyPress(VirtualKeyCode.VK_BACK);
        if (fullWord.Length > 0)
            _inputService.SendUnicode(fullWord);
    }
}
```

장점: 한 줄만 추가. `SendAtomicReplace`의 "조합 전송 중" 의미론은 건드리지 않는다.
단점: 상태 책임이 VM과 모듈로 분산.

**권장**: 방향 B를 먼저 적용해 현상을 막고, 리팩토링 기회가 있을 때 `SendAtomicReplace`의 시그니처를 "조합 프레임" vs "확정 프레임"으로 분리하는 후속 태스크로 돌리자.

VirtualKey 모드 경로에서도 `bsCount` 개 BS + `SendUnicode(fullWord)` 후에 화면이 확정되므로, Unicode 모드와 동일하게 리셋하는 것이 일관적이다. 단 VirtualKey 모드에선 `TrackedOnScreenLength`가 사용되지 않으므로 영향 없다(0 유지).

---

## 4. 수정 금지 영역 (이 작업 중에도)

- `HangulComposer` 내부 어떤 것도 수정 금지.
- `SendAtomicReplace`의 **단일 `SendInput` 호출** 구조 유지.
- `SendAtomicReplace` 끝의 `ReleaseTransientModifiers()` 호출 제거 금지 (회귀 #2).
- `KoreanInputModule.AcceptSuggestion`의 `bsCount = CompletedLength + CompositionDepth` 계산식 유지 ("문문제" 회귀 방지).
- `InputService.TrackedOnScreenLength`의 setter를 private으로 바꾸지 말 것 (현재 `SuggestionBarViewModel`에서 외부 쓰기가 필요).

---

## 5. 회귀 방지 테스트 (반드시 추가)

`AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs`에 아래 케이스 추가.

```csharp
[Fact]
public void AcceptSuggestion_followed_by_new_jamo_does_not_erase_accepted_word()
{
    var module = CreateModule(out var input);

    // "해" 조합 중
    module.HandleKey(ㅎ_slot, ctxNoModifiers);
    module.HandleKey(ㅐ_slot, ctxNoModifiers);

    // 제안 "해달" 수락 — 모듈의 내부 상태 리셋 확인
    var (bs, word) = module.AcceptSuggestion("해달");
    Assert.Equal(2, bs);
    Assert.Equal("해달", word);
    Assert.Equal("", module.CurrentWord);

    // 다음 자모 "ㅎ" 입력 — Accept 후 TrackedOnScreenLength는 0이어야 함.
    var ctxAfterAccept = new KeyContext(false, false, false, InputMode.Unicode, 0);
    module.HandleKey(ㅎ_slot, ctxAfterAccept);

    // 이 시점 SendAtomicReplace에 전달된 prevLen이 0이어야 한다.
    var last = input.AtomicReplaces.Last();
    Assert.Equal(0, last.prevLen);
    Assert.Equal("ㅎ", last.next);
}
```

`FakeInputService`의 `AtomicReplaces` 큐가 이미 `(prevLen, next)` 튜플을 저장하므로 추가 변경 없이 사용 가능하다.

그리고 통합적으로, **ViewModel 단위 테스트**를 추가할 수도 있다(선택):

```csharp
[Fact]
public void SuggestionBarViewModel_AcceptSuggestion_resets_tracked_length_in_unicode_mode()
{
    // 셋업: FakeInputService + KoreanInputModule + SuggestionBarViewModel
    // 전제: 모드=Unicode, 이전 TrackedOnScreenLength > 0

    // AcceptSuggestion 커맨드 실행

    Assert.Equal(0, inputService.TrackedOnScreenLength);
}
```

---

## 6. 수동 검증 절차

1. 자동완성 ON + Unicode 모드(비관리자).
2. `ㅎㅐ` → "해" 표시 + 제안 패널에 "해달" 등.
3. 제안 "해달" 클릭 → "해달" 표시.
4. 즉시 `ㅎㅏ` 입력 → **"해달하"**로 이어져야 한다. ("하"만 남으면 실패.)
5. Space 입력 → "해달하 " — 확정 및 새 단어 시작.

QuietEnglish 모드에서도 동일 절차로 "hello" accept → "w" 입력 → "hellow" 확인.

---

## 7. 의도적으로 남겨두는 것

- `SendAtomicReplace` 내부의 `TrackedOnScreenLength = newOutput.Length` 설정은 유지. "조합 전송 중" 의미론이므로 VM의 Accept 경로가 추가 리셋하는 쪽이 책임 분리 측면에서 더 명확하다.
- 향후 `SendAtomicReplace`를 `SendCompositionFrame`(조합 중)과 `SendFinalText`(확정)로 분리하는 리팩토링은 별도 태스크로 남긴다. 이번 PR에서는 하지 말 것.

---

## 8. PR 제목·커밋 메시지 초안

```
fix(autocomplete): reset TrackedOnScreenLength after accepting suggestion

- Unicode 모드에서 제안 수락 직후의 자모·알파벳 입력이
  이전 제안을 BS로 지워버리는 회귀 해결.
- SuggestionBarViewModel.AcceptSuggestion에서 SendAtomicReplace 후
  ResetTrackedLength 호출.
- 회귀 방지 테스트 KoreanInputModuleTests에 추가.
```
