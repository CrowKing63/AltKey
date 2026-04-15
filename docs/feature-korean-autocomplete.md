# 기능 설계: 한국어 자동 완성

> **상태**: 사전 설계 완료 / 구현 대기  
> **대상 파일**: `Services/AutoCompleteService.cs` 대폭 수정, 신규 `Services/HangulComposer.cs`, `Services/KoreanDictionary.cs`, `Models/AppConfig.cs` 수정

---

## 기능 개요

현재 `AutoCompleteService`는 영문 알파벳(`VK_A`~`VK_Z`)만 지원한다. 한국어는 IME 조합 과정을 거쳐 입력되므로 단순 VK 코드 추적이 불가능하다.  
이 문서는 AltKey가 직접 한글 자모 조합 상태를 추적하여 IME 없이도 한국어 단어 제안을 제공하는 설계를 기술한다.

---

## 현재 코드 파악

### 관련 파일

| 파일 | 현황 |
|------|------|
| `Services/AutoCompleteService.cs` | 영문만 지원. 한글은 "IME/TSF 통합 필요" 주석 있음 |
| `Services/WordFrequencyStore.cs` | 단어 학습/조회 (영문 기준 `ToLower()` 적용 중) |
| `ViewModels/SuggestionBarViewModel.cs` | AcceptSuggestion → SendUnicode(remaining) |
| `Views/SuggestionBar.xaml` | 제안 버튼 UI (스타일 변경 불필요) |
| `Models/AppConfig.cs:40` | `AutoCompleteEnabled` 이미 있음 |
| `Platform/Win32.cs:96` | `ImmGetContext`, `ImmGetConversionStatus` 이미 선언됨 |

### 핵심 제약

AltKey는 키보드 앱 자체이므로 한글 입력 시 **어떤 자모가 눌렸는지 직접 알고 있다** — 이것이 IME 연동 없이도 한글 자동 완성이 가능한 핵심 이유다.  
`layouts/qwerty-ko.json`의 각 키는 `hangul_label`을 가지며, `KeyboardViewModel`에서 이 값을 사용해 실제 입력을 처리한다.

---

## 구현 전략: AltKey 내장 자모 추적

외부 IME 상태가 아닌 **AltKey 내부에서 눌린 한글 키 시퀀스**를 직접 추적한다.

### 한글 입력 흐름 (현재)

```
KeyButton(hangul_label="ㄱ") 클릭
  → InputService.HandleAction(BoilerplateAction{ Text="ㄱ" }) 또는 SendKeyAction
  → (실제 한글 조합은 OS IME가 처리)
```

### 한글 입력 흐름 (자동 완성 추가 후)

```
KeyButton(hangul_label="ㄱ") 클릭
  → InputService.HandleAction(...)
  → AutoCompleteService.OnHangulInput("ㄱ")   ← 신규 메서드
      → HangulComposer.Feed("ㄱ") → 조합 상태 업데이트
      → KoreanDictionary.GetSuggestions(composer.Current) → 제안 목록
      → SuggestionsChanged 이벤트
```

---

## 새로운 파일 1: `Services/HangulComposer.cs`

한글 자모를 받아 현재까지 입력된 음절 문자열을 유지한다.

### 유니코드 한글 조합 원리

```
한글 유니코드 = 0xAC00 + (초성 × 21 + 중성) × 28 + 종성

초성 19개: ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ
중성 21개: ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅠㅡㅢㅣ  (인덱스 0~20)
종성 28개: (없음) ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ
```

### 인터페이스

```csharp
namespace AltKey.Services;

/// 한글 자모를 순서대로 받아 현재 입력 문자열(음절 단위 조합 결과)을 추적한다.
public class HangulComposer
{
    /// 지금까지 완성된 음절들 + 현재 조합 중인 음절
    public string Current { get; private set; } = "";

    /// 자모 하나 입력 (예: "ㄱ", "ㅏ", "ㄴ")
    public void Feed(string jamo);

    /// 백스페이스 처리 (마지막 자모 제거)
    public void Backspace();

    /// 단어 구분자(공백, 엔터 등)로 초기화
    public void Reset();
}
```

