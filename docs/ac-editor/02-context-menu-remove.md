# 작업 02 — 제안 바 우클릭으로 사용자 사전에서 단어 제거

> **이 문서의 목표**: 자동완성 제안 바(`SuggestionBar`)의 각 제안 버튼에 **우클릭 컨텍스트 메뉴**를 달고, 그 메뉴에서 "단어 제거"를 선택하면 해당 제안 단어를 `WordFrequencyStore`에서 즉시 제거한다.
>
> **선행 읽기**: [00-overview.md](00-overview.md) §3·§4, [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2.

---

## 0. TL;DR

- `SuggestionBar.xaml`의 `DataTemplate` 안 `Button`에 `ContextMenu`를 추가한다.
- 메뉴 항목 "단어 제거"의 `Command`는 `SuggestionBarViewModel.RemoveSuggestionCommand`에 바인딩한다.
- `SuggestionBarViewModel`에 `[RelayCommand] RemoveSuggestion(string word)`을 추가, 내부에서 `KoreanDictionary` 또는 `EnglishDictionary`의 사용자 저장소를 접근해 제거한다.
- `WordFrequencyStore`에 공개 메서드 **`RemoveWord(string word)`**를 추가한다 ([00-overview.md](00-overview.md) §4 API 설계).
- 내장 사전(ko-words.txt / en-words.txt)에만 있는 단어는 **제거할 수 없음**이 자연스럽다(리소스 파일). 이 경우 "내장 단어이므로 제거할 수 없습니다"라는 짧은 토스트·상태 메시지를 보이거나, 간단히 메뉴 항목을 회색 처리한다 — 본 작업에서는 **"학습된 단어만 제거 가능, 나머지는 메뉴가 뜨지 않거나 비활성"** 방침을 택한다.
- 제거 후 `SuggestionsChanged` 이벤트로 제안 바를 즉시 갱신한다. 기존 제안 목록에서 해당 단어가 사라져야 함.

---

## 1. 배경과 동기

### 1.1 현재 동작

[`AltKey/Views/SuggestionBar.xaml`](../../AltKey/Views/SuggestionBar.xaml)에서 각 제안은 단순 `Button`으로 렌더링된다:

```xaml
<ItemsControl ItemsSource="{Binding Suggestions}">
  <ItemsControl.ItemTemplate>
    <DataTemplate>
      <Button Content="{Binding}"
              Command="{Binding DataContext.AcceptSuggestionCommand, ...}"
              CommandParameter="{Binding}"
              ... />
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

좌클릭은 `AcceptSuggestionCommand`로 연결되어 해당 단어를 입력 필드에 삽입하고 `WordFrequencyStore`의 빈도를 1 증가시킨다. **우클릭에 대한 처리는 없다**.

### 1.2 사용자가 기대하는 동작

- 사용자가 제안 바에서 원치 않는 단어("헹볶" 같은 오타, 더 이상 쓰지 않는 용어 등)를 우클릭한다.
- 컨텍스트 메뉴가 뜨고 "단어 제거" 항목이 보인다.
- 이를 클릭하면 **확인 없이 즉시** 해당 단어가 사용자 사전에서 지워지고, 제안 바에서도 그 단어가 사라진다.
- 실수 제거 방지를 위해 **Ctrl+Z 같은 undo는 요구하지 않는다** (설정 창의 편집기에서 다시 추가 가능). 다만 실수 방지를 위해 메뉴 항목 텍스트를 "단어 제거 (사용자 사전)" 정도로 명확히 한다.

### 1.3 현 구조 제약

- `SuggestionBarViewModel`은 현재 `AutoCompleteService`와 `InputService`, `ConfigService`만 주입받는다 ([line 25](../../AltKey/ViewModels/SuggestionBarViewModel.cs)). 사전 클래스에 직접 접근하지 않는다.
- `KoreanDictionary`와 `EnglishDictionary`는 내부에 `_userStore` 필드로 `WordFrequencyStore`를 은닉하고 있고, 외부에서 이 저장소에 직접 쓰기(제거·수정)할 공개 경로가 없다.
- `AutoCompleteService`는 `IInputLanguageModule`을 얇게 감싸는 래퍼이며, 단어 제거 개념이 없다.

즉 이 작업은 다음 세 레이어를 관통해야 한다:

```
SuggestionBar.xaml  (ContextMenu XAML 추가)
   ↓
SuggestionBarViewModel  (RemoveSuggestionCommand 추가)
   ↓
KoreanDictionary / EnglishDictionary  (UserStore 노출 또는 TryRemove 메서드 추가)
   ↓
WordFrequencyStore  (RemoveWord 공개 메서드 추가)
```

---

## 2. 구현 전략

### 2.1 어떤 사전에서 제거할지를 어떻게 결정하나?

제안 버튼에는 단어 문자열만 있고, "이 제안이 한글 사전에서 왔는지 영어 사전에서 왔는지"를 직접 알 수는 없다. 다음 방식으로 해결:

**방식 A (권장)**: `SuggestionBarViewModel.RemoveSuggestion(word)`가 **현재 서브모드**를 판단해 적절한 사전을 고른다.

- `IInputLanguageModule.ActiveSubmode`가 `InputSubmode.HangulJamo`이면 `KoreanDictionary`의 user store에서 제거.
- `InputSubmode.QuietEnglish`이면 `EnglishDictionary`의 user store에서 제거.
- `AutoCompleteService`가 `ActiveSubmode`를 이미 노출하고 있다 ([line 39](../../AltKey/Services/AutoCompleteService.cs)).

**방식 B**: 두 사전 모두 시도. 한쪽에서만 성공하면 그쪽에서 제거된 것으로 간주.

- 장점: 서브모드 판단 불필요.
- 단점: 같은 단어가 두 사전에 동시에 있을 수 있는 엣지 케이스(영숫자 혼합 같은)에서 예측 불가. 또 불필요한 IO.

**결론**: 방식 A. 다만 구현은 "서브모드 단일 선택" + "해당 사전 Store에서만 제거 시도"로 단순하게.

### 2.2 `KoreanDictionary`/`EnglishDictionary`에 `TryRemoveUserWord` 메서드를 추가할까, 아니면 `UserStore`를 그대로 노출할까?

**권장 방식**: 사전 클래스에 **`TryRemoveUserWord(string word)` 래퍼 메서드**를 추가.

- 장점: 캡슐화 유지. 정책(예: "학습 필터를 통과한 단어만 제거 가능" 같은 미래 규칙)을 사전 클래스가 소유.
- 단점: 사전 클래스 두 개에 동일한 메서드를 복제해야 함 — 하지만 이미 `RecordWord`, `GetSuggestions`, `Flush`도 같은 시그니처로 복제되어 있어 일관성 있음.

구현 예시:

```csharp
// KoreanDictionary.cs
public bool TryRemoveUserWord(string word) => _userStore.RemoveWord(word);

// EnglishDictionary.cs
public bool TryRemoveUserWord(string word) => _userStore.RemoveWord(word.ToLowerInvariant());
```

> 영어는 `RecordWord`에서 `ToLowerInvariant()`로 소문자 정규화되므로, 제거 시에도 같은 정규화를 거쳐야 실제 저장소의 키와 매칭된다 ([`EnglishDictionary.cs:41`](../../AltKey/Services/EnglishDictionary.cs)). 이 불일치를 놓치면 "제거해도 다시 뜨는" 버그가 난다.

### 2.3 제거 후 제안 바를 어떻게 새로고침하나?

두 가지 방법:

**방식 A**: 제거 직후 `ViewModel.Suggestions.Remove(word)`로 `ObservableCollection`에서 직접 제거.

- 장점: 즉시 반응.
- 단점: 다음 키 입력이 오기 전까지 새 제안이 채워지지 않음 (UI에 빈 공간).

**방식 B**: `AutoCompleteService`를 통해 현재 `CurrentWord`의 제안을 다시 요청, `SuggestionsChanged` 발행.

- 장점: 제거된 자리에 다음 후보가 자동으로 올라옴.
- 단점: 사전 클래스에 "특정 prefix의 제안 다시 가져오기" API가 이미 있으므로(`GetSuggestions`) 쉽게 가능하지만, `AutoCompleteService`에 그 경로가 뚫려 있지 않음 — 추가 작업 필요.

**결론**: **방식 A를 기본**으로 하고, 가능하면 방식 B의 이점(다음 후보 자동 채움)도 작은 확장으로 얹는다. 구체적으로:

1. `Suggestions.Remove(word)`로 즉시 UI에서 제거.
2. `ViewModel`이 현재 조합 중인 단어(`_autoComplete.CurrentWord`)를 알고 있으므로, 필요 시 사전을 다시 조회해 새 후보를 `Suggestions.Add(...)`로 추가 — 이 단계는 선택적. MVP는 Remove만으로도 충분.

MVP에서는 **방식 A만 구현**한다. 방식 B 확장은 작업 03에서 편집기를 만든 뒤 자연스럽게 추가될 수 있다.

### 2.4 내장 사전에만 있는 단어는?

`WordFrequencyStore.RemoveWord(word)`가 `false`를 반환하면(해당 단어가 user store에 없으면), ViewModel은:

- 다이얼로그나 MessageBox를 띄우지 않는다(과도한 UX).
- 그냥 조용히 스킵하고 `Suggestions.Remove(word)`도 **하지 않는다**(UI 상태와 실제 저장소 상태 일치 유지).
- 디버그 로그만 `Debug.WriteLine`으로 남긴다.

또는 더 정교하게, 컨텍스트 메뉴를 만들 때 "해당 단어가 user store에 있는지"를 미리 확인해 **없으면 메뉴 항목을 회색 처리**하는 것도 가능. 하지만 이를 위해 `WordFrequencyStore.Contains(word)`를 호출해야 하고 XAML 바인딩이 복잡해진다. **MVP는 메뉴를 항상 띄우고, 없는 단어면 조용히 무시**한다.

---

## 3. 상세 구현 지시

작업 순서: 레이어 아래부터 위로 (`WordFrequencyStore` → 사전 클래스 → ViewModel → XAML).

### 3.1 `WordFrequencyStore.RemoveWord` 추가

파일: [`AltKey/Services/WordFrequencyStore.cs`](../../AltKey/Services/WordFrequencyStore.cs)

`RecordWord` 바로 아래에 추가 (line 67 근처):

```csharp
/// 단어를 사용자 사전에서 제거. 존재하지 않으면 false.
/// 성공 시 디바운스 저장 예약.
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
```

**주의 사항**:

- `_saveLock`을 반드시 잡는다 (기존 `RecordWord` / `GetSuggestions` 패턴과 동일).
- `ScheduleSave()`는 **락 바깥**에서 호출 (데드락 방지, 기존 `RecordWord`도 그렇게 함).
- 반환값 `bool`: 호출자가 "실제 제거 여부"를 알 수 있도록.

> 작업 03(편집기)에서 `SetFrequency`, `GetAllWords`, `Clear`를 추가로 둘 예정이지만, 이 작업 02에서는 **`RemoveWord`만** 추가하면 된다. 나머지 API는 03 작업에서 책임진다(중복 추가 회피).

### 3.2 `WordFrequencyStoreTests.cs`에 테스트 추가

파일: [`AltKey.Tests/Services/WordFrequencyStoreTests.cs`](../../AltKey.Tests/Services/WordFrequencyStoreTests.cs) (파일 존재 가정, 없으면 신규 작성)

추가할 테스트 3개:

```csharp
[Fact]
public void RemoveWord_ExistingWord_Returns_True_And_Removes_Entry()
{
    var tmp = Path.Combine(Path.GetTempPath(), "altkey-test-" + Guid.NewGuid());
    Directory.CreateDirectory(tmp);
    try
    {
        var store = new WordFrequencyStore(tmp, "ko");
        store.RecordWord("해달");
        store.RecordWord("해달");  // 빈도 2
        Assert.True(store.Contains("해달"));

        var removed = store.RemoveWord("해달");

        Assert.True(removed);
        Assert.False(store.Contains("해달"));
    }
    finally { Directory.Delete(tmp, true); }
}

[Fact]
public void RemoveWord_NonExistingWord_Returns_False()
{
    var tmp = Path.Combine(Path.GetTempPath(), "altkey-test-" + Guid.NewGuid());
    Directory.CreateDirectory(tmp);
    try
    {
        var store = new WordFrequencyStore(tmp, "ko");
        var removed = store.RemoveWord("없는단어");
        Assert.False(removed);
    }
    finally { Directory.Delete(tmp, true); }
}

[Fact]
public void RemoveWord_Persists_After_Flush()
{
    var tmp = Path.Combine(Path.GetTempPath(), "altkey-test-" + Guid.NewGuid());
    Directory.CreateDirectory(tmp);
    try
    {
        var store = new WordFrequencyStore(tmp, "ko");
        store.RecordWord("해달");
        store.Flush();
        Assert.True(store.Contains("해달"));

        store.RemoveWord("해달");
        store.Flush();

        // 새 인스턴스로 로드해 영속성 확인
        var reloaded = new WordFrequencyStore(tmp, "ko");
        Assert.False(reloaded.Contains("해달"));
    }
    finally { Directory.Delete(tmp, true); }
}
```

### 3.3 `KoreanDictionary.TryRemoveUserWord` 추가

파일: [`AltKey/Services/KoreanDictionary.cs`](../../AltKey/Services/KoreanDictionary.cs)

`RecordWord` 아래 (line 117 근처)에 추가:

```csharp
/// 사용자 학습 저장소에서 단어를 제거. 내장 사전은 건드리지 않음.
/// 단어가 없거나 내장 전용이면 false.
public bool TryRemoveUserWord(string word) =>
    !string.IsNullOrWhiteSpace(word) && _userStore.RemoveWord(word.Trim());
```

**주의**:

- 한글은 대소문자 정규화가 의미 없고, `RecordWord`가 `Trim()`만 하므로 여기서도 `Trim()`만 적용.
- `_userStore`는 이미 `WordFrequencyStore` 인스턴스이므로 추가 주입 불필요.
- 내장 사전(`_builtIn`)은 리소스에서 로드된 읽기 전용 — 절대 수정하지 말 것. `_userStore`만 건드린다.

### 3.4 `EnglishDictionary.TryRemoveUserWord` 추가

파일: [`AltKey/Services/EnglishDictionary.cs`](../../AltKey/Services/EnglishDictionary.cs)

`RecordWord` 아래 (line 43 근처)에 추가:

```csharp
/// 사용자 학습 저장소에서 단어를 제거 (소문자 정규화).
public bool TryRemoveUserWord(string word)
{
    if (string.IsNullOrWhiteSpace(word)) return false;
    return _userStore.RemoveWord(word.Trim().ToLowerInvariant());
}
```

**주의**: `RecordWord`에서 `ToLowerInvariant()`로 정규화하므로, 제거할 때도 같은 정규화를 반드시 적용. "Hello"를 제거하려 했는데 저장소에는 "hello"로 들어 있으면 실패하는 버그를 방지.

### 3.5 `SuggestionBarViewModel`에 `RemoveSuggestion` 커맨드 추가

파일: [`AltKey/ViewModels/SuggestionBarViewModel.cs`](../../AltKey/ViewModels/SuggestionBarViewModel.cs)

#### (1) using과 필드

파일 상단의 using은 그대로. 다만 `AltKey.Models;`의 `InputSubmode`를 사용하므로 이미 존재. 필드 영역에 사전 두 개 주입 필요:

```csharp
private readonly KoreanDictionary  _koDict;
private readonly EnglishDictionary _enDict;
```

위치: 기존 `_autoComplete`, `_inputService`, `_configService` 아래.

#### (2) 생성자 시그니처 확장

기존 (line 25-34):

```csharp
public SuggestionBarViewModel(AutoCompleteService autoComplete, InputService inputService, ConfigService configService)
{
    _autoComplete = autoComplete;
    _inputService = inputService;
    _configService = configService;
    _autoComplete.SuggestionsChanged += OnSuggestionsChanged;
    _configService.ConfigChanged += OnConfigChanged;
    SetVisibleFromConfig();
}
```

변경 후:

```csharp
public SuggestionBarViewModel(
    AutoCompleteService autoComplete,
    InputService inputService,
    ConfigService configService,
    KoreanDictionary koDict,
    EnglishDictionary enDict)
{
    _autoComplete = autoComplete;
    _inputService = inputService;
    _configService = configService;
    _koDict = koDict;
    _enDict = enDict;
    _autoComplete.SuggestionsChanged += OnSuggestionsChanged;
    _configService.ConfigChanged += OnConfigChanged;
    SetVisibleFromConfig();
}
```

DI 컨테이너(`App.xaml.cs`)는 자동으로 해결하므로 등록 변경 불필요.

#### (3) `RemoveSuggestionCommand` 추가

기존 `AcceptSuggestion` 아래(line 72 이후)에 추가:

```csharp
/// 우클릭 컨텍스트 메뉴에서 호출 — 사용자 사전에서 단어 제거.
/// 현재 서브모드에 따라 한글/영어 사전을 선택한다.
[RelayCommand]
private void RemoveSuggestion(string suggestion)
{
    if (string.IsNullOrWhiteSpace(suggestion)) return;

    bool removed = _autoComplete.ActiveSubmode == InputSubmode.HangulJamo
        ? _koDict.TryRemoveUserWord(suggestion)
        : _enDict.TryRemoveUserWord(suggestion);

    if (removed)
    {
        // UI 즉시 갱신 — ObservableCollection에서 바로 제거.
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            Suggestions.Remove(suggestion);
            HasSuggestions = Suggestions.Count > 0;
        });
    }
    // 제거 실패(내장 사전에만 있는 단어 등)는 조용히 무시.
}
```

**중요 포인트**:

- `_autoComplete.ActiveSubmode`: `AutoCompleteService`가 이미 노출한다 ([line 39](../../AltKey/Services/AutoCompleteService.cs)).
- UI 스레드 안전: `Dispatcher.Invoke`로 감싼다 (기존 `OnSuggestionsChanged` 패턴과 동일).
- `ObservableCollection.Remove(word)`는 `StringComparer.Ordinal`로 정확히 일치 매칭. 서브모드가 영어여도 UI에는 소문자 그대로 표시되므로 일치한다.
- 이 커맨드는 `[ObservableProperty]`로 생성된 `Suggestions` 컬렉션을 직접 수정한다. 이것은 안전(`SuggestionBarViewModel`이 소유권을 가진다)하지만, 혹시라도 `OnSuggestionsChanged`가 동시에 컬렉션을 새 인스턴스로 교체하는 레이스가 걱정되면 잠시 지역 참조를 잡는 것도 가능 — 현재 코드에서는 Dispatcher 단일 스레드라 불필요.

### 3.6 `SuggestionBar.xaml`에 `ContextMenu` 추가

파일: [`AltKey/Views/SuggestionBar.xaml`](../../AltKey/Views/SuggestionBar.xaml)

기존 `<Button>` 요소를 다음과 같이 확장한다. 변경 전:

```xaml
<Button Content="{Binding}"
        Command="{Binding DataContext.AcceptSuggestionCommand,
            RelativeSource={RelativeSource AncestorType=UserControl}}"
        CommandParameter="{Binding}"
        Padding="8,4" Margin="2,0"
        Background="Transparent"
        Foreground="{DynamicResource PanelFg}"
        BorderBrush="{DynamicResource PanelBorder}" BorderThickness="1"
        Cursor="Hand" FontSize="12"/>
