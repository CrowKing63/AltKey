# TASK-07 — 회귀 방지 테스트 공백 메우기

> **심각도**: 중간 (코드는 지금 정상 동작하지만, 계약이 고정되어 있지 않아 미래 리팩토링이 위험)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) 전 구역, 특히 §4 "핵심 시퀀스"
> **예상 소요**: 2~3시간 (케이스별 30~40분)

---

## 1. 배경

`docs/refactoring_ToDo/버그 목록.md` 에는 과거에 발생했던 6건의 회귀 버그가 기록되어 있다. 대부분 지금은 수정되어 있지만, **단위 테스트로 고정되지 않은** 항목이 여럿이다. 즉 누군가 `KoreanInputModule`이나 `SendAtomicReplace`를 건드릴 때 같은 버그가 슬그머니 돌아올 수 있다.

이 태스크는 **현재 올바른 동작을 테스트로 박제**하는 것이 목적이다. 새로운 기능 추가 없음.

---

## 2. 공백 목록

### G-1. Accept 직후 자모 재입력 (F-1과 연동, 테스트만 미리 추가해도 OK)

`TASK-01`의 핵심 회귀 테스트. 먼저 이 테스트를 추가해 **현재 실패하는 상태**를 확인한 뒤, F-1 수정 후 녹색이 되는지 검증.

```csharp
[Fact]
public void AcceptSuggestion_then_new_jamo_preserves_accepted_word_on_screen()
{
    var (module, input) = CreateModuleWithInput();
    input.Mode = InputMode.Unicode;

    module.HandleKey(HSlot, CtxUnicode);
    module.HandleKey(AeSlot, CtxUnicode);      // "해" 조합

    var (bs, word) = module.AcceptSuggestion("해달");
    // VM이 할 일 시뮬레이션
    input.SendAtomicReplace(bs, word);
    input.ResetTrackedLength();                // ← F-1 수정 후 VM이 호출할 것

    module.HandleKey(HSlot, CtxUnicode);       // Accept 후 첫 자모

    var last = input.AtomicReplaces.Last();
    Assert.Equal(0, last.prevLen);             // ← 핵심: 이전 제안을 건드리지 말아야 함
    Assert.Equal("ㅎ", last.next);
}
```

### G-2. `ToggleSubmode`로 가/A 전환 중 진행 중 조합의 학습 여부

사용자가 `ㅎㅐ달` 입력 후 `ToggleSubmode`로 영어 모드 전환 시:
- 현재 코드(`KoreanInputModule.ToggleSubmode`)는 `FinalizeComposition`을 호출하는지? 아니면 조합을 그대로 버리는지?
- 테스트가 없어 불명확. **현재 동작을 읽고** 테스트로 고정.

```csharp
[Fact]
public void ToggleSubmode_during_composition_learns_or_discards_consistently()
{
    var (module, input) = CreateModuleWithInput();
    var dict = module.Dict;  // KoreanDictionaryTestable

    module.HandleKey(HSlot, CtxUnicode);
    module.HandleKey(AeSlot, CtxUnicode);
    module.HandleKey(DSlot, CtxUnicode);       // "해ㄷ" — 아직 완성 아님
    // 또는 "해달" 완성 상태를 만든다

    module.ToggleSubmode();

    // 현재 동작 확인 후 Assert:
    //   A) 학습됨: Assert.Contains("해달", dict.GetSuggestions("해", 10));
    //   B) 버려짐: Assert.Empty(dict.GetSuggestions("해", 10));
    // 어느 쪽이든 **현재 동작을 고정**.
}
```

> 이 테스트를 작성하기 전에 먼저 `KoreanInputModule.ToggleSubmode` 소스 코드를 읽고 어떻게 동작하는지 확인한 다음, 그 동작을 테스트로 명시한다. 테스트가 "가이드"가 아니라 "스냅샷" 역할.

### G-3. VirtualKey 모드의 AcceptSuggestion

현재 테스트는 Unicode 모드 시나리오 중심. VirtualKey 모드(관리자)에서는 `SendAtomicReplace` 대신 `SendKeyPress(VK_BACK) × bsCount + SendUnicode(fullWord)` 경로를 탄다.

