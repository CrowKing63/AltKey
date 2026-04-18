# 10 — 테스트 & 수동 검증 체크리스트

> **소요**: 2~3시간(테스트 작성) + 1시간(수동 검증)
> **선행**: 01~09 전부
> **후행**: 없음(릴리스 준비 완료)
> **관련 기획**: [refactor-unif-serialized-acorn.md §6-3, §8](../refactor-unif-serialized-acorn.md)

---

## 0. 이 태스크의 목표 한 줄

**"해+ㅆ → 해T" 회귀가 다시 돌아오지 않도록** 단위 테스트를 못박고, 엔드투엔드 수동 체크리스트로 릴리스 품질을 보증.

---

## 1. 전제 조건

- 01~09 완료. 빌드·런타임 정상.
- `AltKey.Tests` 프로젝트 존재. xUnit 기반.

---

## 2. 신규 단위 테스트

### 2-1. `AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs`

**신규 파일**. `AltKey.Tests.csproj`에 폴더가 없다면 생성.

테스트 케이스:

```csharp
using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using Xunit;

namespace AltKey.Tests.InputLanguage;

public class KoreanInputModuleTests
{
    private KoreanInputModule CreateModule(out FakeInputService input)
    {
        input = new FakeInputService();
        var koStore = new WordFrequencyStoreInMemory();
        var enStore = new WordFrequencyStoreInMemory();
        var koDict = new KoreanDictionary(_ => koStore);
        var enDict = new EnglishDictionary(_ => enStore);
        return new KoreanInputModule(input, koDict, enDict);
    }

    [Fact]
    public void Feed_해_해_separator_records_해()
    {
        var module = CreateModule(out var input);
        var ctx = new KeyContext(false, false, false, InputMode.Unicode, 0);

        module.HandleKey(ㅎ_slot, ctx);
        module.HandleKey(ㅐ_slot, ctx);
        Assert.Equal("해", module.CurrentWord);

        module.OnSeparator();
        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void 해_plus_Shift_ㅆ_feeds_ssang_siot_not_T()   // ★ 회귀 방지
    {
        var module = CreateModule(out var input);
        var ctxNoShift = new KeyContext(false, false, false, InputMode.Unicode, 0);

        module.HandleKey(ㅎ_slot, ctxNoShift);
        module.HandleKey(ㅐ_slot, ctxNoShift);

        // Shift sticky 활성 상태 — HasActiveModifiers=true지만 ExcludingShift=false
        var ctxShiftOnly = new KeyContext(true, true, false, InputMode.Unicode, 1);

        // "ㅆ" 자모가 ShiftLabel로 정의된 키 슬롯
        module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctxShiftOnly);

        // "해" 다음에 "ㅆ"이 초성으로 들어가 새 음절 시작
        Assert.Contains("ㅆ", module.CurrentWord);
        Assert.DoesNotContain("T", module.CurrentWord);
    }

    [Fact]
    public void Ctrl_Shift_T_is_not_a_jamo_path()
    {
        var module = CreateModule(out var input);
        var ctx = new KeyContext(true, true, true, InputMode.Unicode, 0);

        bool handled = module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctx);
        Assert.False(handled);   // 자모 경로 타지 않고 false 반환 → HandleAction이 Ctrl+Shift+T 전송
    }

    [Fact]
    public void ToggleSubmode_flushes_hangul_and_switches_to_quiet_english()
    {
        var module = CreateModule(out _);
        var ctx = new KeyContext(false, false, false, InputMode.Unicode, 0);

        module.HandleKey(ㅎ_slot, ctx);
        module.HandleKey(ㅐ_slot, ctx);
        module.ToggleSubmode();
        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
        Assert.Equal("A", module.ComposeStateLabel);
        Assert.Equal("", module.CurrentWord);   // flush됨
    }

    [Fact]
    public void QuietEnglish_mode_feeds_english_prefix_and_sends_unicode()
    {
        var module = CreateModule(out var input);
        var ctx = new KeyContext(false, false, false, InputMode.Unicode, 0);
        module.ToggleSubmode();   // HangulJamo → QuietEnglish

        module.HandleKey(q_slot_with_english_label_q, ctx);
        module.HandleKey(u_slot_with_english_label_u, ctx);
        Assert.Equal("qu", module.CurrentWord);
        Assert.Equal(new[] { "q", "u" }, input.SentUnicodes);
    }

    [Fact]
    public void AcceptSuggestion_in_HangulJamo_returns_correct_bsCount()
    {
        var module = CreateModule(out _);
        var ctx = new KeyContext(false, false, false, InputMode.Unicode, 0);
        module.HandleKey(ㅎ_slot, ctx);
        module.HandleKey(ㅐ_slot, ctx);
        // CompositionDepth=2, CompletedLength=0 → bs=2
        var (bs, word) = module.AcceptSuggestion("해달");
        Assert.Equal(2, bs);
        Assert.Equal("해달", word);
    }

    // ... 겹받침(닭), 자모 재초성 이동(ㄱ+ㅏ+ㅇ+ㅣ → "가"+"이") 등 HangulComposer 경유 케이스
}

// --- 테스트 도움 클래스들 ---

internal sealed class FakeInputService : InputService
{
    public List<string> SentUnicodes { get; } = new();
    public override void SendUnicode(string text) => SentUnicodes.Add(text);
    public override void SendAtomicReplace(int prev, string next) { /* 기록만 */ }
    // 필요한 만큼만 오버라이드; 진짜 SendInput은 호출하지 않도록.
}

internal sealed class WordFrequencyStoreInMemory : WordFrequencyStore
{
    public WordFrequencyStoreInMemory() : base("test") { }
    // Save/Load 오버라이드로 파일 I/O 차단
}
```