### 구현 알고리즘 (두벌식 기준)

```
상태: (초성?, 중성?, 종성?)

Feed(jamo):
  ┌─ 현재 상태 없음 (빈 음절):
  │   jamo가 초성? → 상태 = (초성, null, null)
  │   jamo가 중성? → 완성 불가 홀모음 → 그냥 추가 (예: "ㅏ" 단독)
  │
  ├─ 상태 = (초성, null, null):
  │   jamo가 중성? → 상태 = (초성, 중성, null)  [음절 시작]
  │   jamo가 초성? → 이전 초성 단독으로 확정, 새 초성으로 시작
  │
  ├─ 상태 = (초성, 중성, null):
  │   jamo가 종성 가능한 자음? → 상태 = (초성, 중성, 종성후보)
  │   jamo가 중성? → 이전 음절 확정, 새 (null, 중성) 상태
  │   jamo가 초성? → 이전 음절 확정, 새 (초성, null) 상태
  │
  └─ 상태 = (초성, 중성, 종성후보):
      jamo가 중성? → 종성후보를 다음 음절의 초성으로 이동, 새 음절 = (종성후보_as_초성, 중성)
      jamo가 자음? → 겹받침 가능하면 겹받침으로, 불가하면 새 초성으로

유니코드 조합:
  ch = 0xAC00 + (초성_idx * 21 + 중성_idx) * 28 + 종성_idx
  Current = 완성된_음절들 + ch
```

### 초성/중성/종성 인덱스 테이블

```csharp
static readonly string[] Choseong  = ["ㄱ","ㄲ","ㄴ","ㄷ","ㄸ","ㄹ","ㅁ","ㅂ","ㅃ","ㅅ","ㅆ","ㅇ","ㅈ","ㅉ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"];
static readonly string[] Jungseong = ["ㅏ","ㅐ","ㅑ","ㅒ","ㅓ","ㅔ","ㅕ","ㅖ","ㅗ","ㅘ","ㅙ","ㅚ","ㅛ","ㅜ","ㅝ","ㅞ","ㅟ","ㅠ","ㅡ","ㅢ","ㅣ"];
static readonly string[] Jongseong = ["","ㄱ","ㄲ","ㄳ","ㄴ","ㄵ","ㄶ","ㄷ","ㄹ","ㄺ","ㄻ","ㄼ","ㄽ","ㄾ","ㄿ","ㅀ","ㅁ","ㅂ","ㅄ","ㅅ","ㅆ","ㅇ","ㅈ","ㅊ","ㅋ","ㅌ","ㅍ","ㅎ"];
```

---

## 새로운 파일 2: `Services/KoreanDictionary.cs`

### 한국어 단어 데이터 소스

두 레이어로 제안을 생성한다.

#### 레이어 1: 내장 빈도 사전 (읽기 전용)

