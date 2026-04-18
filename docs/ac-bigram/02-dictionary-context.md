# 02 — `KoreanDictionary` / `EnglishDictionary` 문맥 확장

> **목적**: 01번에서 만든 `BigramFrequencyStore`를 각 언어 사전이 주입받아, "이전 단어"가 주어졌을 때 bigram 후보를 기존 제안 리스트에 **가산**하도록 확장한다. 기존 `GetSuggestions(prefix, count)` 시그니처는 **보존**하고 오버로드로 추가한다.
>
> **선행 조건**: 01번이 완료되어 `BigramFrequencyStore`가 빌드·테스트를 통과한 상태.

---

## 1. 체크리스트

- [ ] `AltKey/Services/KoreanDictionary.cs` 수정: 생성자에 `BigramFrequencyStore` 팩토리 주입 + `RecordBigram`·`GetSuggestions(prefix, prev)` 오버로드 추가.
- [ ] `AltKey/Services/EnglishDictionary.cs` 수정: 동일 패턴으로 확장.
- [ ] `AltKey/App.xaml.cs`: `KoreanDictionary`·`EnglishDictionary` 싱글톤 등록이 이미 팩토리를 받는 형태이면 bigram 팩토리를 한 줄 추가로 전달. 현재 등록부를 먼저 확인할 것.
- [ ] `AltKey.Tests/InputLanguage/KoreanDictionaryTests.cs`·`EnglishDictionaryTests.cs`에 context 오버로드 테스트 추가.
- [ ] `dotnet build`·`dotnet test` 전체 녹색.

---

## 2. 설계

### 2.1 생성자 변경 (호환 손상)

현재 `KoreanDictionary` 생성자 시그니처:

```csharp
public KoreanDictionary(Func<string, WordFrequencyStore> storeFactory)
```

다음으로 확장:

```csharp
public KoreanDictionary(
    Func<string, WordFrequencyStore> userStoreFactory,
    Func<string, BigramFrequencyStore> bigramStoreFactory)
{
    _userStore = userStoreFactory("ko");
    _bigramStore = bigramStoreFactory("ko");
    _builtIn = LoadBuiltIn();
    IndexBuiltInByChoseong();
}
```

- 기존 DI 등록(`App.xaml.cs`)에서 팩토리 두 개를 받도록 업데이트한다. 현재 `AddSingleton<KoreanDictionary>(sp => new KoreanDictionary(sp.GetRequiredService<Func<string, WordFrequencyStore>>()))` 형태라면 두 번째 인자를 덧붙인다.
- 단일 생성자가 두 팩토리를 모두 받으므로 단위 테스트에서 간편히 가짜 팩토리 주입 가능.
- **대체 옵션**: 기존 생성자를 유지하고 두 번째 생성자를 오버로드로 추가 + 기본값으로 in-memory 빈 store를 쓰는 안. 유지보수 부담이 커지므로 기각. 한 번에 시그니처를 바꾸는 것이 더 명확.

### 2.2 새 공개 API

```csharp
public class KoreanDictionary
{
    // 기존 API — 시그니처 보존
    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5);
    public void RecordWord(string word);
    public bool TryRemoveUserWord(string word);
    public WordFrequencyStore UserStore { get; }
    public void Flush();

    // 신규 API
    public BigramFrequencyStore BigramStore { get; }  // 편집기에서 조회용

    /// 이전 확정 단어(prevWord)가 있을 때의 문맥 반영 제안.
    /// prevWord가 null·빈 문자열이면 기존 GetSuggestions와 동일 결과.
    public IReadOnlyList<string> GetSuggestions(string prefix, string? prevWord, int count = 5);

    /// (prev, next) 쌍을 bigram 저장소에 기록. prev·next 중 하나라도 학습 부적격이면 no-op.
    /// 학습 부적격 기준: prev·next 모두 RecordWord가 허용하는 조건(한글은 완성음절 2자 이상)을 만족해야 함.
    public void RecordBigram(string prevWord, string nextWord);

    /// 편집기에서 호출: 사용자가 특정 쌍 삭제.
    public bool TryRemoveBigramPair(string prev, string next);
}
```

`EnglishDictionary`에도 동일하게 `BigramStore` getter + `GetSuggestions(prefix, prevWord)` + `RecordBigram` + `TryRemoveBigramPair`를 추가한다.

### 2.3 RecordBigram 필터

- 한글: `RecordWord`와 동일한 2음절 이상 필터를 `prev`와 `next` **양쪽**에 적용. 둘 중 하나라도 실격이면 skip.
- 영어: `RecordWord`와 동일하게 `word.Length >= 2` + 소문자 정규화를 양쪽에 적용.
- 공백 포함 문자열은 trim 후 평가.