> `InputService`가 봉인 클래스거나 `SendUnicode`가 가상이 아니라면 **테스트를 위해 가상 메서드로 전환**하거나, 혹은 `IInputService` 인터페이스를 추출해서 모듈이 이를 받도록. 설계 복잡도가 과하면 **테스트에서는 실제 `InputService`를 쓰되 SendInput이 호출되지 않는 오버라이드 플래그**를 두는 실용적 접근 허용.

### 2-2. `AltKey.Tests/Services/InputServiceTests.cs` (신규 또는 기존 보강)

```csharp
[Fact]
public void HasActiveModifiersExcludingShift_false_when_only_shift_sticky()
{
    var svc = new InputService();
    svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
    Assert.True(svc.HasActiveModifiers);
    Assert.False(svc.HasActiveModifiersExcludingShift);
}

[Fact]
public void HasActiveModifiersExcludingShift_true_when_ctrl_sticky()
{
    var svc = new InputService();
    svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
    Assert.True(svc.HasActiveModifiersExcludingShift);
}

[Fact]
public void HasActiveModifiersExcludingShift_true_when_ctrl_and_shift_sticky()
{
    var svc = new InputService();
    svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
    svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
    Assert.True(svc.HasActiveModifiersExcludingShift);
}
```

### 2-3. `AltKey.Tests/HangulComposerTests.cs` (회귀 확인)

기존 테스트가 전부 통과하는지 확인. 추가 케이스:
- `ㅎ+ㅐ+ㅆ` (Shift로 입력한 ㅆ) 시나리오에서 CompositionDepth가 어떻게 되는지 — 현재 구현이 ㅆ을 종성 쌍시옷으로 받는지, 새 음절 초성으로 받는지 확인.

---

## 3. 수동 검증 체크리스트

### 3-1. 빌드 & 시작

- [ ] `dotnet build` 녹색.
- [ ] `dotnet test` 녹색(신규 테스트 포함).
- [ ] 포터블 실행: `dotnet run --project AltKey` 또는 발행물 실행. 창이 정상 렌더링.

### 3-2. 기본 한글 조합 (Unicode 모드, 자동완성 ON)

- [ ] 자동완성 토글 ON.
- [ ] 메모장 포커스.
- [ ] "ㅎ+ㅐ" → "해" 정상.
- [ ] "ㄷ+ㅏ+ㄺ" → "닭" 정상.
- [ ] "ㅇ+ㅏ+ㄴ+ㄴ+ㅕ+ㅇ" → "안녕" 정상.
- [ ] 조합 중 백스페이스 → CompositionDepth 기반 원자 교체 정상.

### 3-3. ★ "해+ㅆ → 해ㅆ" 회귀 테스트

- [ ] "ㅎ+ㅐ" → "해" 조합.
- [ ] Shift sticky 켜고 VK_T(자판 위치 ㅆ/ㅅ) 누름.
- [ ] **"해ㅆ"**로 표시되어야 함(T 아님).
- [ ] 메모장에 실제로 "해ㅆ" 혹은 "핐" 같은 조합 결과 확인.

### 3-4. 쌍자음/쌍모음 전체

- [ ] ㅃ (Shift + VK_Q) → 조합 가능.
- [ ] ㅉ (Shift + VK_W) → 조합 가능.
- [ ] ㄸ (Shift + VK_E) → 조합 가능.
- [ ] ㄲ (Shift + VK_R) → 조합 가능.
- [ ] ㅆ (Shift + VK_T) → 조합 가능.
- [ ] ㅒ (Shift + VK_O) → 조합 가능.
- [ ] ㅖ (Shift + VK_P) → 조합 가능.

