# 02 — `WordFrequencyStore` 언어별 분리

> **소요**: 1~2시간
> **선행**: 01
> **후행**: 03, 04
> **관련 기획**: [refactor-unif-serialized-acorn.md §3-4, §4-6, D7](../refactor-unif-serialized-acorn.md)

---

## 0. 이 태스크의 목표 한 줄

`user-words.json` 한 파일에 한국어·영어 단어가 섞여 저장되던 것을 **언어별로 파일을 분리**한다 — `user-words.ko.json` / `user-words.en.json`. `WordFrequencyStore`는 언어 코드를 주입받아 해당 언어만 관리하는 인스턴스가 된다.

---

## 1. 전제 조건

- [00-overview.md](00-overview.md), [01-models-interfaces.md](01-models-interfaces.md) 읽음.
- 01 태스크 완료 상태(인터페이스·enum 존재).
- 기존 `user-words.json`의 마이그레이션은 **하지 않는다**(D7: 새로 시작).

---

## 2. 현재 상태

### 2-1. `AltKey/Services/WordFrequencyStore.cs`

- 싱글톤. 생성자 파라미터 없음.
- `PathResolver.DataDir/user-words.json`에서 로드/저장.
- `RecordWord(string word)` 내부에서 `word.All(c => c < 128)`로 영문 여부 판단 → 영문이면 `ToLower()` + 최소 2자, 한글이면 그대로 + 최소 1자.
- `GetSuggestions(prefix, count)` — 빈도 내림차순.
- `Save()` — `UnsafeRelaxedJsonEscaping` 옵션 사용 (한글이 `\uXXXX`로 저장되는 문제 해결책).

### 2-2. `AltKey/App.xaml.cs` DI 등록

```csharp
services.AddSingleton<WordFrequencyStore>();
services.AddSingleton<KoreanDictionary>();
services.AddSingleton<EnglishDictionary>();
```

두 사전이 같은 `WordFrequencyStore` 인스턴스를 공유.

### 2-3. `AltKey/Services/KoreanDictionary.cs` / `EnglishDictionary.cs`

생성자:
```csharp
public KoreanDictionary(WordFrequencyStore userStore) { ... }
public EnglishDictionary(WordFrequencyStore userStore) { ... }
```

내부에서 `_userStore.GetSuggestions(prefix, count)` 호출 후 내장 사전으로 보충.

---

## 3. 작업 내용

### 3-1. `WordFrequencyStore`에 언어 코드 주입

**파일**: `AltKey/Services/WordFrequencyStore.cs`

변경점:

1. 생성자 시그니처에 `string languageCode` 추가.
2. 파일명 `user-words.json` → `user-words.{languageCode}.json`.
3. `RecordWord` 내부의 영문/한글 분기 제거 — 스토어 자체가 언어별이라 불필요.
4. 최소 길이 규칙을 **호출자(사전) 책임으로 이동**(아래 3-3 참조).

의사 코드:

```csharp
public class WordFrequencyStore
{
    private readonly string _languageCode;
    private readonly Dictionary<string, int> _freq = new();
    private readonly string _filePath;
    private const int MaxWords = 5000;

    public WordFrequencyStore(string languageCode)
    {
        _languageCode = languageCode;
        _filePath = Path.Combine(PathResolver.DataDir, $"user-words.{languageCode}.json");
        Load();
    }

    public void RecordWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        word = word.Trim();
        if (word.Length == 0) return;

        _freq[word] = (_freq.TryGetValue(word, out var c) ? c : 0) + 1;
        if (_freq.Count > MaxWords) PruneLowest();
        Save();   // 기존과 동일: 매 레코드마다 즉시 저장
    }

    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        // 기존 로직 유지 — prefix 필터 + 빈도 내림차순 + Take
    }

    public void Save() { /* 기존 그대로 (UnsafeRelaxedJsonEscaping 유지) */ }
    private void Load() { /* 기존 그대로 */ }
    private void PruneLowest() { /* 기존 그대로 */ }
}
```

### 3-2. DI 등록 변경

**파일**: `AltKey/App.xaml.cs`

기존 단일 등록:
```csharp
services.AddSingleton<WordFrequencyStore>();
```

**변경**: 이름으로 구분되는 두 인스턴스가 필요하지만 `Microsoft.Extensions.DependencyInjection`은 이름 있는 등록을 직접 지원하지 않는다. 간단하게 **사전 생성자가 직접 `WordFrequencyStore`를 만들도록** 책임을 옮긴다:

```csharp
// App.xaml.cs — 두 사전만 DI 등록. WordFrequencyStore는 사전 내부에서 생성.
services.AddSingleton<KoreanDictionary>();
services.AddSingleton<EnglishDictionary>();
```

또는 팩토리 델리게이트로 등록:

```csharp
services.AddSingleton<Func<string, WordFrequencyStore>>(_ => lang => new WordFrequencyStore(lang));
services.AddSingleton<KoreanDictionary>();
services.AddSingleton<EnglishDictionary>();
```

> **선택**: 팩토리 방식이 테스트 시 mock 주입을 쉽게 해주므로 이쪽을 권장. 다만 복잡도가 높으면 전자(사전 내부 `new`)도 허용.

### 3-3. `KoreanDictionary` / `EnglishDictionary` 생성자 수정