```csharp
public void RecordBigram(string prevWord, string nextWord)
{
    if (!IsBigramEligible(prevWord) || !IsBigramEligible(nextWord)) return;
    _bigramStore.Record(prevWord.Trim(), nextWord.Trim());
}

private static bool IsBigramEligible(string w)
{
    if (string.IsNullOrWhiteSpace(w)) return false;
    w = w.Trim();
    int syllables = 0;
    foreach (var ch in w)
        if (ch >= '\uAC00' && ch <= '\uD7A3') syllables++;
    return syllables >= 2;
}
```

영어 버전:

```csharp
public void RecordBigram(string prevWord, string nextWord)
{
    if (string.IsNullOrWhiteSpace(prevWord) || string.IsNullOrWhiteSpace(nextWord)) return;
    if (prevWord.Length < 2 || nextWord.Length < 2) return;
    _bigramStore.Record(
        prevWord.ToLowerInvariant().Trim(),
        nextWord.ToLowerInvariant().Trim());
}
```

---

## 3. Ranking 규칙 (context-aware GetSuggestions)

### 3.1 목표

- **기본 원칙**: 기존 `GetSuggestions(prefix)`가 반환한 리스트를 바탕으로 bigram 후보를 상위에 **끌어올린다**. 결코 "기존 후보를 제거"하지 않는다. 새 후보 추가도 신중하게.
- **사용자 체감**: 현재 상위 3위 안에 있던 단어가 1위로 올라가는 경우 / bigram에서 제공된 신규 단어가 3위 근처에 삽입되는 경우가 가장 자연스럽다.

### 3.2 점수 공식

```
final_score(word) = base_rank_score(word) + boost(word)

base_rank_score(word):
    - userStore 제안에 포함되면 userStore 빈도 그대로 (예: 5회 쓰인 단어는 5점)
    - 내장 사전만 있으면 1점 (단순 존재 가중)

boost(word):
    - bigramStore.GetNexts(prev, prefix)에 있으면 bigram_count * BigramBoost
      (BigramBoost 상수 = 3)
    - 없으면 0
```

- `BigramBoost = 3`은 "기존 5회 쓰인 단어"와 "bigram 2회 쓰인 단어" (=점수 6)가 비슷한 격차로 경쟁하도록 잡은 휴리스틱. 구현 후 수동 시나리오로 체감 후 조정 가능(값 변경은 이번 지시서 범위 안에서 허용).

### 3.3 신규 후보 삽입 정책

- bigram 후보 중 `prefix`를 만족하지만 기존 `GetSuggestions` 결과에 없었던 단어가 있을 수 있다(예: 사용자 사전 상위 5개 바깥이지만 bigram에서 높은 빈도).
- 이런 신규 후보는 **최대 `count` 리스트 안에 `floor(count/2)`개까지만** 삽입 허용. 나머지는 기존 리스트로 채운다. 이 규칙으로 "갑자기 모르는 단어가 대거 추천"되는 느낌을 막는다.

### 3.4 구현 스켈레톤

```csharp
private const int BigramBoost = 3;

public IReadOnlyList<string> GetSuggestions(string prefix, string? prevWord, int count = 5)
{
    // 기존 결과
    var baseList = GetSuggestions(prefix, count);

    if (string.IsNullOrEmpty(prevWord)) return baseList;

    // bigram 후보 조회 (prefix까지 필터된 상태, count보다 넉넉히)
    var bigramHits = _bigramStore.GetNexts(prevWord, prefix, count * 2);
    if (bigramHits.Count == 0) return baseList;

    // 단어 → (baseIndex, bigramCount) 맵핑
    var baseIndex = new Dictionary<string, int>(StringComparer.Ordinal);
    for (int i = 0; i < baseList.Count; i++)
        baseIndex[baseList[i]] = i;

    // 신규 후보 상한
    int maxNewInserts = count / 2;
    int newInserts = 0;

    // 점수 계산용 리스트
    var scored = new List<(string Word, double Score)>();
    foreach (var w in baseList)
    {
        double rank = baseList.Count - baseIndex[w]; // 원래 순위 가중치 (높을수록 먼저)
        double boost = 0;
        foreach (var (next, c) in bigramHits)
        {
            if (next.Equals(w, StringComparison.Ordinal)) { boost = c * BigramBoost; break; }
        }
        scored.Add((w, rank + boost));
    }

    foreach (var (next, c) in bigramHits)
    {
        if (baseIndex.ContainsKey(next)) continue;
        if (newInserts >= maxNewInserts) break;
        scored.Add((next, c * BigramBoost));
        newInserts++;
    }

    return scored
        .OrderByDescending(t => t.Score)
        .ThenBy(t => t.Word, StringComparer.Ordinal)
        .Take(count)
        .Select(t => t.Word)
        .ToList();
}
```