### 3-5. "가/A" 토글

- [ ] 초기 상태: "가" 버튼 표시, 키 라벨이 한글 자모.
- [ ] "가" 클릭 → 버튼 "A"로 변경, 키 라벨이 알파벳으로 전환.
- [ ] 이 상태에서 `apple` 타자 → 메모장에 `apple` 표시, 영어 자동완성 제안 출현.
- [ ] "A" 클릭 → "가"로 복귀, 한글 자모 라벨 복귀.
- [ ] 토글 시 이전 조합이 남지 않음(조합 중 "ㄱㅏ" 상태에서 토글 → 조합이 깔끔하게 완료·flush).

### 3-6. 상단바 OS IME 한/영 버튼

- [ ] 클릭 → 작업표시줄 IME 인디케이터(EN↔KO) 토글.
- [ ] AltKey 내부 "가/A" 라벨·Submode는 변화 없음.
- [ ] Narrator: "OS IME 한영 전환 신호 전송됨" 낭독.

### 3-7. 자동완성 토글 (Unicode ↔ VirtualKey)

- [ ] 자동완성 ON 상태에서 한글 조합 → Unicode로 정상 입력.
- [ ] 자동완성 OFF → 토글 후 한글 조합 → VirtualKey 전송, OS IME가 조합.
- [ ] 제안 바가 OFF 시 숨겨짐.
- [ ] 토글 전환 시 이전 조합 상태가 리셋.

### 3-8. 관리자 모드

- [ ] 관리자 권한으로 실행.
- [ ] 자동완성 토글 버튼 비활성화(disabled).
- [ ] ToolTip/HelpText "관리자 모드에서는..." 표시.
- [ ] 한글 조합은 VirtualKey 모드로 정상(OS IME 의존).

### 3-9. 접근성 (Narrator)

- [ ] Ctrl+Win+Enter로 Narrator 활성.
- [ ] Tab으로 상단바 버튼 순회 → 각 버튼 이름과 도움말 낭독.
- [ ] 키 포커스 → 자모 키는 "비읍", "이응" 등 낭독. 알파벳 키는 "Q 키".
- [ ] "가/A" 토글 포커스 → 현재 Submode에 맞는 상태형 문장 낭독.
- [ ] "가/A" 클릭 → LiveRegion 공지 낭독.
- [ ] 상단바 한/영 클릭 → LiveRegion 공지 낭독.

### 3-10. 사용자 데이터 학습

- [ ] 몇 개의 한글 단어 타자 + 공백 → `user-words.ko.json` 생성/갱신 확인.
- [ ] QuietEnglish 모드에서 영어 단어 타자 + 공백 → `user-words.en.json` 생성/갱신 확인.
- [ ] 재실행 후 학습된 단어가 자동완성 제안에 노출.
- [ ] 기존 `user-words.json`이 남아 있어도 무시(로드 안 됨).

### 3-11. 레이아웃·설정 유지

- [ ] 레이아웃 목록에 `qwerty-ko`만 표시(+ 사용자 커스텀).
- [ ] 테마·Dwell·Sticky 등 기존 기능 정상.
- [ ] 창 위치·크기 저장 정상.

---

## 4. 릴리스 전 종합 체크

- [ ] `dotnet publish AltKey/AltKey.csproj -r win-x64 --self-contained -c Release` 성공.
- [ ] 단일 EXE 실행 가능.
- [ ] Git 로그 커밋 메시지 정리.
- [ ] `docs/refactoring_ToDo/`의 모든 체크박스 완료.
- [ ] `TODO List.md` 갱신.
- [ ] 릴리스 노트 작성(v0.3).

---

## 5. 발견된 이슈 기록 템플릿

검증 중 발견한 회귀·버그는 여기 기록 후 티켓으로 분리:

```markdown
### [심각도] 제목
- 재현: ...
- 기대: ...
- 실제: ...
- 우선순위: 릴리스 블로커 / 후속
```

---

## 6. 커밋 메시지 초안 (테스트 코드)

```
test(ko-only): add KoreanInputModule regression tests

- 해+ㅆ→해ㅆ guard test (was: 해T).
- HasActiveModifiersExcludingShift truth-table.
- ToggleSubmode flush behavior.
- QuietEnglish prefix + SendUnicode trace.
- AcceptSuggestion bsCount = CompletedLength + CompositionDepth.
```