```

변경 후:

```xaml
<Button Content="{Binding}"
        Command="{Binding DataContext.AcceptSuggestionCommand,
            RelativeSource={RelativeSource AncestorType=UserControl}}"
        CommandParameter="{Binding}"
        Padding="8,4" Margin="2,0"
        Background="Transparent"
        Foreground="{DynamicResource PanelFg}"
        BorderBrush="{DynamicResource PanelBorder}" BorderThickness="1"
        Cursor="Hand" FontSize="12"
        ToolTip="좌클릭: 입력 · 우클릭: 관리">
    <Button.ContextMenu>
        <ContextMenu>
            <MenuItem Header="사용자 사전에서 제거"
                      Command="{Binding PlacementTarget.DataContext.RemoveSuggestionCommand,
                          RelativeSource={RelativeSource AncestorType=ContextMenu}}"
                      CommandParameter="{Binding PlacementTarget.Content,
                          RelativeSource={RelativeSource AncestorType=ContextMenu}}"/>
        </ContextMenu>
    </Button.ContextMenu>
</Button>
```

**바인딩 경로 해설**:

- `ContextMenu`는 부모 `UserControl`의 Visual Tree 밖에 있어 `AncestorType=UserControl`로 올라갈 수 없다.
- 대신 `ContextMenu`의 `PlacementTarget`이 현재 클릭된 `Button`을 가리킨다.
- `Button.DataContext`는 부모 `ItemsControl` 상속을 따라 현재 제안 문자열(`string`)이지만, 우리가 원하는 건 **ViewModel**의 `RemoveSuggestionCommand`이다. 따라서:
  - `PlacementTarget.DataContext.RemoveSuggestionCommand`: `Button.DataContext`는 제안 문자열이므로 여기에 Command가 없다. 대신 `Button`이 속한 `ItemsControl`의 DataContext(즉 VM)를 잡아야 한다.
  - 더 견고한 패턴:

```xaml
<MenuItem Header="사용자 사전에서 제거"
          Command="{Binding Path=DataContext.RemoveSuggestionCommand,
              Source={x:Reference SuggestionRoot}}"
          CommandParameter="{Binding}"/>