### 3.5 초성 단독 케이스

- `prefix`가 호환 자모 초성 1글자일 때도 위 공식은 동작한다. `GetSuggestions(prefix, count)`가 이미 `GetSuggestionsByChoseong`로 분기해 초성 기준 단어 리스트를 반환하고, `BigramFrequencyStore.GetNexts`도 prefix가 초성 자모이면 첫 음절 초성 기준으로 매칭한다.
- 단, 초성 매칭 단계에서 bigram 후보가 base 리스트에 없는 새 단어일 가능성이 더 높다(초성 공간이 좁기 때문). `maxNewInserts` 상한은 그대로 적용.

### 3.6 영어 prefix

- `EnglishDictionary.GetSuggestions(prefix, prevWord, count)`는 prefix·prevWord 모두 소문자 정규화 후 호출자 규율(`_userStore`가 소문자 키로 저장됨)에 맞게 처리한다. `prevWord`가 대문자 섞여 들어오면 `ToLowerInvariant()`로 통일.

---

## 4. DI 등록 업데이트

`App.xaml.cs`에서:

```csharp
// Before (예시)
services.AddSingleton<KoreanDictionary>(sp =>
    new KoreanDictionary(sp.GetRequiredService<Func<string, WordFrequencyStore>>()));

// After
services.AddSingleton<KoreanDictionary>(sp =>
    new KoreanDictionary(
        sp.GetRequiredService<Func<string, WordFrequencyStore>>(),
        sp.GetRequiredService<Func<string, BigramFrequencyStore>>()));
```

`EnglishDictionary` 등록도 같은 방식으로.

> **실제 코드를 반드시 먼저 읽어 현재 형태를 확인할 것**. `App.xaml.cs`의 등록부가 팩토리 람다가 아닌 다른 방식일 수 있다.

---

## 5. 기존 테스트 호환성

`AltKey.Tests/InputLanguage/TestHelpers.cs`에 `KoreanDictionaryTestable`·`EnglishDictionaryTestable`가 있다(기존 테스트 빌드 대상). 이들이 `KoreanDictionary`의 기본 생성자를 호출하는 구조라면, 새 생성자 시그니처에 맞춰 수정해야 한다.

### 5.1 권장 수정 패턴 (TestHelpers.cs)

```csharp
public sealed class KoreanDictionaryTestable : KoreanDictionary
{
    public KoreanDictionaryTestable()
        : base(
            lang => new WordFrequencyStore(
                Directory.CreateTempSubdirectory("altkey-tests").FullName, lang),
            lang => new BigramFrequencyStore(
                Directory.CreateTempSubdirectory("altkey-tests").FullName, lang))
    { }

    // 기존 public/internal 도우미 API 그대로
}
```

`EnglishDictionaryTestable`도 동일. 임시 디렉터리 두 개가 생기는 게 거슬리면 한 디렉터리를 공유해도 파일명이 달라 충돌하지 않는다.

### 5.2 기존 테스트 영향

- `KoreanDictionaryTests`·`EnglishDictionaryTests`의 모든 기존 어서션은 **그대로 통과해야 한다**. `GetSuggestions(prefix)` 오버로드는 건드리지 않았고, `RecordWord` 규율은 동일.
- 변경된 건 생성자 시그니처 뿐. Testable 클래스가 이를 흡수.

---

## 6. 신규 테스트 명세

### 6.1 `KoreanDictionaryTests.cs` 추가분

