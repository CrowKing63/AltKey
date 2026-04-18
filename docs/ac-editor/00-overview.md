# 사용자 단어 관리 기능 — 작업 전체 개요

> **이 폴더의 역할**: 자동완성 사용자 단어(user dictionary) 관리 기능을 위한 세 가지 하위 기능을 구현하기 위한 작업 지시서 모음. 각 지시서는 독립된 AI 에이전트가 다른 지시서를 읽지 않고도 작업을 완수할 수 있도록 자기완결적으로 작성되어 있다. **이 개요 문서는 에이전트가 제일 먼저 읽어야 한다.**
>
> **전제**: 자동완성 핵심 로직 자체는 이미 안정화되어 있고, 이번 작업은 그 위에 **사용성**을 얹는 작업이다. 기존 조합 파이프라인(`KoreanInputModule`, `HangulComposer`, `SendAtomicReplace`)은 절대 건드리지 말 것.

---

## 0. TL;DR

이번 이터레이션의 목표는 "자동완성이 단어를 학습하긴 하는데, **사용자가 그 단어 목록을 통제할 수단이 없다**"는 현재 상태를 해결하는 것이다. 구체적으로 세 가지 기능을 순차적으로 구현한다.

| 순서 | 기능 | 난이도 | 지시서 |
|---|---|---|---|
| 1 | 자동완성 토글이 OFF일 때 단어 학습(RecordWord) 스킵 | 쉬움 | [01-conditional-learning.md](01-conditional-learning.md) |
| 2 | 제안 바의 제안 버튼에서 우클릭으로 사용자 사전에서 해당 단어 제거 | 중간 | [02-context-menu-remove.md](02-context-menu-remove.md) |
| 3 | 설정 창에서 사용자 사전 편집기(GUI 창)를 열어 단어 목록 조회·추가·삭제·편집 | 큼 | [03-user-dictionary-editor.md](03-user-dictionary-editor.md) |

**권장 순서**: 01 → 02 → 03. 01·02는 짧고 위험이 낮아 3번 기능의 기초 API(`WordFrequencyStore`의 공개 메서드 확장)를 먼저 마련하기에 좋다. 다만 03이 요구하는 API 일부를 02가 먼저 필요로 하므로(특히 `RemoveWord`), 02와 03은 API 설계가 약간 겹친다 — 자세한 내용은 §4 "공통 API 설계"를 볼 것.

---

## 1. 현재 상태 (2026-04-18 기준)

### 1.1 무엇이 이미 되어 있는가

- **자동완성 조합**: `KoreanInputModule` + `HangulComposer` + `InputService.SendAtomicReplace`의 Unicode 우회 파이프라인이 안정적으로 동작. 4회 재설계 끝에 확정된 구조이며 [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md)에 보호 규정이 명시되어 있다.
- **사용자 사전 저장소**: [`AltKey/Services/WordFrequencyStore.cs`](../../AltKey/Services/WordFrequencyStore.cs). 언어별 JSON(`user-words.ko.json`, `user-words.en.json`) 파일에 `단어: 빈도` 쌍을 저장. 1초 디바운스 + 원자적 쓰기(tmp + File.Move). 한글은 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`으로 유니코드 그대로 기록.
- **학습 트리거**: `KoreanInputModule.FinalizeComposition()` (공백·엔터·탭·구두점·Ctrl계열 조합키 도달 시), `KoreanInputModule.AcceptSuggestion(suggestion)` (제안 수락 시).
- **자동완성 토글**: `AppConfig.AutoCompleteEnabled` (기본값 `false`). `ConfigService.Update()`를 통해 변경되며 `ConfigChanged` 이벤트로 구독자에게 알림. 현재는 제안 바의 가시성(`SuggestionBarViewModel.IsVisible`)과 `InputService.Mode` 초기화에만 영향.
- **제안 바 UI**: [`AltKey/Views/SuggestionBar.xaml`](../../AltKey/Views/SuggestionBar.xaml) + [`AltKey/ViewModels/SuggestionBarViewModel.cs`](../../AltKey/ViewModels/SuggestionBarViewModel.cs). `ItemsControl` + `StackPanel`로 제안 버튼을 수평 나열.
- **설정 창**: [`AltKey/Views/SettingsView.xaml`](../../AltKey/Views/SettingsView.xaml) + [`AltKey/ViewModels/SettingsViewModel.cs`](../../AltKey/ViewModels/SettingsViewModel.cs). 메인 윈도우에 임베드되는 UserControl. "레이아웃 편집기 열기" 버튼이 이미 `OpenLayoutEditorCommand`로 별도 `Window`를 띄우는 패턴을 쓰고 있다 — 이번 작업의 3번 기능이 그대로 모방할 표본.
- **레이아웃 편집기 창**: [`AltKey/Views/LayoutEditorWindow.xaml`](../../AltKey/Views/LayoutEditorWindow.xaml) + [`AltKey/ViewModels/LayoutEditorViewModel.cs`](../../AltKey/ViewModels/LayoutEditorViewModel.cs). WPF `Window`, `CenterOwner`, MVVM 패턴. 다크 배경 `#FF1E1E2E`, 상단 툴바 + 중앙 편집 영역 + 하단 상태바 구조.

