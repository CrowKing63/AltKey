# 04 — 서비스 / ViewModel / 사용자 사전 편집기 UI 확장

> **목적**: 01~03번에서 저장소·사전·모듈이 모두 연결된 상태에서, 나머지 경로(`AutoCompleteService`·`SuggestionBarViewModel`·`UserDictionaryEditorViewModel`)가 변경 없이도 올바르게 동작하는지 확인하고, 필요한 최소한의 UI 노출(사용자 사전 편집기의 "바이그램" 탭)을 추가한다.
>
> **선행 조건**: 01·02·03번 완료 + 모든 테스트 녹색 + 수동 시나리오 통과.
>
> **성격**: 대부분이 "변경 불필요 확인" + "편집기 UI 옵션". 편집기 UI는 이번 이터레이션에서 **선택(optional)**으로 둔다. 범위가 넓어지면 별도 릴리스로 분리.

---

## 1. 체크리스트

### 1.1 필수 (확인만)

- [ ] `AltKey/Services/AutoCompleteService.cs` — 변경 필요 없음을 확인.
- [ ] `AltKey/ViewModels/SuggestionBarViewModel.cs` — 변경 필요 없음을 확인.
- [ ] `AltKey/App.xaml.cs` — `BigramFrequencyStore` 팩토리가 등록되어 있는지, `KoreanDictionary`/`EnglishDictionary` 생성자에 팩토리 두 개가 모두 전달되는지 재확인.

### 1.2 선택 (편집기 확장)

- [ ] `AltKey/ViewModels/UserDictionaryEditorViewModel.cs`에 "바이그램" 탭용 상태·커맨드 추가.
- [ ] `AltKey/Views/UserDictionaryEditorWindow.xaml`에 TabControl을 넣고 "단어" / "바이그램" 두 탭을 분리.
- [ ] 바이그램 탭에서: 목록 표시(prev, next, count) + prev 전체 삭제 + 개별 쌍 삭제 기능.

---

## 2. 변경 불필요 확인 (§1.1)

### 2.1 `AutoCompleteService`

현재 구현([`AltKey/Services/AutoCompleteService.cs`](../../AltKey/Services/AutoCompleteService.cs))은 `_module`의 이벤트를 단순 위임한다. `SuggestionsChanged`는 `IReadOnlyList<string>`을 그대로 전달하므로, 03번에서 모듈이 이미 문맥 기반 제안을 만들어 이벤트를 발사한다. **서비스 레이어는 건드리지 않아도 된다.**

확인만:

```csharp
_module.SuggestionsChanged += list => SuggestionsChanged?.Invoke(list);
// 위 한 줄이 변경되지 않았고, _module가 올바른 리스트를 만들고 있음.
```

> **하지 말 것**: 서비스에 `LastContext` getter 같은 걸 추가하는 것. 외부에 노출하면 CORE-LOGIC-PROTECTION §2의 "얇은 래퍼" 규율이 깨진다.

### 2.2 `SuggestionBarViewModel`

([`AltKey/ViewModels/SuggestionBarViewModel.cs`](../../AltKey/ViewModels/SuggestionBarViewModel.cs)) `SuggestionsChanged` 이벤트를 받아 리스트만 바인딩한다. 03번에서 문맥 반영이 리스트 내용에만 녹아 들어가므로 **변경 불필요**.

수동 확인: `OnSuggestionsChanged`가 호출된 후 `Suggestions` 컬렉션이 UI에 반영되는지. 이 흐름은 `ac-editor` 이터레이션에서 이미 검증됨.

### 2.3 `App.xaml.cs`

02번에서 DI 등록이 이미 바뀌었어야 하지만, 본인이 04번만 담당하는 에이전트라면 다음을 재확인:

```csharp
// 팩토리
services.AddSingleton<Func<string, WordFrequencyStore>>(
    sp => lang => new WordFrequencyStore(lang));
services.AddSingleton<Func<string, BigramFrequencyStore>>(
    sp => lang => new BigramFrequencyStore(lang));

// 사전 싱글톤
services.AddSingleton<KoreanDictionary>(sp =>
    new KoreanDictionary(
        sp.GetRequiredService<Func<string, WordFrequencyStore>>(),
        sp.GetRequiredService<Func<string, BigramFrequencyStore>>()));

services.AddSingleton<EnglishDictionary>(sp =>
    new EnglishDictionary(
        sp.GetRequiredService<Func<string, WordFrequencyStore>>(),
        sp.GetRequiredService<Func<string, BigramFrequencyStore>>()));
```