**파일**: `AltKey/Services/KoreanDictionary.cs`

팩토리 주입 버전:
```csharp
public KoreanDictionary(Func<string, WordFrequencyStore> storeFactory)
{
    _userStore = storeFactory("ko");
    _builtIn   = LoadBuiltIn();
}

public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
{
    if (prefix.Length < 1) return [];   // 한국어 최소 1자 (기존 유지)
    // ... 기존 로직
}

public void RecordWord(string word)
{
    if (word.Length < 1) return;   // 한국어 최소 1자
    _userStore.RecordWord(word);
}
```

**파일**: `AltKey/Services/EnglishDictionary.cs`

```csharp
public EnglishDictionary(Func<string, WordFrequencyStore> storeFactory)
{
    _userStore = storeFactory("en");
    _builtIn   = LoadBuiltIn();
}

public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
{
    if (prefix.Length < 2) return [];   // 영어 최소 2자 (기존 유지)
    // ...
}

public void RecordWord(string word)
{
    if (word.Length < 2) return;   // 영어 최소 2자
    _userStore.RecordWord(word.ToLowerInvariant());
}
```

> 기존 `WordFrequencyStore.RecordWord` 내부에 있던 `ToLower`/최소길이 분기가 여기로 이동했다.

### 3-4. `AutoCompleteService` 호출 지점 확인

현재 `AutoCompleteService`는 `WordFrequencyStore` 인스턴스에 **직접 의존**하고 있을 가능성이 있다(`_store` 필드로 주입). 확인:

- 직접 의존하면: `_store.RecordWord(...)` 호출을 `_koreanDict.RecordWord(...)` 또는 `_englishDict.RecordWord(...)`로 위임.
- 이 부분은 04에서 통째로 리팩토링되므로 지금은 **최소 수정**(컴파일 에러만 해결)하고 넘어가도 된다.

실용적으로는:
- 이 태스크에서는 `AutoCompleteService` 생성자에서 `WordFrequencyStore`를 받는 라인을 **제거**하고, `KoreanDictionary`·`EnglishDictionary`만 주입받도록 조정.
- `AutoCompleteService` 내부의 `_store.RecordWord(word)` 호출은 임시로 언어를 추정해서 각 사전에 위임(04에서 올바른 구조로 재정비).

### 3-5. 경로 상수 확인

- `PathResolver.DataDir`이 `%APPDATA%\AltKey` 또는 포터블 모드일 때 실행 폴더를 가리키는지 재확인. **경로 자체는 건드리지 않는다.**

---

## 4. 검증

1. 빌드 녹색.
2. 런타임:
   - 첫 실행 → `user-words.ko.json`, `user-words.en.json` 두 파일이 각각 생성되거나 없음(빈 상태).
   - 한국어 자동완성 한 번 사용 → `user-words.ko.json`에 기록. `user-words.en.json`은 미변경.
   - (07에서 "가/A" 토글 구현 이후 재검증) `QuietEnglish` 모드에서 영어 입력 → `user-words.en.json`에 기록.
3. 기존 `user-words.json`이 있어도 **무시**. 새 파일이 기본.
4. `AltKey.Tests`의 기존 테스트가 통과. `WordFrequencyStore` 테스트가 있다면 생성자 파라미터 추가 반영.

---

## 5. 함정 / 주의

- **경로 충돌**: 테스트 러너가 `PathResolver.DataDir`을 실제 사용자 경로로 잡으면 테스트 간섭이 생긴다. 테스트에서는 팩토리를 mock으로 주입하거나 `WordFrequencyStore`를 in-memory로 바꿀 수 있도록 가상 경로 파라미터를 추가 고려.
- **`Save()`를 `RecordWord`마다 호출**하는 기존 정책은 유지. 성능 이슈 없는 범위.
- **기존 `user-words.json` 무시**: 마이그레이션은 하지 않음. 사용자에게 혼선을 줄 여지가 있으므로, 09 태스크에서 `AppConfig` 기본값을 변경하거나 최초 실행 안내 다이얼로그를 추가하는 것을 고려해도 된다(이번 태스크 범위 외).
- **DI 순환**: `AutoCompleteService`가 `KoreanDictionary`·`EnglishDictionary`에 의존하게 된다. App.xaml.cs 등록 순서가 `Dictionary → AutoCompleteService`가 되도록.

---

## 6. Critical Files

| 파일 | 수정 유형 |
|---|---|
| `AltKey/Services/WordFrequencyStore.cs` | 수정 (생성자·파일명·최소길이 제거) |
| `AltKey/Services/KoreanDictionary.cs` | 수정 (생성자·`RecordWord` 위임) |
| `AltKey/Services/EnglishDictionary.cs` | 수정 (생성자·`RecordWord` 위임) |
| `AltKey/App.xaml.cs` | 수정 (DI 팩토리 등록) |
| `AltKey/Services/AutoCompleteService.cs` | 임시 수정 (컴파일 맞추기만) |

---

## 7. 커밋 메시지 초안

```
refactor(ko-only): split WordFrequencyStore by language

- WordFrequencyStore constructor now takes a languageCode.
- user-words.json → user-words.{ko|en}.json.
- Dictionaries own their RecordWord (min-length rule moved here).
- DI uses a Func<string, WordFrequencyStore> factory.
- Old user-words.json is ignored (D7).
```
