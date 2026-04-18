# TASK-03 — 단일 자모/1글자 한글 학습 오염 방지

> **심각도**: 중간 (사용자 사전 품질 저하, 체감 크지만 회복 가능)
> **선행 독해**: [CORE-LOGIC-PROTECTION.md](CORE-LOGIC-PROTECTION.md) 완독
> **예상 소요**: 30~45분

---

## 1. 증상

사용자가 실수로 `ㄱ`만 누른 뒤 Space를 누르거나, "안"만 치고 다른 앱으로 포커스를 옮기는 등의 상황에서 현재 코드는:

- `KoreanInputModule.FinalizeComposition()`이 `_composer.Current.Length > 0`을 보고
- `_koDict.RecordWord("ㄱ")` 또는 `_koDict.RecordWord("안")` 을 호출 →
- `WordFrequencyStore`가 `"ㄱ"` / `"안"` 단어를 저장.

이후 사용자가 `ㄱ`을 다시 입력하면 사용자 학습 스토어가 빈도 1 단어 `"ㄱ"`을 반환해 제안 패널 맨 앞에 1글자 자모가 뜬다. 실제 "제안"의 가치가 0이다.

---

## 2. 원인

`AltKey/Services/InputLanguage/KoreanInputModule.cs:194`

```csharp
if (_submode == InputSubmode.HangulJamo && _composer.Current.Length > 0)
{
    _koDict.RecordWord(_composer.Current);
}
```

그리고 `KoreanDictionary.RecordWord`도 `word.Length < 1`만 필터링:

```csharp
// AltKey/Services/KoreanDictionary.cs
public void RecordWord(string word)
{
    if (word.Length < 1) return;
    _userStore.RecordWord(word);
}
```

`WordFrequencyStore.RecordWord`는 trim 후 길이 체크를 **다시 0에 대해서만** 하므로 1글자 한글도 통과.

---

## 3. 설계 결정 — 어느 계층에서 막을 것인가

세 곳 중 하나를 고른다:

| 계층 | 장점 | 단점 |
|---|---|---|
| A. `KoreanInputModule.FinalizeComposition`에서 필터 | 한국어 서브모드에만 적용 | "수락 경로"는 별도로 또 필터 필요 |
| B. `KoreanDictionary.RecordWord`에서 필터 | 한국어 단어를 저장하는 모든 경로 차단 (FinalizeComposition + AcceptSuggestion) | 사전 내부 규칙이 커짐 |
| C. `WordFrequencyStore.RecordWord`에서 필터 | 일반화 | 영어는 이미 Dictionary에서 `< 2` 체크함. 여기서 중복되는 규칙이 됨 |

**권장: B**.

이유: 이미 `EnglishDictionary.RecordWord`는 `word.Length < 2 return`으로 영어 최소 길이를 Dictionary 계층에서 정의했다. 한국어도 같은 계층에서 정의하는 게 대칭적이다.

규칙 초안:

> **한국어 학습 대상의 최소 조건**:
> 1. 트림 후 최소 **완성 음절 2글자 이상**, 또는
> 2. 완성 음절 1글자 + 조합 중 자모 1개(예: "안ㄴ") 은 아님 — 이건 미완성이므로 Finalize 시 절대 도달 못 함
>
> 즉 실전에선 **완성 한글 음절(U+AC00 ~ U+D7A3) 2개 이상**이 최소 기준.

"난", "개", "가"처럼 1음절 단어도 사실 유효하지만, 제안 가치가 낮고 오염 위험이 크므로 2음절 미만은 학습 차단. 품질을 더 올리고 싶다면 `[가-힣]{2,}` 정규식으로 완성 음절만 인정.

---

## 4. 구현 예시

```csharp
// AltKey/Services/KoreanDictionary.cs
public void RecordWord(string word)
{
    if (string.IsNullOrWhiteSpace(word)) return;

    word = word.Trim();

    // 완성 한글 음절만 센다 (자모 단독 U+3131~U+3163 은 제외).
    int syllableCount = 0;
    foreach (var ch in word)
    {
        if (ch >= '\uAC00' && ch <= '\uD7A3')
            syllableCount++;
    }

    // 최소 2음절 이상일 때만 학습.
    if (syllableCount < 2) return;

    _userStore.RecordWord(word);
}
```