+ 앱 종료 시 `BigramFrequencyStore.Flush()`가 호출되는지. `KoreanDictionary.Flush()`·`EnglishDictionary.Flush()`는 이미 종료 훅에서 호출되고 있을 것인데, 이들이 내부에서 `_userStore.Flush()`만 호출하고 있다면 **`_bigramStore.Flush()`도 함께 호출**하도록 02번 단계에서 수정되었어야 한다. 미반영이면 지금 추가:

```csharp
public void Flush()
{
    _userStore.Flush();
    _bigramStore.Flush();
}
```

---

## 3. 선택 기능: 사용자 사전 편집기 "바이그램" 탭

### 3.1 동기

- 사용자가 "헹볶" 같은 오염된 unigram을 지우고 싶어 하는 니즈가 이미 `ac-editor` 이터레이션에서 확인됐다. bigram도 같은 문제를 가진다 — 잘못 입력된 쌍이 상위 제안을 오염시킬 수 있다.
- 진단 가치: 사용자가 "왜 이 제안이 자꾸 뜨지?"를 궁금해할 때, 편집기에서 (prev, next) 쌍 목록을 보는 것만으로도 원인을 알 수 있다.

### 3.2 UI 설계

```
UserDictionaryEditorWindow
├── TabControl
│   ├── Tab "단어"  (기존 UI 그대로)
│   └── Tab "바이그램"  (신규)
│        └── 필터 영역 (언어 한/영 라디오 버튼 또는 기존 재사용)
│        └── DataGrid (Prev | Next | Count | [삭제])
│        └── 하단 버튼: "선택된 이전 단어 전체 삭제", "모두 비우기"
└── 닫기 버튼
```

- 스타일은 기존 `UserDictionaryEditorWindow.xaml`의 다크 톤(`#FF1E1E2E`)과 DataGrid 레이아웃을 그대로 유지.
- "선택된 이전 단어 전체 삭제": 한 행 선택 시 그 행의 `Prev`에 속한 모든 next를 제거(`BigramFrequencyStore.RemoveAllFor`).
- "모두 비우기": 확인 대화상자 후 `BigramFrequencyStore.Clear()`.

### 3.3 ViewModel 상태·커맨드

```csharp
public partial class UserDictionaryEditorViewModel : ObservableObject
{
    // 기존 필드/커맨드들...

    // 신규
    [ObservableProperty]
    private ObservableCollection<BigramPairRow> bigramRows = [];

    [ObservableProperty]
    private BigramPairRow? selectedBigramRow;

    public void LoadBigrams()
    {
        var lang = CurrentLanguage;        // "ko" or "en" (기존 탭 상태 재활용)
        var store = lang == "ko" ? _koDict.BigramStore : _enDict.BigramStore;
        var pairs = store.GetAllPairs();
        BigramRows.Clear();
        foreach (var (prev, next, count) in pairs)
            BigramRows.Add(new BigramPairRow(prev, next, count));
    }

    [RelayCommand]
    private void RemoveBigramPair(BigramPairRow row)
    {
        var store = CurrentLanguage == "ko" ? _koDict.BigramStore : _enDict.BigramStore;
        if (store.RemovePair(row.Prev, row.Next))
            BigramRows.Remove(row);
    }

    [RelayCommand]
    private void RemoveBigramsByPrev(BigramPairRow row)
    {
        var store = CurrentLanguage == "ko" ? _koDict.BigramStore : _enDict.BigramStore;
        int removed = store.RemoveAllFor(row.Prev);
        if (removed > 0)
        {
            for (int i = BigramRows.Count - 1; i >= 0; i--)
                if (BigramRows[i].Prev == row.Prev) BigramRows.RemoveAt(i);
        }
    }

    [RelayCommand]
    private void ClearAllBigrams()
    {
        // 확인 대화상자는 View에서 처리하거나 여기에서 MessageBox 사용.
        var store = CurrentLanguage == "ko" ? _koDict.BigramStore : _enDict.BigramStore;
        store.Clear();
        BigramRows.Clear();
    }
}

public sealed record BigramPairRow(string Prev, string Next, int Count);
```

### 3.4 XAML 스케치 (발췌)