### 1.2 무엇이 비어 있는가 (이 작업이 채워야 할 것)

- **학습 조건 분기 없음**: 자동완성 토글이 OFF여도(`AutoCompleteEnabled == false`) `KoreanInputModule.FinalizeComposition()`은 그대로 `_koDict.RecordWord()`를 호출한다. 즉 "제안 바를 숨겨도 사용자 사전에는 계속 데이터가 쌓이는" 상태.
- **단어 제거 API 없음**: `WordFrequencyStore`에 `RemoveWord(string word)`가 없다. 빈도 덮어쓰기(`SetFrequency`) 같은 편집 API도 없다.
- **제안 바 우클릭 핸들러 없음**: `SuggestionBar.xaml`의 `Button`에는 `ContextMenu`도 `PreviewMouseRightButtonDown`도 없다. 현재 우클릭하면 아무 일도 일어나지 않는다.
- **사용자 사전 편집기 창 없음**: `AltKey/Views/`에 `UserDictionaryEditorWindow.xaml`이 없고, `SettingsView.xaml`에서 이 창을 여는 버튼도 없다.

---

## 2. 이번 작업이 달성해야 할 사용자 시나리오

작업 완료 시점에 사용자는 다음을 할 수 있어야 한다.

1. **개인정보 보호 시나리오**: 자동완성 토글을 끄고 민감한 문장(비밀번호 힌트, 의료 용어 등)을 입력하면 사용자 사전에 아무것도 저장되지 않는다. 토글을 다시 켠 후 입력하는 단어만 학습된다.
2. **실수 제거 시나리오**: 제안 바에 "헹볶" 같은 오타를 누적해 학습한 경우, 그 제안 버튼을 **우클릭**해 컨텍스트 메뉴 "단어 제거"를 선택해 즉시 사용자 사전에서 지운다.
3. **대량 관리 시나리오**: 설정 창 → "사용자 단어 편집기 열기" 버튼 → 별도 창에서 저장된 단어 전체를 리스트로 조회하고, 검색하고, 다중 선택 삭제하고, 직접 단어를 추가하거나 빈도를 수정할 수 있다.

---

## 3. 핵심 설계 원칙 (세 작업 모두 공통)

### 3.1 자동완성 코어 로직은 건드리지 않는다