```csharp
[Fact]
public void AcceptSuggestion_in_VirtualKey_mode_sends_backspaces_then_unicode()
{
    var (module, input) = CreateModuleWithInput();
    input.Mode = InputMode.VirtualKey;
    // VM 경로 시뮬레이션이 필요하므로 SuggestionBarViewModel 레벨 테스트로 작성
    // (또는 VM 없이 VM의 로직을 테스트 내부에서 재현)
}
```

VM 단위 테스트 파일이 없다면 `AltKey.Tests/ViewModels/SuggestionBarViewModelTests.cs` 신설.

### G-4. SendAtomicReplace의 Shift sticky 해제

`InputService.SendAtomicReplace` 내부에 `ReleaseTransientModifiers()` 호출이 있고, 이는 "문문제" 회귀(#2)의 방어책. 테스트로 고정되지 않음.

```csharp
[Fact]
public void SendAtomicReplace_releases_shift_modifier_in_payload()
{
    var input = new RealInputServiceWithFakeSendInput();  // SendInput을 가로채는 테스트 더블
    // Shift를 누른 상태라고 가정
    input.MarkShiftDown();

    input.SendAtomicReplace(2, "가");

    // SendInput 페이로드에 Shift KEYUP이 포함되어야 한다
    Assert.Contains(input.LastInputs, i => i.ki.wVk == (ushort)VirtualKeyCode.LSHIFT
                                            && (i.ki.dwFlags & KEYEVENTF_KEYUP) != 0);
}
```

`InputService`가 현재 정적 `Win32.SendInput`을 직접 호출하므로 테스트를 위해선 `ISendInputPort` 인터페이스 추출 + DI가 필요. 이는 **리팩토링이 필요한 테스트**이므로 이번 태스크에서 가볍게 하려면:

- (대안) `InputService`에 "마지막 SendInput 페이로드"를 공개하는 debug hook 추가 — 단, 이는 프로덕션 코드를 테스트 전용으로 오염시킨다. **권장하지 않음**.
- (대안) 이 테스트는 통합 테스트로 미룬다. 문서에 "Shift sticky 해제 회귀는 수동 검증"으로 기록.

### G-5. BS 후 화면과 Composer 싱크

`KoreanInputModule.HandleBackspace`는 조합 중이면 `_composer.Backspace()`를, 완성 이후면 실제 BS 전송을 하는 분기가 있다. 테스트가 있지만 케이스 커버리지 확인 필요.

```csharp
[Fact]
public void Backspace_during_composition_updates_composer_only()
{
    var (module, input) = CreateModuleWithInput();
    module.HandleKey(HSlot, CtxUnicode);       // ㅎ
    module.HandleKey(AeSlot, CtxUnicode);      // 해
    module.HandleKey(DSlot, CtxUnicode);       // 해ㄷ → (cho=ㄷ 분리됨)

    int beforeBsPressCount = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);
    module.HandleBackspace(CtxUnicode);
    int afterBsPressCount = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);

    // Composer는 1단계 되돌리지만 화면 BS는 Atomic Replace로 처리되어야 함
    Assert.Equal("해", module.CurrentWord);
    Assert.Equal(beforeBsPressCount, afterBsPressCount);  // 직접 VK_BACK 전송은 없어야
}

[Fact]
public void Backspace_after_composition_ended_sends_real_backspace()
{
    var (module, input) = CreateModuleWithInput();
    module.HandleKey(HSlot, CtxUnicode);
    module.HandleKey(AeSlot, CtxUnicode);
    module.OnSeparator();                      // "해" 확정, composer Reset

    int before = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);
    module.HandleBackspace(CtxUnicode);
    int after = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);

    Assert.Equal(before + 1, after);
}
```

### G-6. OnSeparator의 학습 조건

Finalize 경로에서 RecordWord 호출 조건이 정확한지. TASK-03 범위와 겹치지만 **F-3 수정 전 현재 동작**도 테스트로 고정해두면, F-3 수정 시 기존 기대치가 무엇이었는지 Git history로 추적 가능.

```csharp
[Fact]
public void OnSeparator_with_empty_composition_does_not_record()
{
    var (module, _) = CreateModuleWithInput();
    var dict = module.Dict;
    int beforeSize = dict.UserWordCount;
    module.OnSeparator();
    Assert.Equal(beforeSize, dict.UserWordCount);
}

[Fact]
public void OnSeparator_with_multi_syllable_records_user_dictionary()
{
    var (module, _) = CreateModuleWithInput();
    var dict = module.Dict;
    module.HandleKey(HSlot, CtxUnicode);
    module.HandleKey(AeSlot, CtxUnicode);
    module.HandleKey(DSlot, CtxUnicode);
    module.HandleKey(ASlot, CtxUnicode);
    module.HandleKey(LSlot, CtxUnicode);        // "해달"
    module.OnSeparator();
    Assert.Contains("해달", dict.GetSuggestions("해", 5));
}
```

### G-7. 자동완성 OFF 상태에서 제안 이벤트 발행 안 됨

자동완성을 OFF로 한 상태에서 조합해도 `SuggestionsChanged`가 발행되지 않거나 빈 리스트만 와야 한다.

```csharp
[Fact]
public void When_autocomplete_disabled_no_suggestions_emitted()
{
    var (module, _) = CreateModuleWithInput();
    module.AutoCompleteEnabled = false;
    List<IReadOnlyList<string>> events = new();
    module.SuggestionsChanged += s => events.Add(s);

    module.HandleKey(HSlot, CtxUnicode);
    module.HandleKey(AeSlot, CtxUnicode);

    Assert.All(events, s => Assert.Empty(s));
}
```

> 프로퍼티 이름은 현재 구현에 맞게 조정 (`IsSuggestionEnabled` 등일 수 있음).

---

## 3. 테스트 헬퍼 보강

`AltKey.Tests/InputLanguage/TestHelpers.cs`가 이미 있다. 필요 시 다음을 추가:

- `CtxUnicode` / `CtxVirtualKey` / `CtxShiftOnly` 등 **자주 쓰는 KeyContext 프리셋**.
- `CreateModuleWithInput()` 팩토리에서 `(KoreanInputModule, FakeInputService, KoreanDictionaryTestable)` 튜플 반환.
- `HSlot`, `AeSlot`, `DSlot`, `ASlot`, `LSlot` 등 **슬롯 상수** — 자모 키 레이아웃을 매번 만들지 않게.

테스트 가독성이 크게 올라가고 중복이 줄어든다.

---

## 4. 수정 금지 영역

- **프로덕션 코드를 건드리지 말 것**. 이 태스크는 오직 테스트 추가가 목적이다.
- 만약 테스트 작성 중 프로덕션 코드가 "테스트 불가능한 구조"임을 발견해도, 이번 PR에서는 해당 케이스를 **문서에 기록**하고 건너뛴다. 구조 개선은 별도 태스크.
- `TestHelpers`의 기존 시그니처(`CreateModule`, `FakeInputService.AtomicReplaces` 등) 유지. 확장은 추가형으로.

---

## 5. 우선순위

당장 추가해야 하는 것(TASK-01~04 수정 전에):
- G-1 (F-1 회귀 방지)
- G-6 (F-3 수정 전 현재 계약 고정)

순차적으로 추가해도 되는 것:
- G-2, G-5, G-7

장기 리팩토링이 필요한 것(이번 태스크에서 문서만):
- G-4 (SendInput 모킹 인프라 필요)

---

## 6. 수동 검증

테스트 추가이므로 수동 검증 없음. `dotnet test`가 녹색이면 끝.

---

## 7. 커밋 메시지 초안

```
test(korean): lock down current contracts around suggestion/composition

- AcceptSuggestion 직후 자모 재입력 prevLen 검증 (F-1 대비 가드).
- OnSeparator 학습 조건 현재 동작 스냅샷.
- Backspace: 조합 중 vs 조합 종료 후 경로 분기 확인.
- ToggleSubmode 전환 시 현재 학습/폐기 동작 고정.
- 자동완성 OFF 시 제안 이벤트 발행 없음 확인.
- TestHelpers에 자주 쓰는 슬롯·KeyContext 프리셋 추가.
```