```xml
<TabControl Background="#FF1E1E2E">
    <TabItem Header="단어">
        <!-- 기존 UserDictionaryEditor 내용 그대로 이동 -->
    </TabItem>
    <TabItem Header="바이그램">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <!-- 상단 툴바: 언어 선택 재사용 -->
            <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="8">
                <RadioButton Content="한국어" IsChecked="{Binding IsKoreanSelected}"/>
                <RadioButton Content="English" IsChecked="{Binding IsEnglishSelected}"
                             Margin="12,0,0,0"/>
            </StackPanel>

            <DataGrid Grid.Row="1" ItemsSource="{Binding BigramRows}"
                      SelectedItem="{Binding SelectedBigramRow}"
                      AutoGenerateColumns="False" CanUserAddRows="False">
                <DataGrid.Columns>
                    <DataGridTextColumn Header="이전 단어" Binding="{Binding Prev}" Width="*"/>
                    <DataGridTextColumn Header="다음 단어" Binding="{Binding Next}" Width="*"/>
                    <DataGridTextColumn Header="빈도" Binding="{Binding Count}" Width="80"/>
                    <DataGridTemplateColumn Header="" Width="100">
                        <DataGridTemplateColumn.CellTemplate>
                            <DataTemplate>
                                <Button Content="삭제"
                                        Command="{Binding DataContext.RemoveBigramPairCommand,
                                                  RelativeSource={RelativeSource AncestorType=Window}}"
                                        CommandParameter="{Binding}"/>
                            </DataTemplate>
                        </DataGridTemplateColumn.CellTemplate>
                    </DataGridTemplateColumn>
                </DataGrid.Columns>
            </DataGrid>

            <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="8">
                <Button Content="선택된 이전 단어 전체 삭제"
                        Command="{Binding RemoveBigramsByPrevCommand}"
                        CommandParameter="{Binding SelectedBigramRow}" Margin="0,0,8,0"/>
                <Button Content="모두 비우기" Command="{Binding ClearAllBigramsCommand}"/>
            </StackPanel>
        </Grid>
    </TabItem>
</TabControl>
```

> 실제 파일에 반영할 때는 기존 네임스페이스·`DynamicResource` 테마 바인딩·MVVM 바인딩 스타일을 따른다. `LayoutEditorWindow.xaml`/`UserDictionaryEditorWindow.xaml`의 기존 구조를 먼저 읽고 모방.

### 3.5 접근성

- 모든 Button에 `AutomationProperties.Name` 또는 `ToolTip` 필수.
- DataGrid 컬럼은 기본 스크린 리더 지원이 있지만, 헤더 텍스트를 한국어로.
- 탭 이름 "단어"/"바이그램"은 짧고 명확.

---

## 4. 편집기 확장 생략 시의 조건

04번의 "선택" 부분을 이번 이터레이션에서 하지 않기로 결정했다면, 다음을 보장:

- [ ] 사용자가 bigram을 잘못 기록한 경우, 파일을 직접 편집해서 지울 수 있는 경로가 여전히 존재(`%AppData%\...\user-bigrams.ko.json` 수동 편집 가능).
- [ ] 향후 확장을 위한 API(`BigramFrequencyStore.RemovePair`·`RemoveAllFor`·`GetAllPairs`)는 01번에서 이미 구현되어 있다(미래 UI 작업 차단 없음).
- [ ] 릴리스 노트(05번)에 "편집 UI는 차후 릴리스"로 명시.

---

## 5. 주의 사항

- **절대 하지 말 것**: `AutoCompleteService`가 `BigramFrequencyStore`를 **직접** 주입받아 조회하는 경로 추가. 사전 레이어가 문맥을 책임지는 설계를 유지(CORE-LOGIC-PROTECTION §2의 "얇은 래퍼" 조항과 동치).
- **절대 하지 말 것**: `IInputLanguageModule` 인터페이스에 `LastCommittedWord` 같은 공개 속성 추가. 03번에서 이미 private 필드로 격리함.
- 편집기 UI에서 bigram을 대량 삭제·추가할 때는 변경 직후 `store.Flush()`를 호출해 즉시 디스크 반영(사용자가 편집기 닫기와 동시에 안전하게 저장되도록).

---

## 6. 완료 조건

- [ ] `AutoCompleteService` / `SuggestionBarViewModel`이 **변경되지 않은 채로** 제안 흐름이 동작(수동 확인 OK).
- [ ] `App.xaml.cs`의 DI 구성이 02번·03번 변경과 일치하게 갱신.
- [ ] `KoreanDictionary.Flush()` / `EnglishDictionary.Flush()`가 `_bigramStore.Flush()`도 호출.
- [ ] **선택 구현 시**: 사용자 사전 편집기에 "바이그램" 탭 + 삭제 3종(개별·prev 단위·전체) 동작.
- [ ] `dotnet build`·`dotnet test` 녹색.
- [ ] 커밋 메시지: `feat(ac-bigram): wire services and (optional) editor tab`.

---

## 7. 다음 단계

04 완료 후 [05-testing-release.md](05-testing-release.md)로 이동. 엔드투엔드 통합 테스트, 수동 QA 매트릭스, 릴리스 노트 문구를 마무리한다.
