# 작업 01 — 자동완성 토글 OFF 시 단어 학습 스킵

> **이 문서의 목표**: 자동완성 토글(`AppConfig.AutoCompleteEnabled`)이 `false`일 때는 사용자가 입력한 단어를 **사용자 사전(user-words.ko.json, user-words.en.json)에 기록하지 않도록** 한다. 현재는 토글 상태와 무관하게 모든 단어가 학습되고 있다.
>
> **선행 읽기**: [00-overview.md](00-overview.md) §3 "핵심 설계 원칙", [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2 "절대 건드리지 말 것".

---

## 0. TL;DR

- 세 곳에서 `WordFrequencyStore.RecordWord`가 최종 호출된다:
  1. `KoreanInputModule.FinalizeComposition()` → `_koDict.RecordWord(_composer.Current)` (한글 확정)
  2. `KoreanInputModule.FinalizeComposition()` → `_enDict.RecordWord(_englishPrefix)` (QuietEnglish 확정)
  3. `KoreanInputModule.AcceptSuggestion()` → `_koDict.RecordWord(suggestion)` 또는 `_enDict.RecordWord(suggestion)` (제안 수락)
- 이 세 지점 모두에서 "자동완성 토글이 꺼져 있으면 기록을 스킵"하는 분기를 넣는다.
- 가장 깔끔한 구현 방법은 `KoreanInputModule`이 `ConfigService`에 의존하도록 DI를 수정하는 것이다. 대안으로 `Func<bool> isLearningEnabled` 콜백 주입도 가능.
- **`KoreanInputModule.HandleKey`의 분기 구조, `AcceptSuggestion`의 BS 카운트 계산, `FinalizeComposition`의 `_composer.Reset()`·`_input.ResetTrackedLength()` 호출 등은 건드리지 않는다**. 오직 `_koDict.RecordWord`/`_enDict.RecordWord` **호출 앞에 조건문 한 줄**을 추가하는 수준으로 끝난다.

---

## 1. 배경과 동기

### 1.1 현재 동작

[`AltKey/Models/AppConfig.cs:39`](../../AltKey/Models/AppConfig.cs):

```csharp
public bool AutoCompleteEnabled { get; set; } = false;
```

이 토글은 현재 두 가지 역할만 한다:

1. [`AltKey/ViewModels/SuggestionBarViewModel.cs:38`](../../AltKey/ViewModels/SuggestionBarViewModel.cs) — 제안 바의 `IsVisible`을 결정 (UI 가시성).
2. [`AltKey/App.xaml.cs:103-114`](../../AltKey/App.xaml.cs) — 관리자 모드에서 강제 OFF, 일반 모드에서 `InputService.Mode` 초기화 (Unicode vs VirtualKey).

**문제**: 토글이 OFF여도 `KoreanInputModule`은 여전히 단어를 확정·기록한다. 이는 [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md)에서 "가/A" 토글이나 VirtualKey 모드에서도 `FinalizeComposition`이 호출되도록 한 기존 설계와 맞물려 있다. 즉 `FinalizeComposition`은 **조합 상태 초기화**를 위해 꼭 필요하지만, 그 안의 `RecordWord` 호출은 **조건부**여야 한다.

### 1.2 사용자가 기대하는 동작

- 토글 OFF → **입력은 정상 동작**하되(조합 및 OS 송신은 그대로), 사용자 사전에는 **아무것도 추가되지 않는다**.
- 토글 ON → 기존과 동일하게 두 음절 이상 한글 단어·두 글자 이상 영어 단어를 학습한다.
- 토글을 ON↔OFF로 바꾸는 동안 **현재 조합 중인 단어의 송신은 중단되지 않는다**. 토글 변경은 즉시 반영되지만, 과거에 조합 중이던 단어가 중간에 끊기거나 이중 저장되는 일은 없어야 한다.
- 토글 OFF에서 사용자가 제안 버튼을 클릭해도(→ 시나리오상 제안 바 자체가 숨겨져 있으므로 거의 일어날 일 없음) 학습하지 않는다. 안전망.

### 1.3 왜 `FinalizeComposition` 자체를 건드리면 안 되는가

[`CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §4.2에서 설명하듯, `FinalizeComposition`은 다음을 순서대로 수행한다:

```
1. _koDict.RecordWord(_composer.Current)   ← 이 호출만 조건부로 만들어야 함
2. _composer.Reset()                        ← 항상 실행 (조합 상태 비움)
3. _input.ResetTrackedLength()              ← 항상 실행 (Unicode BS 계산 무너짐 방지)
4. SuggestionsChanged(empty)                ← 항상 실행 (제안 바 비움)
```

만약 `FinalizeComposition` 자체를 스킵하면 조합 상태가 누적되거나 `TrackedOnScreenLength`가 어긋나 다음 자모 입력이 망가진다. **그래서 이 작업은 `RecordWord` 호출만 조건부로 만들고, 나머지는 그대로 둔다**.

---

## 2. 구현 전략

세 가지 방식이 가능하다. **권장 방식은 A**(명시적 `ConfigService` 주입)이며, 나머지는 대안.

### A. `KoreanInputModule`에 `ConfigService`를 주입 (권장)

- 장점: 명시적, 단위 테스트에서 `ConfigService`를 모킹 가능, 기존 ViewModel·다른 모듈 패턴(`SuggestionBarViewModel`도 `ConfigService`를 받는다)과 일관.
- 단점: 생성자 시그니처가 바뀌어 DI 등록을 업데이트해야 함. 테스트에서 `ConfigService`를 만드는 것이 다소 번거로움.
- 구현 지점:
  - [`AltKey/Services/InputLanguage/KoreanInputModule.cs:15-20`](../../AltKey/Services/InputLanguage/KoreanInputModule.cs) 생성자에 `ConfigService config` 추가.
  - `FinalizeComposition`과 `AcceptSuggestion`에서 `_config.Current.AutoCompleteEnabled` 확인.
  - [`AltKey/App.xaml.cs`](../../AltKey/App.xaml.cs)의 `services.AddSingleton<KoreanInputModule>()`는 DI가 자동으로 해결하므로 등록 변경은 필요 없음.
  - 기존 테스트(`AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs`)에 `ConfigService` 주입 추가.

### B. `Func<bool> isLearningEnabled` 콜백 주입 (대안)

- 장점: `KoreanInputModule`이 `ConfigService`를 몰라도 됨. 테스트에서 `() => true` 또는 `() => false`로 간단히 제어.
- 단점: DI 등록 시 람다 팩토리를 등록해야 하고, 런타임 설정 변경이 Func 클로저를 통해 투명하게 작동하는지 확인 필요.
- 구현 지점:
  - 생성자에 `Func<bool> isLearningEnabled` 추가.
  - DI: `sp => new KoreanInputModule(sp.GetRequiredService<InputService>(), ..., () => sp.GetRequiredService<ConfigService>().Current.AutoCompleteEnabled)`.
  - 나머지는 A와 동일.

### C. `KoreanDictionary`/`EnglishDictionary.RecordWord`에서 체크 (비권장)

- 장점: `KoreanInputModule`을 전혀 건드리지 않음.
- 단점: 사전 클래스가 "학습 가능 여부"를 알 이유가 없다(관심사 분리 위반). 또 사전 클래스는 다른 컨텍스트(예: 편집기에서 직접 추가)에서도 쓰이므로, 그 경로마저 토글에 영향받으면 안 됨 — 결국 다시 분기를 두 개로 나눠야 함.

**결론**: 방식 A를 따른다.

---

## 3. 상세 구현 지시 (방식 A 기준)

### 3.1 `KoreanInputModule.cs` 수정

파일: [`AltKey/Services/InputLanguage/KoreanInputModule.cs`](../../AltKey/Services/InputLanguage/KoreanInputModule.cs)

#### (1) using과 필드 추가

파일 상단의 using 부분은 다음과 같이 있는 그대로 유지(`using AltKey.Models;`는 이미 있음). `AltKey.Services`는 같은 어셈블리이므로 using이 필요 없다.

필드 영역에 다음 추가:

```csharp
private readonly ConfigService _config;
```

위치: [line 7-10](../../AltKey/Services/InputLanguage/KoreanInputModule.cs) 근처, 기존 필드들 아래.

#### (2) 생성자 시그니처 변경

기존 (line 15-20):

```csharp
public KoreanInputModule(InputService input, KoreanDictionary koDict, EnglishDictionary enDict)
{
    _input  = input;
    _koDict = koDict;
    _enDict = enDict;
}
```

변경 후:

```csharp
public KoreanInputModule(
    InputService input,
    KoreanDictionary koDict,
    EnglishDictionary enDict,
    ConfigService config)
{
    _input  = input;
    _koDict = koDict;
    _enDict = enDict;
    _config = config;
}
```

#### (3) `FinalizeComposition` 수정

기존 (line 213-230):

```csharp
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
    _input.ResetTrackedLength();
}
```

변경 후:

```csharp
private void FinalizeComposition()
{
    if (!_composer.HasComposition && _englishPrefix.Length == 0) return;

    bool learningEnabled = _config.Current.AutoCompleteEnabled;

    if (_submode == InputSubmode.HangulJamo && _composer.Current.Length > 0)
    {
        if (learningEnabled) _koDict.RecordWord(_composer.Current);
    }
    else if (_submode == InputSubmode.QuietEnglish && _englishPrefix.Length >= 2)
    {
        if (learningEnabled) _enDict.RecordWord(_englishPrefix);
    }

    _composer.Reset();
    _englishPrefix = "";
    SuggestionsChanged?.Invoke(Array.Empty<string>());
    _input.ResetTrackedLength();
}
```

**⚠️ 반드시 지킬 것**:

- `_composer.Reset()`, `_englishPrefix = ""`, `SuggestionsChanged?.Invoke(...)`, `_input.ResetTrackedLength()` 네 줄은 조건과 **무관하게** 항상 실행. 이들을 if 블록 안으로 들여 쓰지 말 것.
- `if (!_composer.HasComposition && _englishPrefix.Length == 0) return;` early-return도 기존 그대로.
- `_koDict.RecordWord` 내부에는 이미 2음절 미만 스킵 필터(TASK-03)가 있다 — 이중 체크이지만 제거하지 말 것.

#### (4) `AcceptSuggestion` 수정

기존 (line 172-189):

```csharp
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

변경 후:

```csharp
public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
{
    int bsCount;
    bool learningEnabled = _config.Current.AutoCompleteEnabled;

    if (_submode == InputSubmode.HangulJamo)
    {
        bsCount = _composer.CompletedLength + _composer.CompositionDepth;
        if (learningEnabled) _koDict.RecordWord(suggestion);
        _composer.Reset();
    }
    else
    {
        bsCount = _englishPrefix.Length;
        if (learningEnabled) _enDict.RecordWord(suggestion);
        _englishPrefix = "";
    }
    SuggestionsChanged?.Invoke(Array.Empty<string>());
    return (bsCount, suggestion);
}
```

**⚠️ 반드시 지킬 것**:

- `bsCount` 계산식(`_composer.CompletedLength + _composer.CompositionDepth`)은 그대로. 이 식은 [`CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §4.3의 "문문제" 회귀 방지용.
- `_composer.Reset()`, `_englishPrefix = ""`, `SuggestionsChanged?.Invoke(...)`, 반환값 모두 기존 그대로.

### 3.2 DI 등록 확인

파일: [`AltKey/App.xaml.cs`](../../AltKey/App.xaml.cs)

[line 67](../../AltKey/App.xaml.cs) 부근의 `services.AddSingleton<KoreanInputModule>();`는 수정 없이 그대로 둔다. `ConfigService`는 이미 line 48에 등록되어 있어 DI가 자동으로 주입한다.

**변경 없음** — 이 섹션은 "확인만" 하고 넘어가라는 뜻이다.

### 3.3 테스트 수정·추가

파일: [`AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs`](../../AltKey.Tests/InputLanguage/KoreanInputModuleTests.cs)

기존 테스트가 `new KoreanInputModule(...)`를 직접 호출하고 있으면 생성자 시그니처 변경으로 컴파일 에러가 난다. 헬퍼(아마 `TestHelpers.cs`)에서 공용 팩토리를 쓰고 있다면 거기에 `ConfigService` 추가.

#### (1) 테스트 헬퍼 업데이트

`AltKey.Tests/InputLanguage/TestHelpers.cs`(또는 이와 동등한 위치)에서 `KoreanInputModule`을 만드는 팩토리를 찾는다. 파일 내용을 먼저 읽어보고, 다음 중 하나의 패턴을 따른다:

**패턴 1**: 팩토리 함수가 있다면 거기에 `ConfigService` 파라미터 추가.

**패턴 2**: 개별 테스트가 `new KoreanInputModule(inputSvc, koDict, enDict)`를 직접 만든다면 공통 팩토리로 리팩토링하거나, 임시로 각 테스트마다 `new ConfigService()`를 추가.

예시 (공통 팩토리 패턴):

```csharp
public static KoreanInputModule CreateModule(
    InputService? input = null,
    KoreanDictionary? koDict = null,
    EnglishDictionary? enDict = null,
    ConfigService? config = null,
    bool autoCompleteEnabled = true)
{
    input ??= new InputService();
    koDict ??= new KoreanDictionary(lang => new WordFrequencyStore(Path.GetTempPath(), lang));
    enDict ??= new EnglishDictionary(lang => new WordFrequencyStore(Path.GetTempPath(), lang));
    config ??= new ConfigService();
    config.Current.AutoCompleteEnabled = autoCompleteEnabled;
    return new KoreanInputModule(input, koDict, enDict, config);
}
```

#### (2) 신규 테스트 추가

`KoreanInputModuleTests.cs`에 다음 시나리오를 추가:

```csharp
[Fact]
public void FinalizeComposition_With_AutoCompleteDisabled_Does_Not_Record_Word()
{
    // Arrange: 임시 디렉토리에 저장소 초기화
    var tempDir = Path.Combine(Path.GetTempPath(), "altkey-test-" + Guid.NewGuid());
    Directory.CreateDirectory(tempDir);
    var storeFactory = (string lang) => new WordFrequencyStore(tempDir, lang);
    var koDict = new KoreanDictionary(storeFactory);
    var enDict = new EnglishDictionary(storeFactory);
    var config = new ConfigService();
    config.Current.AutoCompleteEnabled = false;  // 토글 OFF
    var module = new KoreanInputModule(new InputService(), koDict, enDict, config);

    // Act: "해달"을 자모로 조합
    var ctx = new KeyContext(InputMode.Unicode, /* 나머지 인자는 기존 테스트와 동일 */);
    // ... 자모 입력 시뮬레이션 ...
    module.OnSeparator();  // FinalizeComposition 트리거

    // Assert: user-words.ko.json에 "해달"이 기록되지 않았는지 확인
    koDict.Flush();
    var storePath = Path.Combine(tempDir, "user-words.ko.json");
    var json = File.ReadAllText(storePath);
    Assert.DoesNotContain("\"해달\"", json);

    // Cleanup
    Directory.Delete(tempDir, true);
}

[Fact]
public void FinalizeComposition_With_AutoCompleteEnabled_Records_Word()
{
    // 위와 거의 동일하되 config.Current.AutoCompleteEnabled = true;
    // Assert: Assert.Contains("\"해달\"", json);
}

[Fact]
public void AcceptSuggestion_With_AutoCompleteDisabled_Does_Not_Record()
{
    // 제안 수락 경로도 같은 방식으로 검증
}
```

> **주의**: `KeyContext`의 정확한 생성자 시그니처와 자모 입력 시뮬레이션은 기존 테스트 파일을 그대로 참고하라. 여기 스니펫의 `/* 나머지 인자는 기존 테스트와 동일 */` 부분은 실제 작성 시 반드시 채운다.

#### (3) 기존 테스트 컴파일 오류 해결

생성자 시그니처 변경으로 **모든** 기존 `KoreanInputModuleTests`가 컴파일 에러를 낼 것이다. 각 테스트를 다음처럼 고친다:

- `new KoreanInputModule(input, koDict, enDict)` → `new KoreanInputModule(input, koDict, enDict, new ConfigService { Current = { AutoCompleteEnabled = true } })`
  - 또는 위의 `CreateModule` 헬퍼 호출로 치환.

**중요**: 기존 테스트는 암묵적으로 "학습이 된다"는 가정 위에 있으므로, **`AutoCompleteEnabled = true`로 기본값**을 잡아야 회귀하지 않는다.

### 3.4 `WordFrequencyStoreTests.cs` (변경 없음)

`WordFrequencyStore` 자체는 이 작업에서 수정하지 않으므로 테스트 변경 없음.

---

## 4. 수락 기준 (Acceptance Criteria)

에이전트가 작업을 "완료"로 선언하려면 다음이 모두 성립해야 한다.

### 4.1 빌드와 자동 테스트

- [ ] `dotnet build C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj` 성공 (경고 허용치 기존과 동일).
- [ ] `dotnet test C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj` 전부 녹색.
  - 특히 `HangulComposerTests` 전체, `KoreanInputModuleTests` 전체, `WordFrequencyStoreTests` 전체.
- [ ] 신규 테스트 3개(토글 OFF/ON × Finalize/Accept)가 포함되어 있고 녹색.

### 4.2 수동 검증 — 포터블 빌드 기준

1. `AltKey/bin/Release/net8.0-windows/AltKey.exe`를 실행.
2. 설정 창을 열고 자동완성 토글이 **ON**인 상태에서:
   - 메모장에 "안녕하세요" 입력 → 공백 → 창 닫기.
   - `user-words.ko.json`을 열어 `"안녕하세요": N` 형태로 기록되어 있는지 확인.
3. `user-words.ko.json`을 비우고(`{}`로 저장) 앱 재시작.
4. 설정에서 자동완성 토글을 **OFF**로 바꾼 뒤:
   - 메모장에 "안녕하세요" 입력 → 공백 → 창 닫기.
   - `user-words.ko.json`이 **여전히 `{}`** 인지 확인 (아무것도 기록 안 됨).
5. 다시 토글을 **ON**으로 바꾸고 같은 입력 반복 → 이번엔 기록됨.
6. QuietEnglish 서브모드("가/A" 버튼으로 전환)에서도 동일한 절차로 `user-words.en.json`이 토글을 따르는지 확인.

### 4.3 회귀 금지 — 반드시 유지되어야 하는 동작

- [ ] 자동완성 토글 OFF 상태에서도 **한글 조합은 정상 동작**한다. "ㅎ + ㅐ = 해"가 그대로 뜬다.
- [ ] 자동완성 토글 OFF 상태에서도 공백·엔터·탭·구두점 도달 시 조합 상태가 확정되어 다음 자모가 새 음절로 시작한다. (`_composer.Reset()`이 여전히 호출된다.)
- [ ] 자동완성 토글 OFF 상태에서 제안 바는 **숨겨져 있어** 제안 버튼 자체를 클릭할 수 없다 (이 부분은 기존 동작 그대로).
- [ ] 자동완성 토글 ON으로 돌아갔을 때, 바로 다음 입력부터 학습이 재개된다 (앱 재시작 없이).
- [ ] "해+ㅆ → 했" (쌍자음), "ㄷㅏㄹㄱ → 닭" (종성 확정), "화사에서 BS 3회 → 빈 필드"가 회귀하지 않는다.

---

## 5. 하면 안 되는 것 (다시 강조)

> [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2 "절대 건드리지 말 것"을 열어놓고 작업하라.

이 작업에서 수정하지 말아야 할 것들:

- `HangulComposer.cs` 전체 — 한 줄도 고치지 않는다.
- `KoreanInputModule.HandleKey()` (line 33-84)의 분기 구조. `isComboKey`, `jamo == null`, `ctx.HasActiveModifiersExcludingShift` 체크.
- `KoreanInputModule.HandleBackspace()`, `HandleQuietEnglish()` 내부.
- `InputService.SendAtomicReplace`, `SendUnicode`, `ResetTrackedLength`.
- `AcceptSuggestion`의 `bsCount = _composer.CompletedLength + _composer.CompositionDepth` 식.
- `WordFrequencyStore.RecordWord`의 `UnsafeRelaxedJsonEscaping`, 디바운스, `File.Move` 원자성.
- `KoreanDictionary.RecordWord`의 2음절 미만 필터(TASK-03).
- `AppConfig`의 기존 필드.
- `SuggestionBarViewModel`, `SettingsViewModel`의 기존 로직 (`IsVisible` 연결 등).

**허용되는 변경**은 오직:

1. `KoreanInputModule.cs` — 필드 1개, 생성자 파라미터 1개, `FinalizeComposition`과 `AcceptSuggestion` 안에서 `RecordWord` 호출을 `if`로 감싸기.
2. 테스트 헬퍼와 테스트 파일 — 생성자 호출 시 `ConfigService` 넘기도록 수정, 신규 테스트 3개 추가.

---

## 6. 설계 근거 FAQ

### Q1. 왜 `FinalizeComposition` 자체를 토글로 막지 않는가?

`FinalizeComposition`은 RecordWord 외에도 조합 상태 리셋(`_composer.Reset()`)과 `TrackedOnScreenLength` 리셋 같은 **상태 정리**를 담당한다. 이것을 막으면 사용자가 공백을 눌러도 조합이 확정되지 않아 다음 자모가 이어져 버린다. 따라서 "기록만 스킵, 나머지는 실행"이 유일한 올바른 형태.

### Q2. 왜 `KoreanDictionary.RecordWord`에서 체크하지 않는가?

사전 클래스는 "학습 가능 여부"라는 정책을 모르는 순수한 저장소 게이트웨이로 설계되어 있다. 정책은 호출자(이 경우 입력 모듈)가 가져야 한다. 또 사전 클래스는 **미래의 편집기 창에서도 `RecordWord`를 직접 호출할 가능성**이 있는데(작업 03번 참조), 그 경로는 사용자가 명시적으로 추가한 단어이므로 토글과 무관하게 저장되어야 한다.

### Q3. `ConfigService`가 싱글톤이니까 `_config.Current.AutoCompleteEnabled`를 매번 읽어도 되는가?

안전하다. `ConfigService.Current`는 싱글 스레드 UI 컨텍스트(WPF Dispatcher)에서 업데이트되며, 키 입력 처리도 같은 디스패처에서 일어난다. 동시 쓰기 race는 없다. 읽기 성능도 무시할 만하다.

### Q4. 관리자 모드에서 토글이 강제 OFF되는데, 그 경우 어떻게 동작하는가?

[`App.xaml.cs:103-107`](../../AltKey/App.xaml.cs)에서 관리자 모드 + `AutoCompleteEnabled=true`이면 강제 OFF한다. 이 작업 후에는 관리자 모드에서도 자연히 "학습 안 됨"이 된다(토글이 OFF이니까). 추가 변경 필요 없음.

### Q5. 토글이 OFF인 동안 "입력 중"인 단어는 어떻게 되는가?

입력 중 단어는 `_composer`와 `_englishPrefix` 인스턴스에만 존재한다. 화면에도 조합 중인 음절이 나타난다. 공백/엔터 등을 누르면 **확정은 되지만 학습되지 않는다** — 즉 사용자에게는 단어가 화면에 남지만, 내부 사전에는 추가되지 않는다. 정확히 원하는 동작.

---

## 7. 작업 완료 후 보고

에이전트가 작업 완료를 보고할 때 다음 정보를 포함:

1. 변경한 파일 목록 (경로 기준).
2. 추가한 테스트 개수와 이름.
3. `dotnet test` 실행 결과(총 테스트 수, 통과 수).
4. 수동 검증 §4.2의 6단계 중 수행한 단계와 결과. 수행하지 못한 단계(예: exe 빌드 후 메모장 테스트를 환경 제약으로 못 한 경우)는 명시적으로 표기.
5. 회귀 확인(§4.3)에 대한 명시적 답변.

**보고하지 말아야 할 것**:

- "…도 추가로 개선했습니다" 같은 범위 확장. 이 작업의 범위는 §3에 명시된 지점만이다.
- 토글 UI 자체의 재디자인. 토글은 이미 `SettingsView.xaml`의 기존 요소 중 하나이며, 이 작업은 해당 UI를 건드리지 않는다.

---

## 8. 다음 작업으로

이 작업 완료 후 자연스러운 다음 단계는 [02-context-menu-remove.md](02-context-menu-remove.md) — 제안 바에서 우클릭으로 학습된 단어를 제거하는 기능. 해당 지시서는 `WordFrequencyStore.RemoveWord()` 공개 API 추가도 포함하며, 03번 작업의 기반이 된다.