**주의할 엣지**:
- "해!"나 "안녕?"처럼 구두점 포함 단어 — 현재 Finalize 경로에는 구두점이 단어에 섞이지 않도록 이미 분리자 처리가 되어 있다. 그래도 필터가 구두점을 걸러주는 효과가 덤으로 생긴다.
- 영문 혼용 "Wi-Fi" — 한국어 사전에는 저장되지 않는다(QuietEnglish 모드로 친 경우 영어 사전으로 간다). Unicode 모드에서 영문 알파벳은 HangulComposer가 `_completed += jamo`로 섞어 저장하지만, 실사용에서는 GetHangulJamoFromSlot이 자모만 통과시키므로 이 경로는 안 탄다.

---

## 5. 수정 금지 영역

- `KoreanDictionary.GetSuggestions`의 제안 병합/순위 로직 유지.
- `WordFrequencyStore` JSON 직렬화 옵션(`UnsafeRelaxedJsonEscaping`) 유지.
- `HangulComposer` 내부 어떤 것도 변경 금지.
- `FinalizeComposition`의 나머지 흐름(`_composer.Reset` + `ResetTrackedLength` 등) 유지.
- `EnglishDictionary.RecordWord`의 `< 2` 규칙 유지 (이미 동일 패턴).

---

## 6. 회귀 방지 테스트

`AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs` (또는 `KoreanDictionaryTests.cs` 신설):

```csharp
[Fact]
public void RecordWord_rejects_single_jamo()
{
    var dict = new KoreanDictionaryTestable();
    dict.RecordWord("ㄱ");           // 자모 단독 — 저장 X
    Assert.Empty(dict.GetSuggestions("ㄱ"));
}

[Fact]
public void RecordWord_rejects_single_syllable()
{
    var dict = new KoreanDictionaryTestable();
    dict.RecordWord("해");           // 1음절 — 저장 X
    Assert.Empty(dict.GetSuggestions("해"));
}

[Fact]
public void RecordWord_accepts_two_or_more_syllables()
{
    var dict = new KoreanDictionaryTestable();
    dict.RecordWord("해달");
    var sugg = dict.GetSuggestions("해");
    Assert.Contains("해달", sugg);
}
```

`KoreanDictionaryTestable`이 테스트 헬퍼에 있으므로 그대로 사용 가능.

그리고 모듈 수준에서도 회귀 방지:

```csharp
[Fact]
public void FinalizeComposition_via_OnSeparator_does_not_learn_single_syllable()
{
    var module = CreateModule(out _);
    module.HandleKey(ㅎ_slot, ctxNoModifiers);
    module.HandleKey(ㅐ_slot, ctxNoModifiers);    // "해"
    module.OnSeparator();

    // 다음 "ㅎ" 입력 시 사용자 학습 제안으로 "해"가 올라오면 안 됨.
    module.HandleKey(ㅎ_slot, ctxNoModifiers);
    // SuggestionsChanged 이벤트의 마지막 리스트에 "해"가 단독으로 있으면 실패.
    // (내장 사전에 "해"만 있는 경우는 무시 — 사용자 학습 경로만 검증)
}
```

> 이벤트 수집 헬퍼가 없다면 이벤트 핸들러를 테스트 내부에서 구독해서 리스트를 모아 검증.

---

## 7. 수동 검증

1. 새 `user-words.ko.json`(빈 파일)로 시작.
2. `ㄱ` → Space → 사전에 `"ㄱ"`이 저장되는지 파일 확인. 저장되면 실패.
3. `해달` 입력 → Space → 파일에 `"해달": 1` 이 저장되는지 확인.
4. `해` → Space 를 반복해서 "해"가 저장되지 않는지.
5. `해달` 수락 후 `해` 입력 시 제안이 여전히 정상(내장 사전에 "해달" 등이 있으면) 표시.

---

## 8. 후속 고려 (이 태스크에서는 하지 말 것)

- **사용자가 기존에 쌓아둔 user-words.ko.json에 이미 1음절 단어가 있다면** — 앱 시작 시 자동 정리할지, 그대로 둘지 결정 필요. 기본은 **그대로 둔다**(사용자가 일부러 썼을 수 있음). 향후 별도 마이그레이션 태스크로 분리.
- 영어 사전의 최소 길이를 2 → 3으로 올릴지 여부도 별개 논의.

---

## 9. 커밋 메시지 초안

```
fix(dict): require 2+ 완성 음절 before learning Korean words

- 단일 자모/1음절 단어가 사용자 사전에 쌓여 제안 품질을 떨어뜨리는 문제 방지.
- KoreanDictionary.RecordWord에서 U+AC00~U+D7A3 완성 음절 수를 세어
  2 미만이면 저장 skip.
- KoreanInputModuleTests/KoreanDictionaryTests 에 회귀 방지 추가.
```
