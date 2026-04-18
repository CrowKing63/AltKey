# 03 — `KoreanInputModule` 이전 단어 추적 · Bigram 기록 · 제안 호출 시 문맥 주입

> **목적**: 01·02번에서 마련한 저장소·사전 API를 실제 키 입력 파이프라인에 연결한다. `KoreanInputModule`이 "마지막으로 확정된 단어"를 슬롯에 기억하고, (a) 다음 확정 시 bigram을 기록하며 (b) 조합 중 제안 갱신 시 해당 문맥을 사전에 전달한다.
>
> ⚠️ **CORE-LOGIC-PROTECTION 위험 구역**. 이 작업은 [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2 목록 중 **`KoreanInputModule.HandleKey`의 분기 구조**에 인접한다. 제어 흐름(분기 순서, `jamo==null`·`FinalizeComposition` 호출 규칙, `SendAtomicReplace` 호출 타이밍)은 **절대 바꾸지 말 것**. 본 지시서가 허용하는 변경은 **제안 호출의 인자 확장** + **새 private 필드** + **`FinalizeComposition`/`AcceptSuggestion` 내부의 기록 한 줄**뿐이다.
>
> **선행 조건**: 01·02번 완료 + 관련 테스트 전부 녹색.

---

## 1. 체크리스트

- [ ] `AltKey/Services/InputLanguage/KoreanInputModule.cs` 수정: `_lastCommittedWord` 필드 추가 + 확정 시 갱신 + bigram 기록 + 제안 호출 시 전달.
- [ ] `AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs`에 회귀 방지·학습 동작 테스트 추가.
- [ ] 기존 `KoreanInputModuleTests` 전부 녹색 유지(회귀 없음).
- [ ] 수동 시나리오 §5 재현 성공.

---

## 2. 범위 경계 (무엇을 건드리고 무엇을 건드리지 않는가)

### 2.1 건드릴 부분 (허용)

- `KoreanInputModule`에 **새 private 필드** `private string? _lastCommittedWord;` 추가.
- `FinalizeComposition()`의 말미에 bigram 기록 한 줄 추가 + `_lastCommittedWord` 갱신 한 줄.
- `AcceptSuggestion()`의 학습 기록 바로 뒤에 bigram 기록 한 줄 + `_lastCommittedWord` 갱신 한 줄.
- `HandleKey`/`HandleBackspace`/`HandleQuietEnglish`에서 `_koDict.GetSuggestions(...)` / `_enDict.GetSuggestions(...)` 호출 시 **인자만** `(prefix, _lastCommittedWord)` 형태로 확장.
- `Reset()`·`ToggleSubmode()`에서 `_lastCommittedWord = null` 로 초기화.

### 2.2 건드리지 말 부분 (금지)

- `HandleKey` 내부의 분기 순서, `isComboKey` 판단, `jamo==null` 반환, `TrackedOnScreenLength` 관리, `SendAtomicReplace` 호출 타이밍 — [CORE-LOGIC-PROTECTION.md §2](../auto-complet/CORE-LOGIC-PROTECTION.md#2-절대-건드리지-말-것-hard-freeze) 중 해당 행 참고.
- `FinalizeComposition`의 기존 `_koDict.RecordWord(...)` / `_enDict.RecordWord(...)` 조건(2음절 이상·2글자 이상 필터)을 **대체**하지 말 것. bigram 기록은 **추가**이며, 기존 unigram 기록 규율은 그대로 둔다.
- `AcceptSuggestion`의 `bsCount` 계산식(`_composer.CompletedLength + _composer.CompositionDepth`) — 절대 변경 금지.
- `InputService.SendAtomicReplace`·`ResetTrackedLength` 호출 규율.
- 언어 모듈 인터페이스(`IInputLanguageModule`)의 시그니처 — 이 지시서 범위에서는 변경하지 않는다.

### 2.3 왜 이 정도에서 멈추는가

- "직전 단어"라는 개념은 **모듈 내부에만** 존재하면 충분하다. `IInputLanguageModule`에 메서드를 추가해 외부에 노출할 필요가 없다(`SuggestionsChanged` 이벤트는 이미 사전 결과 리스트만 넘기고 있고, ViewModel은 리스트만 본다).
- 호출 경로의 분기·타이밍을 바꾸지 않으면 기존 테스트·수동 시나리오(해·했·닭·화사 3회 BS 등)가 회귀 없이 동작할 가능성이 높다.

---

## 3. 구현

### 3.1 필드 추가

```csharp
public sealed class KoreanInputModule : IInputLanguageModule
{
    // 기존 필드들...
    private InputSubmode _submode = InputSubmode.HangulJamo;
    private string _englishPrefix = "";

    // 신규 — 마지막으로 확정된 단어. null이면 문맥 없음.
    // Finalize/AcceptSuggestion에서 갱신. 서브모드 토글·Reset에서 초기화.
    private string? _lastCommittedWord;

    // 나머지 그대로...
}
```

### 3.2 제안 호출 인자 확장

**현재 `HandleKey`의 Unicode 경로 말미**:

```csharp
if (ctx.InputMode == InputMode.Unicode)
{
    int prevLen = ctx.TrackedOnScreenLength;
    _composer.Feed(jamo);
    string newOutput = _composer.Current;
    _input.SendAtomicReplace(prevLen, newOutput);
    SuggestionsChanged?.Invoke(_koDict.GetSuggestions(newOutput));    // ← 기존
    return true;
}

_composer.Feed(jamo);
SuggestionsChanged?.Invoke(_koDict.GetSuggestions(_composer.Current));  // ← 기존 (VirtualKey 경로)
return false;
```

**변경 후**:

```csharp
if (ctx.InputMode == InputMode.Unicode)
{
    int prevLen = ctx.TrackedOnScreenLength;
    _composer.Feed(jamo);
    string newOutput = _composer.Current;
    _input.SendAtomicReplace(prevLen, newOutput);
    SuggestionsChanged?.Invoke(_koDict.GetSuggestions(newOutput, _lastCommittedWord));
    return true;
}

_composer.Feed(jamo);
SuggestionsChanged?.Invoke(_koDict.GetSuggestions(_composer.Current, _lastCommittedWord));
return false;
```

**`HandleBackspace` 내부** — Unicode·Virtual 두 경로 모두에서 `GetSuggestions(prefix, _lastCommittedWord)` 로 확장.

**`HandleQuietEnglish`의 `TrackEnglishKey` 호출 경로** — `TrackEnglishKey`가 내부에서 `_enDict.GetSuggestions(_englishPrefix)`를 호출하고 있으므로, 해당 호출을 `_enDict.GetSuggestions(_englishPrefix, _lastCommittedWord)` 로 확장한다.

```csharp
private void TrackEnglishKey(char ch)
{
    if (ch != '\0')
    {
        _englishPrefix += ch;
        SuggestionsChanged?.Invoke(_enDict.GetSuggestions(_englishPrefix, _lastCommittedWord));
    }
}
```

QuietEnglish BS 경로도 동일.

### 3.3 Finalize 시 기록

```csharp
private void FinalizeComposition()
{
    if (!_composer.HasComposition && _englishPrefix.Length == 0) return;

    bool learningEnabled = _config.Current.AutoCompleteEnabled;

    string? committed = null;

    if (_submode == InputSubmode.HangulJamo && _composer.Current.Length > 0)
    {
        var word = _composer.Current;
        if (learningEnabled)
        {
            _koDict.RecordWord(word);
            // bigram: 직전 단어가 있으면 (prev → word) 기록
            if (_lastCommittedWord is { Length: > 0 })
                _koDict.RecordBigram(_lastCommittedWord, word);
        }
        committed = word;
    }
    else if (_submode == InputSubmode.QuietEnglish && _englishPrefix.Length >= 2)
    {
        var word = _englishPrefix;
        if (learningEnabled)
        {
            _enDict.RecordWord(word);
            if (_lastCommittedWord is { Length: > 0 })
                _enDict.RecordBigram(_lastCommittedWord, word);
        }
        committed = word;
    }

    // 다음 확정 때 쓸 이전-단어 슬롯 갱신
    // 학습 토글이 OFF여도 문맥 자체는 기억해야 제안이 좋아진다.
    // (토글 OFF = 저장 안 함. 조회는 허용.)
    if (committed is not null)
        _lastCommittedWord = committed;

    _composer.Reset();
    _englishPrefix = "";
    SuggestionsChanged?.Invoke(Array.Empty<string>());
    _input.ResetTrackedLength();
}
```

**설계 결정**: `_lastCommittedWord` 갱신은 `learningEnabled` 토글과 **독립**. 저장(기록)은 토글을 따르지만, "다음 타이핑에서 문맥을 쓸 수 있게" 기억하는 것은 사용자 편의 기능이므로 항상 한다. 토글을 끈 상태에서도 방금 타이핑한 문장 안에서의 제안 품질 개선은 유지된다.

### 3.4 AcceptSuggestion 시 기록

```csharp
public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
{
    int bsCount;
    bool learningEnabled = _config.Current.AutoCompleteEnabled;

    if (_submode == InputSubmode.HangulJamo)
    {
        bsCount = _composer.CompletedLength + _composer.CompositionDepth;
        if (learningEnabled)
        {
            _koDict.RecordWord(suggestion);
            if (_lastCommittedWord is { Length: > 0 })
                _koDict.RecordBigram(_lastCommittedWord, suggestion);
        }
        _composer.Reset();
    }
    else
    {
        bsCount = _englishPrefix.Length;
        if (learningEnabled)
        {
            _enDict.RecordWord(suggestion);
            if (_lastCommittedWord is { Length: > 0 })
                _enDict.RecordBigram(_lastCommittedWord, suggestion);
        }
        _englishPrefix = "";
    }

    _lastCommittedWord = suggestion;   // 수락한 단어가 다음 문맥

    SuggestionsChanged?.Invoke(Array.Empty<string>());
    return (bsCount, suggestion);
}
```

### 3.5 ToggleSubmode / Reset에서 초기화

```csharp
public void ToggleSubmode()
{
    FinalizeComposition();   // 기존 — 조합 중인 걸 먼저 확정·학습
    _lastCommittedWord = null;   // 신규: 서브모드 간 문맥 독립

    _submode = _submode == InputSubmode.HangulJamo
        ? InputSubmode.QuietEnglish
        : InputSubmode.HangulJamo;

    SuggestionsChanged?.Invoke(Array.Empty<string>());
    SubmodeChanged?.Invoke(_submode);
}

public void Reset()
{
    _composer.Reset();
    _englishPrefix = "";
    _submode = InputSubmode.HangulJamo;
    _lastCommittedWord = null;   // 신규
    SuggestionsChanged?.Invoke(Array.Empty<string>());
}
```

`FinalizeComposition`이 `ToggleSubmode`에서 먼저 불리는 점에 주의 — Finalize가 `_lastCommittedWord`를 갱신해 버리기 때문에 토글 직후에도 초기화가 필요하다(Finalize가 기록한 값 위에 null로 덮어쓰기).

---

## 4. 회귀 방지 테스트

`KoreanInputModuleTests.cs`에 다음을 추가. 기존 테스트 스타일(내부 `FakeInputService`·`KoreanDictionaryTestable` 등)을 그대로 재사용한다.

```csharp
[Fact]
public void Bigram_recorded_on_separator_after_previous_finalize()
{
    var module = CreateModule(out _);

    // "해" 두 글자 입력 (실제로는 2음절 단어가 학습 대상이므로 더 긴 단어 사용)
    // "안녕" → 공백 → "하세요" → 공백
    FeedSyllables(module, "안녕");
    module.OnSeparator();
    FeedSyllables(module, "하세요");
    module.OnSeparator();

    // 검증: "안녕" → "하세요" 바이그램이 기록됐는가
    Assert.True(module.DebugKoDict.BigramStore.Contains("안녕", "하세요"));
}

[Fact]
public void Bigram_recorded_on_AcceptSuggestion()
{
    var module = CreateModule(out _);
    FeedSyllables(module, "안녕");
    module.OnSeparator();
    FeedSyllables(module, "하");
    module.AcceptSuggestion("하세요");
    Assert.True(module.DebugKoDict.BigramStore.Contains("안녕", "하세요"));
}

[Fact]
public void LastCommittedWord_resets_after_ToggleSubmode()
{
    var module = CreateModule(out _);
    FeedSyllables(module, "안녕");
    module.OnSeparator();
    module.ToggleSubmode();                 // 영어 모드로
    // 영어 단어 확정
    FeedEnglish(module, "hello");
    module.OnSeparator();

    // 한→영 경계에서 ("안녕" → "hello")가 기록되면 오염 (다른 언어 간 문맥 금지)
    Assert.False(module.DebugEnDict.BigramStore.Contains("안녕", "hello"));
}

[Fact]
public void Bigram_not_recorded_when_autoComplete_disabled()
{
    var module = CreateModule(out _, autoCompleteEnabled: false);
    FeedSyllables(module, "안녕");
    module.OnSeparator();
    FeedSyllables(module, "하세요");
    module.OnSeparator();
    Assert.Equal(0, module.DebugKoDict.BigramStore.Count);
}

[Fact]
public void Context_is_used_in_GetSuggestions_call_path()
{
    var module = CreateModule(out _);
    // 사전에 "안녕" 다음 "하세요"를 몇 회 박아둠
    for (int i = 0; i < 3; i++) module.DebugKoDict.RecordBigram("안녕", "하세요");
    module.DebugKoDict.RecordWord("하세요");
    module.DebugKoDict.RecordWord("해달");

    FeedSyllables(module, "안녕");
    module.OnSeparator();

    IReadOnlyList<string>? captured = null;
    module.SuggestionsChanged += list => captured = list;

    // 다음 단어 시작 "ㅎ"
    module.HandleKey(ㅎ_slot, ctxNoModifiers);
    Assert.NotNull(captured);
    Assert.Contains("하세요", captured!);
}
```

### 4.1 테스트 도우미 필요 사항

- `KoreanInputModule`에 테스트 전용 getter 노출은 **금지**(CORE-LOGIC-PROTECTION 위반 위험). 대신:
  - `KoreanDictionaryTestable`·`EnglishDictionaryTestable`가 외부에서 주입되므로, 테스트는 `CreateModule`이 반환하는 사전 인스턴스를 테스트 필드로 직접 보관하도록 `CreateModule` 헬퍼를 살짝 조정.
- `FeedSyllables(module, "안녕")`·`FeedEnglish(module, "hello")` 헬퍼는 `TestSlotFactory`와 기존 `ㅎ_slot` 계열을 조합해 즉시 만들 수 있다. 없으면 테스트 파일 내부에 private 헬퍼로 추가.

`CreateModule` 예시 변경:

```csharp
private KoreanInputModule CreateModule(
    out FakeInputService input,
    out KoreanDictionaryTestable koDict,
    out EnglishDictionaryTestable enDict,
    bool autoCompleteEnabled = true)
{
    input = new FakeInputService();
    koDict = new KoreanDictionaryTestable();
    enDict = new EnglishDictionaryTestable();
    var config = new ConfigService();
    config.Current.AutoCompleteEnabled = autoCompleteEnabled;
    return new KoreanInputModule(input, koDict, enDict, config);
}

// 기존 out 한 개짜리 CreateModule은 다음과 같이 래핑
private KoreanInputModule CreateModule(out FakeInputService input, bool autoCompleteEnabled = true)
    => CreateModule(out input, out _, out _, autoCompleteEnabled);
```

### 4.2 기존 회귀 시나리오 확인 (필수)

다음 기존 테스트가 **여전히 녹색**이어야 한다. 하나라도 빨갛게 되면 즉시 원인 분석:

- `Feed_해_해_separator_records_해`
- `해_plus_Shift_ㅆ_feeds_ssang_siot_not_T`
- `Ctrl_Shift_T_is_not_a_jamo_path`
- `Accept_followed_by_new_jamo_preserves_accepted_word_on_screen` (존재 시)
- 기타 `KoreanInputModuleTests.cs`의 모든 기존 Fact

---

## 5. 수동 검증 시나리오

실제 앱을 실행해 아래 세 시나리오를 손으로 재현한다. 하나라도 어긋나면 원인 파악 후 수정.

1. **Bigram 상위권 승격**
   - 자동완성 토글 ON.
   - "안녕하세요"를 세 번 연속 입력(각각 `안녕␣하세요␣`).
   - 다시 `안녕␣` 입력 후 `ㅎ`만 누른다 → 제안 바 첫 번째가 "하세요"인가?
   - 데이터 파일 확인: `%AppData%\...\user-bigrams.ko.json` 에 `"안녕": {"하세요": 3}` 이 기록되는가?

2. **토글 OFF 시 미기록**
   - 자동완성 토글 OFF로 전환.
   - 다시 "안녕하세요"를 두 번 입력.
   - `user-bigrams.ko.json`의 카운트가 **증가하지 않아야** 한다.

3. **서브모드 토글 독립성**
   - "안녕" 입력 + 공백 후 "가/A" 버튼으로 QuietEnglish 전환.
   - "hello" 입력 + 공백.
   - `user-bigrams.en.json`에 `"안녕": {"hello": 1}`이 기록되어 있으면 실패(언어 간 오염 금지).

4. **코어 로직 회귀 없음**
   - 다음 네 개 시나리오를 그대로 재현:
     - "해" + Shift+ㅆ → "했"
     - "ㄷㅏㄹㄱ" → "닭"
     - "화사"에서 BS 3회 → 빈 필드
     - 엔터 조합 중 → 다음 줄 정상
   - 한 개라도 깨지면 즉시 중단 → CORE-LOGIC-PROTECTION 위반 체크.

---

## 6. 주의 사항

- **`_lastCommittedWord` 갱신 타이밍**: Finalize/AcceptSuggestion **끝**에서 갱신해야 한다. 중간에 갱신하면 그 턴 안에서 호출되는 `RecordBigram`이 자기 자신을 `prev`로 쓰는 버그.
- **토글 OFF 시 문맥 유지 정책**: §3.3에서 설명한 대로, 학습은 스킵하되 `_lastCommittedWord` 갱신은 유지. 이유는 사용자 체감(방금 친 단어에 이어지는 제안 품질)을 지키기 위해. 민감 정보 우려는 현재 프라이버시 설계가 "저장 안 함"으로 충분히 커버되며, 이 값은 인메모리에서 다음 Finalize 또는 Reset 시 교체된다.
- **ConfigChanged 이벤트**: 토글이 런타임 중 OFF→ON으로 바뀔 때 `_lastCommittedWord`를 굳이 초기화할 필요 없음(방금 친 단어는 문맥으로 유효). ON→OFF 전환 시에도 동일.
- **예외 처리**: `_koDict.RecordBigram` / `_enDict.RecordBigram`는 내부에서 이미 null·공백 가드와 필터를 처리한다. 모듈은 단순 호출만.

---

## 7. 완료 조건

- [ ] `KoreanInputModule.cs` 변경이 §3 스펙과 일치.
- [ ] 기존 `KoreanInputModuleTests` 전부 녹색(회귀 없음).
- [ ] 신규 테스트 §4.1 ~ §4 전체 녹색.
- [ ] §5 수동 시나리오 1~4 통과(스크린샷·파일 dump 권장).
- [ ] `HangulComposer.cs` · `InputService.cs` · `IInputLanguageModule.cs`는 **변경 없음**.
- [ ] 커밋 메시지: `feat(ac-bigram): track last committed word and record bigrams on finalize`.

---

## 8. 다음 단계

03 완료 후 [04-service-ui.md](04-service-ui.md)로 이동. `AutoCompleteService`·ViewModel 레벨에서의 문맥 전달 확인(대개 변경 불필요)과, 사용자 사전 편집기에 bigram 탭을 추가할지 결정한다.