```

이 경우 `UserControl` 최상위에 `x:Name="SuggestionRoot"`를 붙인다:

```xaml
<UserControl x:Class="AltKey.Views.SuggestionBar"
             x:Name="SuggestionRoot"
             ... >
```

그리고 `DataTemplate` 안에서 `Binding` (제안 문자열)을 그대로 CommandParameter로, 명명 참조로 VM을 가져온다.

**최종 권장 XAML**:

`UserControl` 태그에 `x:Name="SuggestionRoot"` 추가, 그리고 `ContextMenu` 블록은:

```xaml
<Button.ContextMenu>
    <ContextMenu>
        <MenuItem Header="사용자 사전에서 제거"
                  Command="{Binding DataContext.RemoveSuggestionCommand,
                      Source={x:Reference SuggestionRoot}}"
                  CommandParameter="{Binding}"/>
    </ContextMenu>
</Button.ContextMenu>
```

`CommandParameter="{Binding}"`은 현재 `DataTemplate`의 DataContext(제안 문자열)를 전달 — 이것이 `RemoveSuggestion(string suggestion)` 파라미터로 들어간다.

**왜 `x:Reference`를 쓰는가**: `ContextMenu`는 Popup으로 부모 Visual Tree에서 분리되므로 `RelativeSource`로 `UserControl`을 찾을 수 없다. `x:Reference`는 XAML 이름 범위 기반이라 Popup 내부에서도 참조 가능하다.

### 3.7 `App.xaml.cs` DI 등록 확인

[`AltKey/App.xaml.cs`](../../AltKey/App.xaml.cs) — **변경 불필요**. `KoreanDictionary`, `EnglishDictionary`, `SuggestionBarViewModel` 모두 이미 등록되어 있다 (line 65-80). 생성자 시그니처가 바뀌면 DI가 자동으로 새 의존성을 주입한다.

---

## 4. 테스트 전략

### 4.1 단위 테스트 (필수)

`WordFrequencyStoreTests.cs`에 §3.2의 3개 테스트 추가 (이미 명시).

사전 클래스에 대한 테스트도 간단히:

`AltKey.Tests/InputLanguage/KoreanDictionaryTests.cs` (파일 존재 가정):

```csharp
[Fact]
public void TryRemoveUserWord_Removes_Only_From_User_Store_Not_BuiltIn()
{
    var tmp = Path.Combine(Path.GetTempPath(), "altkey-test-" + Guid.NewGuid());
    Directory.CreateDirectory(tmp);
    try
    {
        var dict = new KoreanDictionary(lang => new WordFrequencyStore(tmp, lang));

        dict.RecordWord("해달테스트");  // 내장사전에 없는 커스텀 단어
        Assert.Contains("해달테스트", dict.GetSuggestions("해달"));

        Assert.True(dict.TryRemoveUserWord("해달테스트"));
        Assert.DoesNotContain("해달테스트", dict.GetSuggestions("해달"));

        // 내장 사전에 있는 단어는 false (user store에 없음)
        Assert.False(dict.TryRemoveUserWord("사랑"));
    }
    finally { Directory.Delete(tmp, true); }
}
```

`EnglishDictionaryTests.cs`(있다면)에도 유사 테스트 추가:

```csharp
[Fact]
public void TryRemoveUserWord_Normalizes_To_LowerCase()
{
    var dict = new EnglishDictionary(...);
    dict.RecordWord("Hello");  // 내부적으로 "hello" 저장
    Assert.True(dict.TryRemoveUserWord("HELLO"));  // 대문자로 시도해도 성공해야 함
}
```

### 4.2 수동 검증 (포터블 빌드 기준)

1. 앱 실행 후 자동완성 토글을 ON으로 설정.
2. 메모장에 "바나나 우유" 같은 사용자 특화 단어를 입력하고 공백 → 학습 발생.
3. 같은 prefix로 다시 입력 시 제안 바에 "바나나" 등이 보이는지 확인.
4. 제안 버튼을 **우클릭** → 컨텍스트 메뉴 표시 확인.
5. "사용자 사전에서 제거" 클릭 → 제안 바에서 해당 단어가 즉시 사라지는지 확인.
6. 같은 prefix를 다시 입력해도 제거한 단어가 제안에 나오지 않는지 확인.
7. 앱 재시작 후 같은 prefix → 여전히 나오지 않는지(영속성) 확인.
8. QuietEnglish 서브모드("가/A" 토글)에서도 동일한 플로우로 영어 단어 제거 확인.
9. 내장 사전에만 있는 단어("사랑" 등) 우클릭 → 메뉴 클릭해도 "조용히 무시"되는지 확인 (제안 바에 그대로 남아 있어야 함).

### 4.3 접근성 검증

- [ ] NVDA/Windows 내레이터로 제안 버튼에 포커스 시 "사용자 사전에서 제거" 메뉴 항목이 키보드(Shift+F10 또는 컨텍스트 메뉴 키)로 열리는지 확인.
- [ ] `ToolTip="좌클릭: 입력 · 우클릭: 관리"`가 읽히는지 확인.
- [ ] 메뉴 항목의 `Header` 텍스트가 명확한지 확인.

---

## 5. 수락 기준

- [ ] `dotnet build AltKey.csproj` 성공.
- [ ] `dotnet test AltKey.Tests.csproj` 전부 녹색. 신규 테스트(§3.2의 3개 + 사전 클래스 1~2개) 포함.
- [ ] 수동 검증 §4.2의 9단계 수행 결과 보고 (실행 환경 제약으로 못 한 단계는 명시).
- [ ] `SuggestionBar.xaml.cs` code-behind에 이벤트 핸들러를 직접 넣지 않았는지 확인 — 모든 로직은 ViewModel + RelayCommand 경로.
- [ ] 접근성 §4.3 항목 충족.

## 6. 회귀 금지

- [ ] 좌클릭(`AcceptSuggestion`) 동작이 이전과 동일하다: 해당 단어 삽입 + 빈도 1 증가.
- [ ] 제안 바의 시각적 스타일(테마 색상, 폰트 크기, 패딩)이 이전과 동일하다.
- [ ] 자동완성 토글 OFF일 때 제안 바 자체가 숨겨지는 기존 동작 유지 (`IsVisible` 바인딩).
- [ ] `CORE-LOGIC-PROTECTION.md` §2의 모든 항목(특히 `SendAtomicReplace`, `HangulComposer`, `AcceptSuggestion`의 BS 카운트)은 전혀 건드리지 않음.

---

## 7. 하면 안 되는 것

- `KoreanInputModule.cs` 수정 — 이 작업과 관련 없음. 01번 작업에서 `FinalizeComposition`·`AcceptSuggestion`의 `RecordWord` 호출부를 건드렸을 수 있지만, 02번에서는 그 영역을 다시 건드리지 않는다.
- `HangulComposer.cs` 수정 — 건드리지 않음.
- 제안 목록 **정렬 방식 변경** — `KoreanDictionary.GetSuggestions`의 빈도 내림차순 + 내장사전 보충 규칙은 유지.
- MessageBox 또는 ConfirmDialog로 "정말 제거하시겠습니까?" 묻기 — MVP는 즉시 제거. 사용자 피드백으로 "확인 다이얼로그 필요"라는 요청이 나중에 오면 그때 추가.
- 내장 사전(ko-words.txt / en-words.txt) 수정 — 리소스는 건드리지 않음.
- `WordFrequencyStore`에 `SetFrequency`, `GetAllWords`, `Clear` 같은 작업 03용 API를 같이 추가 — 이 작업은 `RemoveWord`만. 나머지는 03에서.

---

## 8. FAQ

### Q1. `ObservableCollection.Remove(word)`가 `string` 비교를 어떻게 하는가?

기본 `EqualityComparer<string>.Default`를 쓰므로 `string.Equals` 즉 대소문자 구분 비교. 영어의 경우 제안 바에는 `_userStore`에 저장된 소문자 그대로 올라오므로 일치한다. 한글은 정규화 이슈가 없음.

### Q2. 우클릭이 좌클릭과 동시에 발생하는 경우가 있나?

WPF의 `Button` 기본 동작상 좌클릭은 `Click` 이벤트, 우클릭은 `ContextMenu`로 분기된다. `Command` 바인딩은 좌클릭(기본 `Click`)에만 반응한다. 따라서 우클릭해도 `AcceptSuggestionCommand`가 실행되지 않는다 — 이 부분을 추가 테스트로 직접 확인할 것(§4.2 4·5단계).

### Q3. `x:Reference`를 못 쓰는 환경이 있나?

WPF는 `x:Reference`를 정상 지원한다. 다만 `{x:Reference}` 사용 시 순환 참조가 있으면 디자인 타임에 경고가 날 수 있다 — 실제 런타임에는 문제 없음.

### Q4. 내장 사전에만 있는 단어 우클릭 → 메뉴 클릭 시 사용자에게 알려야 하지 않나?

MVP는 알리지 않는다. 근거: 대부분의 단어가 user store에 있을 것이고, 가끔 내장 전용 단어가 뜨는 경우는 조용히 실패해도 사용자는 "그냥 다시 뜨는구나" 정도로 이해한다. 명시적 알림을 원하면 작업 03(편집기)에서 "내장 사전" 탭을 둬 명확히 구분하는 것이 낫다.

### Q5. 제거 후 제안이 비면 빈 바가 이상해 보인다.

맞다. 하지만 다음 키 입력(자모 추가 또는 BS)이 오면 `SuggestionsChanged`가 새 제안을 공급한다. MVP 체감상 큰 문제 아님. 향후 `SuggestionBarViewModel`에 "현재 prefix로 다시 제안 조회" 유틸을 두어 자동 채움을 할 수 있다 — 작업 03 이후 고려.

---

## 9. 작업 완료 후 보고

- 변경·추가한 파일 목록 (경로 기준).
- 추가한 테스트 개수와 이름.
- `dotnet test` 결과 (통과/실패 수).
- 수동 검증 §4.2의 단계별 결과.
- UI 변경 스크린샷(선택) — 컨텍스트 메뉴가 뜬 상태.

## 10. 다음 작업

이 작업 완료 후 [03-user-dictionary-editor.md](03-user-dictionary-editor.md)로 이동 — 설정 창에서 여는 사용자 사전 GUI 편집기. 본 작업에서 추가한 `WordFrequencyStore.RemoveWord`를 그대로 재사용하고, 추가로 `SetFrequency`·`GetAllWords`·`Clear`를 덧붙인다.