[`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2 "절대 건드리지 말 것" 목록 전체를 이 작업에서도 유지한다. 특히:

- `HangulComposer` 내부 알고리즘
- `KoreanInputModule.HandleKey()`의 분기 구조 (`isComboKey` 판단, `jamo==null` 처리 등)
- `InputService.SendAtomicReplace()`의 단일 `SendInput` 호출
- `KoreanInputModule.AcceptSuggestion()`의 BS 카운트 계산
- 제안 수락 후 `InputService.ResetTrackedLength()` 호출

이 작업에서 수정할 곳은 **그 바깥 레이어**(`WordFrequencyStore`의 공개 API, 사전 클래스의 일부 메서드, ViewModel, XAML)뿐이다.

### 3.2 MVVM 패턴을 따른다

- 상태는 ViewModel의 `[ObservableProperty]`로 관리한다 (CommunityToolkit.MVVM).
- UI 이벤트는 `[RelayCommand]`로 바인딩한다.
- 파일 IO나 저장소 조작은 ViewModel이 아닌 Service(`WordFrequencyStore`, 필요 시 신규 `UserDictionaryService`)에 둔다.
- DI 컨테이너(`App.xaml.cs`의 `ServiceCollection`)에 싱글톤으로 등록한다.

### 3.3 스레드 안전성

- `WordFrequencyStore`의 내부 `_freq` Dictionary는 `_saveLock`으로 보호되고 있다. 새로 추가하는 메서드도 **반드시 같은 락을 잡을 것**.
- UI 컬렉션 갱신은 `System.Windows.Application.Current.Dispatcher.Invoke(...)`로 감싼다 (이미 `SuggestionBarViewModel.OnSuggestionsChanged`가 이 패턴을 쓴다).

### 3.4 한국어 UX 우선

- 모든 UI 라벨·툴팁·확인 대화상자는 한국어로 작성한다.
- 버튼 텍스트는 짧고 명확하게 ("단어 제거", "저장", "닫기", "+ 추가" 등). 기존 `LayoutEditorWindow.xaml`·`SettingsView.xaml`의 톤을 따른다.
- 접근성 고려: 모든 상호작용 가능 컨트롤에 `AutomationProperties.Name` 또는 `ToolTip`을 지정한다.

### 3.5 저장은 ConfigService 패턴을 따른다

설정이 바뀌면 `_configService.Update(c => c.XXX = value)`를 호출해 (a) `AppConfig` 갱신, (b) JSON 저장, (c) `ConfigChanged` 이벤트 발행이 한꺼번에 일어난다. 사용자 단어 저장소는 `WordFrequencyStore`가 디바운스를 책임지지만, **명시적 대량 수정 후에는 `Flush()`를 호출**해 즉시 저장하도록 한다(에디터 창 닫기, 우클릭 삭제 등).

---

## 4. 공통 API 설계 — `WordFrequencyStore` 확장

세 작업이 공유하는 신규 공개 API는 다음과 같다. **01번 작업은 이 API를 건드리지 않고**, 02·03번 작업이 필요로 한다. 02 또는 03을 먼저 구현하는 에이전트가 이 절을 참고해 `WordFrequencyStore.cs`에 한 번에 추가하고, 다른 에이전트는 이미 추가되어 있으면 그대로 쓴다.

### 4.1 추가할 공개 메서드 시그니처

```csharp
// AltKey/Services/WordFrequencyStore.cs 에 추가

/// 단어를 사용자 사전에서 제거. 존재하지 않으면 false 반환.
/// 성공 시 디바운스 저장 예약.
public bool RemoveWord(string word);

/// 단어의 빈도를 특정 값으로 설정 (수동 편집용).
/// frequency <= 0 이면 RemoveWord와 동일한 효과.
/// 새 단어를 직접 추가할 때도 사용 (기본 빈도 1).
public void SetFrequency(string word, int frequency);

/// 모든 단어를 (word, frequency) 쌍으로 스냅샷 반환.
/// 편집기 창에서 목록을 표시할 때 사용. 빈도 내림차순, 같으면 단어 오름차순.
public IReadOnlyList<(string Word, int Frequency)> GetAllWords();

/// 저장소를 완전히 비운다 (사용자 확인 후에만 호출).
public void Clear();
```

### 4.2 구현 가이드

```csharp
public bool RemoveWord(string word)
{
    if (string.IsNullOrWhiteSpace(word)) return false;
    word = word.Trim();
    bool removed;
    lock (_saveLock)
    {
        removed = _freq.Remove(word);
    }
    if (removed) ScheduleSave();
    return removed;
}

public void SetFrequency(string word, int frequency)
{
    if (string.IsNullOrWhiteSpace(word)) return;
    word = word.Trim();
    if (word.Length == 0) return;
    lock (_saveLock)
    {
        if (frequency <= 0)
        {
            _freq.Remove(word);
        }
        else
        {
            _freq[word] = frequency;
            if (_freq.Count > MaxWords) PruneLowest();
        }
    }
    ScheduleSave();
}

public IReadOnlyList<(string Word, int Frequency)> GetAllWords()
{
    lock (_saveLock)
    {
        return _freq
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
    }
}

public void Clear()
{
    lock (_saveLock) { _freq.Clear(); }
    ScheduleSave();
}
```

### 4.3 주의 사항

- 위 네 메서드는 모두 기존 `_saveLock`을 잡아야 한다 (현재 파일의 `RecordWord`·`GetSuggestions` 등이 쓰는 락과 동일).
- `ScheduleSave()`는 락 바깥에서 호출한다 (현재 `RecordWord`도 그렇게 함). 이유: `_debounceTimer.Start()`가 내부적으로 락을 잡을 수 있어 데드락 위험이 있다.
- **절대 제거하지 말 것**: `_jsonOptions`의 `UnsafeRelaxedJsonEscaping`, `File.Move(tmp, _filePath, overwrite: true)` 원자적 쓰기, 1초 디바운스.
- 테스트는 `AltKey.Tests/Services/WordFrequencyStoreTests.cs`에 추가 (존재 확인: `tests`). 기존 테스트 스타일을 따른다.

### 4.4 테스트 명세 (신규 API)

각 지시서(특히 03)에서 구체화하지만, 공통 테스트로 다음은 반드시 포함.

```csharp
[Fact]
public void RemoveWord_ExistingWord_Returns_True_And_Decrements_Count() { ... }

[Fact]
public void RemoveWord_NonExistingWord_Returns_False() { ... }

[Fact]
public void SetFrequency_Zero_Removes_Word() { ... }

[Fact]
public void SetFrequency_Positive_Upserts_Word() { ... }

[Fact]
public void GetAllWords_Returns_Sorted_By_Frequency_Desc_Then_Word_Asc() { ... }

[Fact]
public void Clear_Empties_Store_And_Persists() { ... }
```

---

## 5. DI / 서비스 등록 가이드

[`AltKey/App.xaml.cs:43-87`](../../AltKey/App.xaml.cs)의 `ServiceCollection` 블록에 다음을 추가한다 (주로 03번 작업에서).

```csharp
// ac-editor 03: 사용자 단어 편집기
services.AddSingleton<UserDictionaryEditorViewModel>();
```

- `KoreanDictionary`, `EnglishDictionary`, `WordFrequencyStore` 팩토리는 이미 등록되어 있다 (66-67행 근처).
- 편집기 ViewModel은 `KoreanDictionary`와 `EnglishDictionary`를 통해서가 아니라, **`WordFrequencyStore`를 직접** 주입받는 것이 가장 깔끔하다. 다만 현재 `Func<string, WordFrequencyStore>` 팩토리가 매 호출마다 새 인스턴스를 만든다 — 편집기는 `KoreanDictionary`·`EnglishDictionary` 내부 `_userStore`와 같은 인스턴스를 공유해야 한다. 이 불일치는 03번 지시서에서 다룬다 (한 가지 해결책: `KoreanDictionary`에 `WordFrequencyStore UserStore => _userStore;` getter를 추가).

---

## 6. 빌드·테스트

- **빌드**: `C:\Users\UITAEK\AltKey\AltKey\AltKey.csproj` (`dotnet build`)
- **테스트**: `C:\Users\UITAEK\AltKey\AltKey.Tests\AltKey.Tests.csproj` (`dotnet test`)
- **PowerShell 환경 주의**: `&&` 대신 `;`를 사용하거나 명령을 분리해 실행.

작업 완료 후 반드시:

1. `dotnet build` 성공
2. 기존 테스트 모두 녹색 — 특히 `HangulComposerTests`, `KoreanInputModuleTests`, `WordFrequencyStoreTests`
3. 실제 실행해서 손으로 재현:
   - 토글 ON 상태에서 두 음절 한국어 입력 → 공백 → 재시작 후에도 제안에 나타남
   - 토글 OFF 상태에서 두 음절 한국어 입력 → 공백 → 재시작 후에도 **제안에 나타나지 않음**(01)
   - 제안 버튼 우클릭 → 단어 제거(02)
   - 편집기에서 단어 목록 조회·추가·삭제(03)

---

## 7. 파일 구조 요약 (작업 후)

이번 이터레이션 완료 시 변경·신규 파일:

```
AltKey/
├── Models/
│   └── AppConfig.cs                         (변경 없음 — 토글은 이미 있음)
├── Services/
│   ├── WordFrequencyStore.cs                (변경: RemoveWord, SetFrequency, GetAllWords, Clear 추가)
│   ├── KoreanDictionary.cs                  (변경 최소: UserStore getter 추가, RecordWord 스킵 인자 검토)
│   ├── EnglishDictionary.cs                 (변경 최소: UserStore getter 추가, RecordWord 스킵 인자 검토)
│   └── InputLanguage/
│       └── KoreanInputModule.cs             (변경: 토글 OFF 시 Record 스킵 — 주입 or 콜백으로)
├── ViewModels/
│   ├── SuggestionBarViewModel.cs            (변경: RemoveSuggestion RelayCommand 추가)
│   ├── SettingsViewModel.cs                 (변경: OpenUserDictionaryEditor RelayCommand 추가)
│   └── UserDictionaryEditorViewModel.cs     (신규)
├── Views/
│   ├── SuggestionBar.xaml                   (변경: 버튼에 ContextMenu 또는 PreviewMouseRightButtonDown)
│   ├── SuggestionBar.xaml.cs                (변경 또는 신규 필요 시)
│   ├── SettingsView.xaml                    (변경: "사용자 단어 편집기 열기" 버튼 추가)
│   ├── UserDictionaryEditorWindow.xaml      (신규)
│   └── UserDictionaryEditorWindow.xaml.cs   (신규)
└── App.xaml.cs                              (변경: UserDictionaryEditorViewModel DI 등록)

AltKey.Tests/
└── Services/
    └── WordFrequencyStoreTests.cs           (변경: Remove/SetFrequency/GetAll/Clear 테스트 추가)
```

---

## 8. 작업 시 에이전트가 지켜야 할 원칙

1. **먼저 [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2를 읽는다**. 거기에 명시된 코드 경로는 어떤 핑계로도 건드리지 않는다.
2. **작업 전에 현재 파일 내용을 읽고, 최근 커밋을 훑는다**. `git log --oneline -20 -- AltKey/Services/WordFrequencyStore.cs` 같은 식으로 최근 맥락을 확인.
3. **타 작업 지시서가 먼저 완료되었을 가능성을 항상 확인**한다. 예컨대 02를 시작할 때 `WordFrequencyStore.RemoveWord`가 이미 있을 수 있다 — 중복 구현하지 말고 기존 API를 재사용.
4. **테스트부터 작성할 수 있다면 작성**한다. 특히 `WordFrequencyStore`의 신규 메서드는 ViewModel보다 먼저 테스트로 격리 검증한다.
5. **UI 파일(XAML)은 `MVVM 바인딩` + `DynamicResource` 테마 패턴**을 유지한다. 하드코드 색상은 기존 `LayoutEditorWindow.xaml`이 쓰는 `#FF1E1E2E` 같은 값으로만 허용.
6. **접근성**: 모든 버튼·체크박스에 `ToolTip` 또는 `AutomationProperties.Name`을 붙인다. 시각 장애인이 NVDA 등으로 내비게이션할 때 이름이 읽히도록.
7. **빌드가 깨지면 멈춘다**. 중간에 `dotnet build`가 실패하면 즉시 원인을 고치고, "나중에 해결"로 남기지 않는다.
8. **커밋 메시지**는 `feat(ac-editor): ...` 또는 `feat(user-dict): ...` 같은 스코프를 사용한다. 최근 커밋 스타일(`cfc87a9 fix(autocomplete): reset TrackedOnScreenLength after accepting suggestion`) 참고.

---

## 9. 참고 문서

- [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) — **필독**. 자동완성 코드 보호 규정.
- [`docs/feature-korean-autocomplete.md`](../feature-korean-autocomplete.md) — 자동완성 기능 원래 설계 문서.
- [`docs/ime-korean-detection-problem.md`](../ime-korean-detection-problem.md) — Unicode 우회 채택 배경.
- [`AGENTS.md`](../../AGENTS.md) — 리포지토리 전역 에이전트 가이드.
- [`AltKey/Views/LayoutEditorWindow.xaml`](../../AltKey/Views/LayoutEditorWindow.xaml) — 새 편집기 창 작성 시 모방할 표본.

---

## 10. 다음 단계

이 개요를 읽은 에이전트는:

- 단일 작업만 할당받았다면 해당 번호의 지시서로 이동한다 ([01](01-conditional-learning.md), [02](02-context-menu-remove.md), [03](03-user-dictionary-editor.md)).
- 세 작업 모두 할당받았다면 **01 → 02 → 03** 순으로 진행하며, 02에서 §4의 `RemoveWord` API를 먼저 추가하고 03에서 나머지(`SetFrequency`, `GetAllWords`, `Clear`)를 추가하는 것이 가장 자연스럽다.
- 작업 중 예상치 못한 충돌(예: 개요에 없는 기존 코드가 작업을 막음)이 있으면 즉시 작업을 중단하고 사용자에게 보고한다. 임의로 범위를 확장하지 말 것.