```csharp
[Fact]
public void GetSuggestions_with_prev_null_is_same_as_no_context()
{
    var dict = new KoreanDictionaryTestable();
    dict.RecordWord("해달");
    var a = dict.GetSuggestions("해");
    var b = dict.GetSuggestions("해", null);
    Assert.Equal(a, b);
}

[Fact]
public void GetSuggestions_with_prev_promotes_bigram_match_to_top()
{
    var dict = new KoreanDictionaryTestable();
    dict.RecordWord("하세요");    // 기본 빈도 1
    dict.RecordWord("해달");      // 빈도 1
    // 바이그램 기록: "안녕" 뒤에 "하세요"가 3번 등장
    for (int i = 0; i < 3; i++) dict.RecordBigram("안녕", "하세요");

    var withCtx = dict.GetSuggestions("하", "안녕", 5);
    Assert.Equal("하세요", withCtx[0]);
}

[Fact]
public void GetSuggestions_new_bigram_candidate_is_inserted_but_capped()
{
    var dict = new KoreanDictionaryTestable();
    // baseList 상위권을 다른 단어로 채운 뒤 bigram에만 신규 단어가 있음
    dict.RecordWord("가족");
    dict.RecordBigram("우리", "가나다");
    var sugg = dict.GetSuggestions("가", "우리", 5);
    Assert.Contains("가나다", sugg);
    Assert.True(sugg.Count <= 5);
}

[Fact]
public void RecordBigram_skips_when_either_side_is_single_syllable()
{
    var dict = new KoreanDictionaryTestable();
    dict.RecordBigram("가", "하세요");       // prev 실격
    dict.RecordBigram("안녕", "해");          // next 실격
    Assert.Equal(0, dict.BigramStore.Count);
}

[Fact]
public void RecordBigram_accepts_two_syllable_pair()
{
    var dict = new KoreanDictionaryTestable();
    dict.RecordBigram("안녕", "하세요");
    Assert.True(dict.BigramStore.Contains("안녕", "하세요"));
}
```

### 6.2 `EnglishDictionaryTests.cs` 추가분

`KoreanDictionaryTests`와 유사하되, 2글자 이상 + 소문자 정규화 규율에 맞춰.

```csharp
[Fact]
public void RecordBigram_is_case_insensitive_storage()
{
    var dict = new EnglishDictionaryTestable();
    dict.RecordBigram("Hello", "World");
    Assert.True(dict.BigramStore.Contains("hello", "world"));
}

[Fact]
public void GetSuggestions_english_prev_boosts_bigram_next()
{
    var dict = new EnglishDictionaryTestable();
    dict.RecordWord("world");     // base 빈도 1
    dict.RecordWord("work");      // base 빈도 1
    for (int i = 0; i < 3; i++) dict.RecordBigram("hello", "world");

    var sugg = dict.GetSuggestions("wo", "hello", 5);
    Assert.Equal("world", sugg[0]);
}
```

### 6.3 테스트 원칙

- 실제 파일 I/O를 수반하지만 `Testable` 클래스가 임시 디렉터리를 만들므로 테스트 간 격리는 OS가 보장.
- 디바운스 타이머 종료를 기다리지 않고 `dict.BigramStore.Flush()`를 호출해 즉시 파일에 반영(필요 시).
- 병렬 테스트 실행 시에도 각 테스트가 고유 임시 디렉터리를 쓰므로 충돌 없음.

---

## 7. 주의·규정

- **건드리지 말 것**: `KoreanDictionary.GetSuggestions(prefix, count)` 기존 시그니처의 내부 로직. 새 오버로드는 내부에서 기존 메서드를 호출하는 얇은 래퍼로만 쓰고, 기존 메서드를 private으로 바꾸지 않는다(다른 호출자가 있을 수 있음).
- **건드리지 말 것**: `RecordWord`의 2음절 필터. bigram은 별도 규율(`RecordBigram` 내부 필터)이며 unigram 저장소에는 영향을 주지 않는다.
- **허용**: `BigramBoost` 상수 값, `maxNewInserts` 상한 — 이 지시서 범위 안에서 수동 검증 후 조정 가능. 디폴트는 각각 3, `count / 2`.
- **허용**: 사전 내부 private 헬퍼 추가(`IsBigramEligible` 등).

---

## 8. 완료 조건

- [ ] `KoreanDictionary`·`EnglishDictionary` 생성자가 bigram 팩토리를 받는다.
- [ ] 각 사전에 `BigramStore` getter, `RecordBigram`, `GetSuggestions(prefix, prev, count)`, `TryRemoveBigramPair`가 있다.
- [ ] `App.xaml.cs` DI 업데이트 반영.
- [ ] Testable 클래스가 새 생성자에 맞춰 수정됨.
- [ ] 기존 사전 테스트 전부 녹색.
- [ ] 신규 context-aware 테스트 전부 녹색.
- [ ] 커밋 메시지: `feat(ac-bigram): context-aware suggestions in Korean/English dictionary`.

---

## 9. 다음 단계

02 완료 후 [03-module-wire.md](03-module-wire.md)로 이동해 `KoreanInputModule`이 이전 단어를 추적·전달하고 bigram을 기록하도록 한다. 이 단계가 **CORE-LOGIC-PROTECTION 위험 구역**이므로 02의 테스트가 모두 녹색임을 반드시 확인하고 진행한다.