- 파일: `Assets/Data/ko-words.txt` (신규 추가)
- 형식: 한 줄에 한 단어, 빈도순 정렬 (상위 10,000개)
- 출처: [Korean Frequency Word List (공개 데이터)](https://github.com/hermitdave/FrequencyWords/) — `ko_50k.txt` 상위 추출
- 앱 빌드 시 `Build Action: Embedded Resource` 설정

#### 레이어 2: `WordFrequencyStore` (사용자 학습)

기존 `WordFrequencyStore`를 한글 단어도 저장하도록 확장.  
`RecordWord`에서 `ToLower()` 호출 제거 (한글에 대소문자 없음).

```csharp
namespace AltKey.Services;

public class KoreanDictionary
{
    private readonly WordFrequencyStore     _userStore;
    private readonly IReadOnlyList<string>  _builtIn;  // 내장 사전 (로딩 후 캐시)

    public KoreanDictionary(WordFrequencyStore userStore)
    {
        _userStore = userStore;
        _builtIn   = LoadBuiltIn();  // EmbeddedResource 로드
    }

    /// prefix로 시작하는 한국어 단어 제안 (사용자 학습 우선, 그 다음 내장 사전)
    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (prefix.Length < 1) return [];

        // 1. 사용자 학습 단어 (빈도 높은 순)
        var userSuggestions = _userStore.GetSuggestions(prefix, count);

        // 2. 부족하면 내장 사전으로 보충
        var needed = count - userSuggestions.Count;
        if (needed <= 0) return userSuggestions;

        var builtInSuggestions = _builtIn
            .Where(w => w.StartsWith(prefix) && w.Length > prefix.Length
                        && !userSuggestions.Contains(w))
            .Take(needed)
            .ToList();

        return [..userSuggestions, ..builtInSuggestions];
    }

    private static IReadOnlyList<string> LoadBuiltIn()
    {
        var asm    = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("AltKey.Assets.Data.ko-words.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
    }
}
```

---

## `AutoCompleteService.cs` 수정

```csharp
public class AutoCompleteService
{
    private readonly WordFrequencyStore  _store;
    private readonly KoreanDictionary   _koreanDict;   // 신규
    private readonly HangulComposer     _hangul = new(); // 신규
    private string _currentWord = "";
    private bool   _isHangulMode = false;  // 현재 한글 입력 중 여부

    /// 한글 자모 입력 (KeyboardViewModel에서 호출)
    /// jamo: "ㄱ", "ㅏ" 등 단일 자모 문자열
    public void OnHangulInput(string jamo)
    {
        _isHangulMode = true;
        _hangul.Feed(jamo);
        var suggestions = _koreanDict.GetSuggestions(_hangul.Current);
        SuggestionsChanged?.Invoke(suggestions);
    }

    /// 기존 영문 입력 (변경 없음)
    public void OnKeyInput(VirtualKeyCode vk)
    {
        if (IsWordSeparator(vk))
        {
            if (_isHangulMode)
            {
                if (_hangul.Current.Length >= 1) _store.RecordWord(_hangul.Current);
                _hangul.Reset();
                _isHangulMode = false;
            }
            else if (_currentWord.Length >= 2)
            {
                _store.RecordWord(_currentWord);
            }
            _currentWord = "";
            SuggestionsChanged?.Invoke([]);
            return;
        }

        if (vk == VirtualKeyCode.VK_BACK)
        {
            if (_isHangulMode) _hangul.Backspace();
            else if (_currentWord.Length > 0) _currentWord = _currentWord[..^1];
        }
        else if (!_isHangulMode)
        {
            var ch = VkToChar(vk);
            if (ch != '\0') _currentWord += ch;
        }

        var sugg = _isHangulMode
            ? _koreanDict.GetSuggestions(_hangul.Current)
            : (IReadOnlyList<string>)_store.GetSuggestions(_currentWord);
        SuggestionsChanged?.Invoke(sugg);
    }

    /// 제안 수락 (한글/영문 공통)
    public string AcceptSuggestion(string suggestion)
    {
        var prefix   = _isHangulMode ? _hangul.Current : _currentWord;
        var remaining = suggestion.Length > prefix.Length
            ? suggestion[prefix.Length..] : "";

        _store.RecordWord(suggestion);
        _hangul.Reset();
        _currentWord  = "";
        _isHangulMode = false;
        SuggestionsChanged?.Invoke([]);
        return remaining;
    }
}
```

---

## `KeyboardViewModel.cs` 수정

현재 `KeyboardViewModel`은 키 클릭 시 `InputService.HandleAction()`을 호출한다.  
한글 키의 경우 `hangul_label`을 기반으로 `AutoCompleteService.OnHangulInput()`도 함께 호출해야 한다.

```csharp
// KeyboardViewModel.KeyPressed() 또는 유사 메서드에 추가
if (slot.HangulLabel is { Length: > 0 } jamo && _configService.Current.AutoCompleteEnabled)
    _autoComplete.OnHangulInput(jamo);
```

> `KeySlot` 모델에 `HangulLabel` 프로퍼티가 있는지 확인 필요.  
> 현재 `qwerty-ko.json`에는 `hangul_label` 필드가 있으며 `KeySlot.cs`에서 역직렬화됨.

---

## `WordFrequencyStore.cs` 수정 (최소)

한글 단어 저장 지원을 위해 `ToLower()` 조건부 적용:

```csharp
public void RecordWord(string word)
{
    if (string.IsNullOrWhiteSpace(word) || word.Length < 1) return;
    // 영문은 소문자 통일, 한글은 그대로
    bool isLatin = word.All(c => c < 128);
    if (isLatin) word = word.Trim().ToLower();
    else         word = word.Trim();
    // 최소 길이: 영문 2자, 한글 1자
    if (isLatin && word.Length < 2) return;
    _freq[word] = (_freq.TryGetValue(word, out var c) ? c : 0) + 1;
    if (_freq.Count > MaxWords) PruneLowest();
}
```

---

## AppConfig.cs 수정

```csharp
// 기존 AutoCompleteEnabled 에 아래 추가
public bool KoreanAutoCompleteEnabled { get; set; } = false;  // 한글 자동 완성 별도 토글
```

---

## 에셋 파일 추가

`AltKey/Assets/Data/ko-words.txt` 신규 생성 필요.

**데이터 생성 방법:**
1. [hermitdave/FrequencyWords](https://github.com/hermitdave/FrequencyWords) 의 `ko_50k.txt` 다운로드
2. 상위 10,000개 추출: `head -10000 ko_50k.txt | awk '{print $2}' > ko-words.txt`
3. 또는 [NIKL 국립국어원 어휘 목록](https://corpus.korean.go.kr/) 공개 데이터 활용

`.csproj`에 추가:
```xml
<ItemGroup>
  <EmbeddedResource Include="Assets\Data\ko-words.txt" />
</ItemGroup>
```

---

## 구현 단계 체크리스트

- [ ] **Step 1** — `Services/HangulComposer.cs` 신규 작성 (두벌식 자모 조합 상태 머신)
- [ ] **Step 2** — `Assets/Data/ko-words.txt` 한국어 빈도 단어 파일 추가 + `.csproj` EmbeddedResource 등록
- [ ] **Step 3** — `Services/KoreanDictionary.cs` 신규 작성 (내장 사전 + 사용자 학습 레이어)
- [ ] **Step 4** — `Services/WordFrequencyStore.cs` 수정 (한글 단어 학습 지원)
- [ ] **Step 5** — `Services/AutoCompleteService.cs` 수정 (`OnHangulInput` 추가, 한/영 분기 처리)
- [ ] **Step 6** — `KeyboardViewModel.cs` 수정 (한글 키 클릭 시 `OnHangulInput` 호출)
- [ ] **Step 7** — `App.xaml.cs`에 `KoreanDictionary` DI 등록
- [ ] **Step 8** — `AppConfig.cs`에 `KoreanAutoCompleteEnabled` 추가
- [ ] **Step 9** — `SettingsView.xaml`에 한글 자동 완성 토글 추가
- [ ] **Step 10** — 단위 테스트: `HangulComposer` 자모 조합 케이스 (가나다, 닭볶음 등)

---

## 주의사항 및 엣지 케이스

| 상황 | 처리 방법 |
|------|----------|
| 한글/영문 혼용 (예: "Wi-Fi 연결") | `OnKeyInput` 호출 시 `_isHangulMode` 플래그로 분기 |
| 쌍자음 입력 (ㄲ, ㄸ 등 shift+자음) | `HangulComposer`가 쌍자음을 단일 초성으로 인식 |
| 겹받침 (닭 → ㄺ) | `Jongseong` 테이블에 겹받침 포함 — `Feed` 내부에서 처리 |
| IME 모드와 충돌 | AltKey 내부 추적이므로 OS IME 상태와 무관. 단, 사용자가 키보드를 통하지 않고 IME로 직접 입력하면 추적 안 됨 |
| 제안 수락 시 나머지 문자 입력 | `SendUnicode(remaining)` — 이미 `SuggestionBarViewModel`에 구현됨 |
