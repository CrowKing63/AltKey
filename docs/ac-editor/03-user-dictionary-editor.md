# 작업 03 — 사용자 사전 GUI 편집기 창

> **이 문서의 목표**: 설정 창에 "사용자 단어 편집기 열기" 버튼을 추가하고, 클릭 시 별도 `Window`를 띄워 저장된 학습 단어 전체를 리스트로 보여주고 **조회·검색·추가·삭제·빈도 수정**이 가능한 GUI를 제공한다. `LayoutEditorWindow`와 같은 창 패턴을 따른다.
>
> **선행 읽기**: [00-overview.md](00-overview.md) §4·§5, [`docs/auto-complet/CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2.

---

## 0. TL;DR

- 신규 뷰: [`AltKey/Views/UserDictionaryEditorWindow.xaml`](../../AltKey/Views/UserDictionaryEditorWindow.xaml) (Window, CenterOwner).
- 신규 뷰모델: [`AltKey/ViewModels/UserDictionaryEditorViewModel.cs`](../../AltKey/ViewModels/UserDictionaryEditorViewModel.cs).
- `WordFrequencyStore`에 공개 API 추가: `SetFrequency`, `GetAllWords`, `Clear`. (`RemoveWord`는 작업 02에서 이미 추가됐을 가능성 높음 — 없다면 같이 추가.)
- 사전 클래스(`KoreanDictionary`, `EnglishDictionary`)에 내부 `WordFrequencyStore`를 안전하게 공개하는 getter 추가 (또는 편집 작업을 위임하는 메서드 세트 추가).
- 설정 창의 "레이아웃 편집기 열기" 버튼 바로 아래에 "사용자 단어 편집기 열기" 버튼 추가.
- 편집기 창은 **탭 2개** (한국어 / 영어)로 구분, 각 탭은 `DataGrid` 또는 `ListView`로 `(단어, 빈도)` 쌍 표시.
- 상단 툴바: 검색 상자, "+ 추가", "선택 항목 제거", "전체 삭제" 버튼.
- 하단 상태바: 총 단어 수 표시.
- 저장은 디바운스(1초) + 창 닫기 시 즉시 `Flush()`.

---

## 1. 배경과 동기

### 1.1 사용자가 해결하고자 하는 문제

작업 02(제안 바 우클릭 제거)만으로는 해결되지 않는 시나리오:

- **오타가 제안 바에 뜨지 않는데도 사전에는 남아 있음**: 예컨대 "헹복" 같은 오타가 빈도 1로 저장되어 "행복"보다 항상 뒤에 랭크될 때, 제안 바에는 안 뜨니 우클릭 불가.
- **대량 정리**: 수백 개 누적된 단어 중 특정 카테고리만 일괄 제거하고 싶을 때.
- **직접 추가**: 사용자가 자주 쓰는 전문용어를 미리 학습 없이 직접 넣고 싶을 때.
- **빈도 조정**: "가장 자주 쓰는 단어"를 최상단에 고정하고 싶을 때 빈도를 수동으로 올린다.
- **초기화**: 공용 컴퓨터에서 쓴 뒤 전부 지우고 싶을 때.

편집기는 이러한 대량·정밀 관리 요구를 모두 한 창에서 수용한다.

### 1.2 설계 참고 표본

[`AltKey/Views/LayoutEditorWindow.xaml`](../../AltKey/Views/LayoutEditorWindow.xaml) + [`AltKey/ViewModels/LayoutEditorViewModel.cs`](../../AltKey/ViewModels/LayoutEditorViewModel.cs)를 그대로 모방한다:

- 창 크기: 920×620, 최소 700×450 (편집기는 조금 작게 800×600 권장).
- `WindowStartupLocation="CenterOwner"`, 부모 창 지정.
- 배경색: `#FF1E1E2E` (다크).
- 상단 툴바(`#FF12121F`) + 중앙 편집 영역 + 하단 상태바(`#FF12121F`) 3단 구조.
- 버튼 스타일: `DarkBtn` (반투명 화이트), `AccentBtn` (파란색 `#3B82F6`).
- MVVM: 뷰모델이 `ObservableCollection<T>`을 소유, `[RelayCommand]`로 액션.
- 설정 창에서 버튼 한 개가 `OpenXxxCommand`를 통해 창을 `new ... { Owner = ... }.Show()`로 띄운다.

### 1.3 왜 설정 창에 탭을 두지 않고 별도 창을 쓰는가

설정 창은 임베드된 `UserControl`이고, 메인 키보드 창 안에 접혀 있는 패널이다. 스크롤 영역이 좁아 수백 개 단어 리스트를 다루기 어렵다. 또 `LayoutEditorWindow`도 같은 이유로 별도 `Window`다 — 일관성 유지.

---

## 2. 전제: `WordFrequencyStore` API 확장

[00-overview.md](00-overview.md) §4에서 제안한 네 가지 메서드 중 `RemoveWord`는 작업 02에서 추가되었을 가능성이 높다. 이 작업은 **나머지 세 가지**를 추가한다.

### 2.1 `WordFrequencyStore.cs`에 추가할 공개 메서드

파일: [`AltKey/Services/WordFrequencyStore.cs`](../../AltKey/Services/WordFrequencyStore.cs)

#### (1) `SetFrequency` (존재 시 작업 02 코드와 합쳐 중복 회피)

```csharp
/// 단어 빈도를 명시적으로 설정. <=0 이면 제거.
/// 새 단어 추가 겸용.
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
```

#### (2) `GetAllWords`

```csharp
/// 저장된 모든 단어의 스냅샷 반환. 빈도 내림차순, 같은 빈도는 단어 오름차순.
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
```

#### (3) `Clear`

```csharp
/// 저장소를 완전히 비운다 (UI 측 확인 대화상자 뒤에서만 호출할 것).
public void Clear()
{
    lock (_saveLock) { _freq.Clear(); }
    ScheduleSave();
}
```

#### (4) `RemoveWord` (작업 02 참조 — 없으면 추가)

[02-context-menu-remove.md](02-context-menu-remove.md) §3.1을 참고해 추가. 이미 있으면 스킵.

### 2.2 `WordFrequencyStoreTests.cs`에 신규 테스트

```csharp
[Fact]
public void SetFrequency_Zero_Removes_Word() { ... }

[Fact]
public void SetFrequency_Positive_Upserts_Word() { ... }

[Fact]
public void SetFrequency_TriggersPrune_WhenOverMax() { ... }

[Fact]
public void GetAllWords_Returns_SortedByFrequencyDesc_ThenWordAsc()
{
    var tmp = ...;
    var store = new WordFrequencyStore(tmp, "ko");
    store.RecordWord("가");  store.RecordWord("가");  // 빈도 2
    store.RecordWord("나");                              // 빈도 1
    store.RecordWord("다");                              // 빈도 1

    var all = store.GetAllWords();

    Assert.Equal(3, all.Count);
    Assert.Equal(("가", 2), all[0]);
    // 빈도 1은 "나" < "다" (문자열 순)
    Assert.Equal(("나", 1), all[1]);
    Assert.Equal(("다", 1), all[2]);
}

[Fact]
public void Clear_Empties_Store_Persistently()
{
    var tmp = ...;
    var store = new WordFrequencyStore(tmp, "ko");
    store.RecordWord("해달");
    store.Flush();
    store.Clear();
    store.Flush();
    var reloaded = new WordFrequencyStore(tmp, "ko");
    Assert.Equal(0, reloaded.Count);
}
```

`RecordWord`는 단어당 1씩 증가하므로 빈도 N을 만들려면 N번 호출하거나 `SetFrequency`로 직접 설정.

---

## 3. 사전 클래스에 Store 접근 노출

편집기 ViewModel은 **한국어 store와 영어 store 각각**에 접근해야 한다. 현재 `KoreanDictionary`와 `EnglishDictionary`는 `_userStore`를 private으로 은닉. 두 가지 해결책:

### 3.1 방식 A: `UserStore` 읽기 전용 속성 노출 (권장)

```csharp
// KoreanDictionary.cs, EnglishDictionary.cs 둘 다에 추가
public WordFrequencyStore UserStore => _userStore;
```

- 장점: 편집기 VM이 `dict.UserStore.GetAllWords()`로 직접 호출. 심플.
- 단점: 캡슐화가 살짝 느슨. 하지만 `WordFrequencyStore` 자체가 이미 락 보호되어 있어 안전.

### 3.2 방식 B: 사전 클래스에 편집 메서드 추가

```csharp
public IReadOnlyList<(string, int)> GetAllUserWords() => _userStore.GetAllWords();
public void SetUserWordFrequency(string w, int f) => _userStore.SetFrequency(...);
public void ClearUserWords() => _userStore.Clear();
public bool TryRemoveUserWord(string w) => _userStore.RemoveWord(...);
```

- 장점: 완전 캡슐화.
- 단점: 두 사전에 중복 코드, 편집기 VM이 사전별로 메서드 쌍을 맺어야 함(`koDict.GetAllUserWords()` vs `enDict.GetAllUserWords()`).

**권장**: **방식 A**. 두 사전 클래스에 `UserStore` getter만 달고, 편집기 VM이 `WordFrequencyStore`를 직접 다루게 한다. 영어는 대소문자 정규화가 필요한데, 이는 편집기 VM에서 명시적으로 처리(§5.5 참조).

---

## 4. 편집기 창 UI 디자인

### 4.1 레이아웃 개요

```
┌─────────────────────────────────────────────────────────────────┐
│  사용자 단어 편집기                          [X] ← 닫기        │
├─────────────────────────────────────────────────────────────────┤
│  [한국어] [영어]  ← 탭                                          │
├─────────────────────────────────────────────────────────────────┤
│  [🔍 검색: ________]   [+ 추가]  [선택 제거]  [전체 삭제]       │
├─────────────────────────────────────────────────────────────────┤
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  ☐ 단어           │ 빈도  │  편집                         │ │
│  │  ☐ 해달           │   5   │                                │ │
│  │  ☐ 바나나         │   3   │                                │ │
│  │  ☐ 우유           │   2   │                                │ │
│  │  ...                                                        │ │
│  └───────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────┤
│  총 124개 단어 · 저장됨 ·   [닫기]                              │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 `UserDictionaryEditorWindow.xaml` 전체 초안

파일: [`AltKey/Views/UserDictionaryEditorWindow.xaml`](../../AltKey/Views/UserDictionaryEditorWindow.xaml) (신규)

```xml
<Window x:Class="AltKey.Views.UserDictionaryEditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:vm="clr-namespace:AltKey.ViewModels"
        mc:Ignorable="d"
        Title="사용자 단어 편집기"
        Width="800" Height="600"
        MinWidth="600" MinHeight="450"
        WindowStartupLocation="CenterOwner"
        Background="#FF1E1E2E"
        d:DataContext="{d:DesignInstance vm:UserDictionaryEditorViewModel}">

    <Window.Resources>
        <Style x:Key="DarkBtn" TargetType="Button">
            <Setter Property="Background"      Value="#33FFFFFF"/>
            <Setter Property="Foreground"      Value="#EEFFFFFF"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding"         Value="10,5"/>
            <Setter Property="Margin"          Value="2,0"/>
            <Setter Property="Cursor"          Value="Hand"/>
        </Style>
        <Style x:Key="AccentBtn" TargetType="Button">
            <Setter Property="Background"      Value="#3B82F6"/>
            <Setter Property="Foreground"      Value="#FFFFFFFF"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding"         Value="10,5"/>
            <Setter Property="Margin"          Value="2,0"/>
            <Setter Property="Cursor"          Value="Hand"/>
        </Style>
        <Style x:Key="DangerBtn" TargetType="Button">
            <Setter Property="Background"      Value="#55FF4444"/>
            <Setter Property="Foreground"      Value="#FFEEAAAA"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Padding"         Value="10,5"/>
            <Setter Property="Margin"          Value="2,0"/>
            <Setter Property="Cursor"          Value="Hand"/>
        </Style>
        <Style x:Key="InputBox" TargetType="TextBox">
            <Setter Property="Background"  Value="#33FFFFFF"/>
            <Setter Property="Foreground"  Value="#EEFFFFFF"/>
            <Setter Property="BorderBrush" Value="#55FFFFFF"/>
            <Setter Property="Padding"     Value="6,4"/>
            <Setter Property="CaretBrush"  Value="#EEFFFFFF"/>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- ── 헤더 ── -->
        <Border Grid.Row="0" Background="#FF12121F" Padding="16,10">
            <Grid>
                <TextBlock Text="사용자 단어 편집기"
                           Foreground="#EEFFFFFF"
                           FontSize="14" FontWeight="SemiBold"
                           VerticalAlignment="Center"/>
                <Button HorizontalAlignment="Right"
                        Content="✕" FontSize="14"
                        Background="Transparent" Foreground="#AAFFFFFF"
                        BorderThickness="0" Cursor="Hand" Padding="8,2"
                        Click="CloseButton_Click"
                        ToolTip="닫기 (Esc)"
                        AutomationProperties.Name="닫기"/>
            </Grid>
        </Border>

        <!-- ── 탭 ── -->
        <Border Grid.Row="1" Background="#FF181826" Padding="12,4">
            <StackPanel Orientation="Horizontal">
                <RadioButton Content="한국어"
                             Foreground="#EEFFFFFF"
                             IsChecked="{Binding IsKoreanTabActive, Mode=TwoWay}"
                             Margin="0,0,12,0" Padding="6,3"
                             GroupName="DictTab"/>
                <RadioButton Content="영어"
                             Foreground="#EEFFFFFF"
                             IsChecked="{Binding IsEnglishTabActive, Mode=TwoWay}"
                             Padding="6,3"
                             GroupName="DictTab"/>
            </StackPanel>
        </Border>

        <!-- ── 툴바 ── -->
        <Border Grid.Row="2" Background="#FF12121F" Padding="12,8">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- 검색 + 추가 -->
                <StackPanel Grid.Column="0" Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="검색:" Foreground="#AAFFFFFF"
                               VerticalAlignment="Center" Margin="0,0,6,0"/>
                    <TextBox Text="{Binding SearchQuery, UpdateSourceTrigger=PropertyChanged, Delay=150}"
                             Style="{StaticResource InputBox}"
                             Width="200" Height="26" Margin="0,0,12,0"/>

                    <TextBox Text="{Binding NewWord, UpdateSourceTrigger=PropertyChanged}"
                             Style="{StaticResource InputBox}"
                             Width="150" Height="26" Margin="0,0,4,0"
                             ToolTip="추가할 단어 입력"/>
                    <Button Content="+ 추가"
                            Command="{Binding AddWordCommand}"
                            Style="{StaticResource AccentBtn}"/>
                </StackPanel>

                <!-- 제거 버튼 -->
                <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                    <Button Content="선택 항목 제거"
                            Command="{Binding RemoveSelectedCommand}"
                            Style="{StaticResource DarkBtn}"
                            ToolTip="체크된 단어를 삭제합니다"/>
                    <Button Content="전체 삭제"
                            Command="{Binding ClearAllCommand}"
                            Style="{StaticResource DangerBtn}"
                            ToolTip="현재 탭의 모든 사용자 단어를 삭제합니다"/>
                </StackPanel>
            </Grid>
        </Border>

        <!-- ── 목록 ── -->
        <DataGrid Grid.Row="3"
                  ItemsSource="{Binding FilteredWords}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False"
                  CanUserDeleteRows="False"
                  CanUserReorderColumns="False"
                  GridLinesVisibility="Horizontal"
                  HorizontalGridLinesBrush="#22FFFFFF"
                  Background="#FF1A1A2A"
                  Foreground="#EEFFFFFF"
                  RowBackground="Transparent"
                  AlternatingRowBackground="#0AFFFFFF"
                  BorderBrush="#33FFFFFF"
                  SelectionMode="Extended"
                  SelectionUnit="FullRow"
                  FontSize="12">
            <DataGrid.Columns>
                <DataGridCheckBoxColumn Header="✓"
                                        Binding="{Binding IsSelected, UpdateSourceTrigger=PropertyChanged}"
                                        Width="36"/>
                <DataGridTextColumn Header="단어"
                                    Binding="{Binding Word}"
                                    Width="*"
                                    IsReadOnly="True"/>
                <DataGridTemplateColumn Header="빈도" Width="100">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBox Text="{Binding Frequency, UpdateSourceTrigger=LostFocus}"
                                     Background="Transparent"
                                     Foreground="#EEFFFFFF"
                                     BorderThickness="0"
                                     HorizontalAlignment="Center"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
                <DataGridTemplateColumn Header="" Width="40">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <Button Content="✕"
                                    Command="{Binding DataContext.RemoveOneCommand,
                                        RelativeSource={RelativeSource AncestorType=Window}}"
                                    CommandParameter="{Binding}"
                                    Background="Transparent"
                                    Foreground="#88FFFFFF"
                                    BorderThickness="0"
                                    Cursor="Hand"
                                    FontSize="11"
                                    ToolTip="이 단어 제거"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
            </DataGrid.Columns>
        </DataGrid>

        <!-- ── 상태바 ── -->
        <Border Grid.Row="4" Background="#FF12121F" Padding="12,8">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0"
                           Text="{Binding StatusText}"
                           Foreground="#AAFFFFFF" FontSize="11"
                           VerticalAlignment="Center"/>
                <Button Grid.Column="1"
                        Content="닫기"
                        Click="CloseButton_Click"
                        Style="{StaticResource DarkBtn}"
                        AutomationProperties.Name="닫기"/>
            </Grid>
        </Border>
    </Grid>
</Window>
```

### 4.3 `UserDictionaryEditorWindow.xaml.cs` (code-behind)

파일: [`AltKey/Views/UserDictionaryEditorWindow.xaml.cs`](../../AltKey/Views/UserDictionaryEditorWindow.xaml.cs) (신규)

```csharp
using System.Windows;
using System.Windows.Input;
using AltKey.ViewModels;

namespace AltKey.Views;

public partial class UserDictionaryEditorWindow : Window
{
    private readonly UserDictionaryEditorViewModel _vm;

    public UserDictionaryEditorWindow(UserDictionaryEditorViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.OnLoaded();  // 열릴 때 초기 로드
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); return; }
        base.OnKeyDown(e);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _vm.OnClosing();  // Flush 등 마무리
        base.OnClosed(e);
    }
}
```

- `Esc` 키로 닫기.
- 창 닫힐 때 `OnClosing()`에서 `Flush()` 호출해 디스크 영속화 보장.
- `OnLoaded()`는 초기 로드. 로드·저장을 생성자에 넣지 않고 라이프사이클 훅으로 분리하는 것은 테스트성 이유.

---

## 5. `UserDictionaryEditorViewModel` 구현

파일: [`AltKey/ViewModels/UserDictionaryEditorViewModel.cs`](../../AltKey/ViewModels/UserDictionaryEditorViewModel.cs) (신규)

### 5.1 전체 초안 (중요 부분 전부)

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMsgBox = System.Windows.MessageBox;
using WpfMsgBoxButton = System.Windows.MessageBoxButton;
using WpfMsgBoxImage = System.Windows.MessageBoxImage;
using WpfMsgBoxResult = System.Windows.MessageBoxResult;

namespace AltKey.ViewModels;

public partial class UserDictionaryEditorViewModel : ObservableObject
{
    private readonly KoreanDictionary _koDict;
    private readonly EnglishDictionary _enDict;

    // 현재 활성 store (탭 전환 시 갱신)
    private WordFrequencyStore _activeStore;

    [ObservableProperty]
    private ObservableCollection<WordEntryVm> words = [];

    [ObservableProperty]
    private string searchQuery = "";

    [ObservableProperty]
    private string newWord = "";

    [ObservableProperty]
    private string statusText = "";

    // 탭
    private bool _isKoreanTabActive = true;
    public bool IsKoreanTabActive
    {
        get => _isKoreanTabActive;
        set
        {
            if (SetProperty(ref _isKoreanTabActive, value) && value)
                SwitchTab(korean: true);
        }
    }
    public bool IsEnglishTabActive
    {
        get => !_isKoreanTabActive;
        set
        {
            if (value && _isKoreanTabActive)
            {
                _isKoreanTabActive = false;
                OnPropertyChanged(nameof(IsKoreanTabActive));
                OnPropertyChanged(nameof(IsEnglishTabActive));
                SwitchTab(korean: false);
            }
        }
    }

    // 필터된 표시 목록 (SearchQuery에 따라 동적 필터)
    public ICollectionView FilteredWords { get; }

    public UserDictionaryEditorViewModel(KoreanDictionary koDict, EnglishDictionary enDict)
    {
        _koDict = koDict;
        _enDict = enDict;
        _activeStore = _koDict.UserStore;  // 기본 한국어

        FilteredWords = CollectionViewSource.GetDefaultView(Words);
        FilteredWords.Filter = obj =>
        {
            if (obj is not WordEntryVm entry) return false;
            if (string.IsNullOrWhiteSpace(SearchQuery)) return true;
            return entry.Word.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
        };
    }

    public void OnLoaded()
    {
        ReloadWords();
    }

    public void OnClosing()
    {
        // 빈도 편집 등이 반영되도록 즉시 저장
        _koDict.Flush();
        _enDict.Flush();
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilteredWords.Refresh();
        UpdateStatus();
    }

    private void SwitchTab(bool korean)
    {
        _activeStore = korean ? _koDict.UserStore : _enDict.UserStore;
        ReloadWords();
    }

    private void ReloadWords()
    {
        Words.Clear();
        foreach (var (w, f) in _activeStore.GetAllWords())
        {
            var entry = new WordEntryVm(w, f);
            entry.FrequencyChanged += OnEntryFrequencyChanged;
            Words.Add(entry);
        }
        FilteredWords.Refresh();
        UpdateStatus();
    }

    private void OnEntryFrequencyChanged(WordEntryVm entry)
    {
        // 빈도 텍스트 편집 → store 반영
        _activeStore.SetFrequency(entry.Word, entry.Frequency);
        // 음수/0은 store가 제거함. UI도 동기화.
        if (entry.Frequency <= 0)
        {
            Words.Remove(entry);
            FilteredWords.Refresh();
            UpdateStatus();
        }
    }

    [RelayCommand]
    private void AddWord()
    {
        var w = NewWord.Trim();
        if (w.Length == 0) return;

        // 영어 탭에서는 소문자 정규화
        var normalized = _isKoreanTabActive ? w : w.ToLowerInvariant();

        _activeStore.SetFrequency(normalized, GetFrequencyOrDefault(normalized, 1));
        NewWord = "";
        ReloadWords();
    }

    private int GetFrequencyOrDefault(string word, int fallback)
    {
        // 이미 있으면 기존 값 +1, 없으면 fallback
        var existing = _activeStore.GetAllWords()
            .FirstOrDefault(p => p.Word == word);
        return existing.Word == null ? fallback : existing.Frequency + 1;
    }

    [RelayCommand]
    private void RemoveOne(WordEntryVm entry)
    {
        if (entry is null) return;
        _activeStore.RemoveWord(entry.Word);
        Words.Remove(entry);
        FilteredWords.Refresh();
        UpdateStatus();
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        var toRemove = Words.Where(w => w.IsSelected).ToList();
        if (toRemove.Count == 0) return;

        var result = WpfMsgBox.Show(
            $"선택한 {toRemove.Count}개의 단어를 삭제하시겠습니까?",
            "단어 삭제 확인",
            WpfMsgBoxButton.YesNo,
            WpfMsgBoxImage.Question);
        if (result != WpfMsgBoxResult.Yes) return;

        foreach (var entry in toRemove)
        {
            _activeStore.RemoveWord(entry.Word);
            Words.Remove(entry);
        }
        FilteredWords.Refresh();
        UpdateStatus();
    }

    [RelayCommand]
    private void ClearAll()
    {
        var label = _isKoreanTabActive ? "한국어" : "영어";
        var result = WpfMsgBox.Show(
            $"{label} 사용자 사전의 모든 단어({Words.Count}개)를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
            "전체 삭제 확인",
            WpfMsgBoxButton.YesNo,
            WpfMsgBoxImage.Warning);
        if (result != WpfMsgBoxResult.Yes) return;

        _activeStore.Clear();
        Words.Clear();
        FilteredWords.Refresh();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int total = Words.Count;
        int shown = FilteredWords.Cast<object>().Count();
        StatusText = string.IsNullOrWhiteSpace(SearchQuery)
            ? $"총 {total}개 단어"
            : $"총 {total}개 단어 중 {shown}개 일치";
    }
}

/// DataGrid 한 행 ViewModel
public partial class WordEntryVm : ObservableObject
{
    public string Word { get; }

    private int _frequency;
    public int Frequency
    {
        get => _frequency;
        set
        {
            if (SetProperty(ref _frequency, value))
                FrequencyChanged?.Invoke(this);
        }
    }

    [ObservableProperty]
    private bool isSelected;

    public event Action<WordEntryVm>? FrequencyChanged;

    public WordEntryVm(string word, int frequency)
    {
        Word = word;
        _frequency = frequency;
    }
}
```

### 5.2 설계 포인트 해설

#### (1) `ICollectionView`를 통한 필터링

- `CollectionViewSource.GetDefaultView(Words)`로 `Words` 컬렉션 위에 필터 레이어를 씌운다.
- `SearchQuery`가 바뀔 때 `FilteredWords.Refresh()`만 호출하면 UI가 자동 갱신된다.
- `DataGrid.ItemsSource="{Binding FilteredWords}"`로 바인딩.
- `WordEntryVm.Word`는 immutable(생성자에서만 설정), 따라서 필터 검사는 안전.

#### (2) 탭 전환

- `RadioButton`의 `IsChecked`를 `IsKoreanTabActive` / `IsEnglishTabActive`에 양방향 바인딩.
- 두 속성이 상호 배타(한 쪽이 true면 다른 쪽이 false)이도록 setter에서 상태 동기화.
- 탭 전환 시 `_activeStore`를 바꾸고 `ReloadWords()`.
- 탭 바뀔 때 검색어는 유지할 수도, 리셋할 수도 있다 — MVP는 유지(사용자가 교차 검색을 원할 수 있음). 필요 시 `SearchQuery = ""`를 탭 전환 시 추가.

#### (3) 빈도 편집

- `DataGrid`의 빈도 컬럼을 `DataGridTemplateColumn` + `TextBox`로 두어 인라인 편집.
- `UpdateSourceTrigger=LostFocus`로 포커스 이탈 시 반영.
- `WordEntryVm.Frequency` setter에서 `FrequencyChanged` 이벤트 발행 → VM이 store에 `SetFrequency` 호출.
- 값이 0 또는 음수면 store가 제거 처리 → VM은 UI에서도 Remove.
- 파싱 실패(비숫자 입력) 대응: WPF의 기본 int 바인딩은 파싱 실패 시 변경을 적용하지 않는다 — 별도 `ValidationRule` 없이도 안전.

#### (4) "+ 추가"

- 텍스트박스의 `NewWord`를 `ObservableProperty`로 바인딩.
- 영어 탭이면 `ToLowerInvariant()` 정규화 (저장소 정합성).
- 이미 존재하면 빈도를 +1, 없으면 1로 설정.
- 추가 후 `NewWord = ""`로 상자 비움, `ReloadWords()`로 정렬 순서 반영(빈도 높아져 위로 올라갈 수 있음).

#### (5) 안전 조치

- **대량 삭제 확인**: `RemoveSelected`와 `ClearAll`은 `MessageBox`로 Yes/No 확인. 개별 `RemoveOne`은 즉시 삭제(단일 항목이라 실수 여파 작음).
- 저장은 `WordFrequencyStore`가 디바운스 처리. 편집기 닫힐 때 `Flush()`로 디스크 반영 보장.
- 삭제 후 store 상태와 UI `Words` 컬렉션을 **항상 동기화** (Remove 후 `FilteredWords.Refresh()`).

#### (6) 스레드

- 모든 조작이 UI 스레드(Dispatcher)에서 일어나므로 명시적 Invoke 불필요.
- `WordFrequencyStore`는 내부적으로 락 보호되어 있어 스레드 안전.
- 다만 store의 디바운스 타이머가 background 스레드에서 `Save()`를 호출할 때 편집기가 동시에 `GetAllWords`를 호출하면? — store의 `_saveLock`이 둘을 직렬화.

---

## 6. DI 등록

파일: [`AltKey/App.xaml.cs`](../../AltKey/App.xaml.cs)

[line 80-82 근처](../../AltKey/App.xaml.cs) — 기존 ViewModel 등록 블록에 추가:

```csharp
// T-9.4: 레이아웃 편집기 ViewModel
services.AddSingleton<LayoutEditorViewModel>();
// ac-editor 03: 사용자 사전 편집기 ViewModel
services.AddSingleton<UserDictionaryEditorViewModel>();
```

`UserDictionaryEditorWindow`는 **싱글톤 Window로 등록하지 않는다**. 매 열 때 새 인스턴스를 만든다 (WPF 창은 Close되면 재사용 불가). 따라서 `OpenUserDictionaryEditorCommand`에서 `new UserDictionaryEditorWindow(vm)`으로 직접 생성.

---

## 7. 설정 창 버튼 추가

파일: [`AltKey/Views/SettingsView.xaml`](../../AltKey/Views/SettingsView.xaml)

[line 103-108](../../AltKey/Views/SettingsView.xaml) — "레이아웃 편집기 열기" 버튼 바로 아래에 추가:

```xaml
<!-- T-9.4: 레이아웃 편집기 -->
<Button Content="레이아웃 편집기 열기"
        Command="{Binding OpenLayoutEditorCommand}"
        Height="30" Margin="0,0,0,8"
        Background="{DynamicResource SettingsHighlight}" Foreground="{DynamicResource SettingsFg}"
        BorderThickness="0" Cursor="Hand"/>

<!-- ac-editor 03: 사용자 단어 편집기 -->
<Button Content="사용자 단어 편집기 열기"
        Command="{Binding OpenUserDictionaryEditorCommand}"
        Height="30" Margin="0,0,0,16"
        Background="{DynamicResource SettingsHighlight}" Foreground="{DynamicResource SettingsFg}"
        BorderThickness="0" Cursor="Hand"
        ToolTip="자동완성에서 학습된 단어 목록을 확인·편집합니다"/>
```

**주의**: 기존 "레이아웃 편집기 열기" 버튼의 `Margin="0,0,0,16"`을 `"0,0,0,8"`로 줄여 두 버튼이 가까이 붙도록 한다(위 스니펫처럼).

파일: [`AltKey/ViewModels/SettingsViewModel.cs`](../../AltKey/ViewModels/SettingsViewModel.cs)

[line 28](../../AltKey/ViewModels/SettingsViewModel.cs) 근처 필드 영역에 추가:

```csharp
private readonly UserDictionaryEditorViewModel _userDictEditorVm;
```

생성자 파라미터 추가 ([line 95-105](../../AltKey/ViewModels/SettingsViewModel.cs)):

```csharp
public SettingsViewModel(
    ConfigService        configService,
    ThemeService         themeService,
    LayoutService        layoutService,
    HotkeyService        hotkeyService,
    StartupService       startupService,
    SoundService         soundService,
    LayoutEditorViewModel layoutEditorViewModel,
    UpdateService        updateService,
    DownloadService      downloadService,
    InstallerService     installerService,
    UserDictionaryEditorViewModel userDictionaryEditorViewModel)  // 추가
{
    // ... 기존 필드 할당들 ...
    _userDictEditorVm = userDictionaryEditorViewModel;
    // ... 나머지 ...
}
```

`OpenLayoutEditorCommand` 바로 아래([line 326-334](../../AltKey/ViewModels/SettingsViewModel.cs))에 새 커맨드 추가:

```csharp
// ── T-9.4: 레이아웃 편집기 열기 ──────────────────────────────────────────
[RelayCommand]
private void OpenLayoutEditor()
{
    var win = new AltKey.Views.LayoutEditorWindow(_layoutEditorVm)
    {
        Owner = WpfApp.Current.MainWindow
    };
    win.Show();
}

// ── ac-editor 03: 사용자 단어 편집기 열기 ─────────────────────────────
[RelayCommand]
private void OpenUserDictionaryEditor()
{
    var win = new AltKey.Views.UserDictionaryEditorWindow(_userDictEditorVm)
    {
        Owner = WpfApp.Current.MainWindow
    };
    win.Show();
}
```

`Show()` vs `ShowDialog()` 중 모달 필요 없으면 `Show()` (레이아웃 편집기와 일관). 사용자가 편집기 열어놓고 키보드 입력을 테스트할 수 있어 편함.

---

## 8. 테스트 전략

### 8.1 단위 테스트 — `WordFrequencyStoreTests`

§2.2의 신규 테스트 5개 추가 후 전부 녹색 확인.

### 8.2 단위 테스트 — `UserDictionaryEditorViewModel`

파일: `AltKey.Tests/ViewModels/UserDictionaryEditorViewModelTests.cs` (신규)

```csharp
using System.IO;
using AltKey.Services;
using AltKey.ViewModels;
using Xunit;

public class UserDictionaryEditorViewModelTests
{
    private static (UserDictionaryEditorViewModel vm, string tmpDir) CreateVm()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "altkey-edit-" + Guid.NewGuid());
        Directory.CreateDirectory(tmp);
        var factory = (string lang) => new WordFrequencyStore(tmp, lang);
        var ko = new KoreanDictionary(factory);
        var en = new EnglishDictionary(factory);
        return (new UserDictionaryEditorViewModel(ko, en), tmp);
    }

    [Fact]
    public void OnLoaded_LoadsAllWordsFromActiveStore()
    {
        var (vm, tmp) = CreateVm();
        try
        {
            // 사전에 단어 미리 심기 (ko)
            var store = new WordFrequencyStore(tmp, "ko");
            store.SetFrequency("해달", 5);
            store.SetFrequency("바나나", 3);
            store.Flush();

            // 새 VM 생성 (위 store가 Flush 후)
            var (vm2, _) = CreateVm();  // 같은 tmp 아니므로 별도 테스트 구조 필요
            // ...
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void AddWord_AddsToActiveStoreAndReloads() { ... }

    [Fact]
    public void RemoveOne_RemovesFromStoreAndUI() { ... }

    [Fact]
    public void SearchQuery_FiltersDisplayedWords()
    {
        var (vm, tmp) = CreateVm();
        try
        {
            vm.OnLoaded();
            // 단어 3개 추가
            vm.NewWord = "해달"; vm.AddWordCommand.Execute(null);
            vm.NewWord = "해안"; vm.AddWordCommand.Execute(null);
            vm.NewWord = "바다"; vm.AddWordCommand.Execute(null);

            vm.SearchQuery = "해";
            // FilteredWords가 2개(해달, 해안)여야 함
            Assert.Equal(2, vm.FilteredWords.Cast<object>().Count());
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void TabSwitch_SwapsActiveStore()
    {
        var (vm, tmp) = CreateVm();
        try
        {
            vm.OnLoaded();
            vm.NewWord = "해달"; vm.AddWordCommand.Execute(null);

            vm.IsEnglishTabActive = true;
            Assert.Equal(0, vm.Words.Count);  // 영어 사전 비어있음

            vm.NewWord = "banana"; vm.AddWordCommand.Execute(null);
            Assert.Equal(1, vm.Words.Count);

            vm.IsKoreanTabActive = true;
            Assert.Equal(1, vm.Words.Count);  // 한국어 사전으로 복귀
            Assert.Equal("해달", vm.Words[0].Word);
        }
        finally { Directory.Delete(tmp, true); }
    }
}
```

> 테스트 명세는 방향성을 보여주는 것이며, 실제 구현에서 `WordEntryVm` 구조나 컬렉션 인터페이스 맞춰 조정. 최소 **탭 스위치, 검색 필터, 추가, 제거** 네 시나리오는 커버.

### 8.3 수동 검증 (포터블 빌드)

1. 앱 실행 후 설정 창 열기.
2. "사용자 단어 편집기 열기" 버튼이 보이는지 확인. 클릭 → 편집기 창이 CenterOwner로 뜸.
3. 한국어 탭이 기본 선택되어 있음 확인.
4. 리스트가 현재 `user-words.ko.json`의 내용과 일치하는지 스포트 체크.
5. 검색 상자에 "해"를 입력 → 해당 prefix 포함 단어만 보임.
6. "+ 추가" 상자에 "테스트단어" 입력 → 버튼 클릭 → 리스트 최상단 근처에 등장(빈도 1이면 아래쪽).
7. 한 행의 빈도 셀 클릭 → 10으로 바꾸고 Tab/포커스 이탈 → 리스트가 재정렬되거나(ReloadWords가 재호출되지 않으면 그대로), 저장은 반영됨.
8. 체크박스로 2개 선택 → "선택 항목 제거" → 확인 다이얼로그 → Yes → 리스트에서 사라짐.
9. "전체 삭제" → 확인 → 목록 비워짐.
10. 영어 탭 클릭 → 영어 단어만 보임. 추가·제거 동일 동작.
11. 창 닫고 재오픈 → 변경 사항 영속화 확인.
12. `Esc` 키로 창 닫힘 확인.
13. 메모장에서 단어 입력 후 같은 prefix로 제안을 받아, 편집기에서 빈도를 올린 단어가 우선순위 상승했는지 확인.

### 8.4 접근성 검증

- [ ] `Tab` 키로 버튼 간 포커스 이동, `Space`/`Enter`로 활성화.
- [ ] NVDA가 탭 전환 시 "한국어 라디오 버튼, 선택됨" 류 안내를 읽음.
- [ ] DataGrid 내 화살표 키 내비게이션.
- [ ] 빈도 셀 편집: `F2` 또는 `Enter`로 편집 모드 진입, `Tab`으로 이동.
- [ ] 모든 버튼에 `ToolTip` 또는 `AutomationProperties.Name` 부여.
- [ ] 다크 배경 대비 충분(AA 이상). 사용 색상은 기존 `LayoutEditorWindow`와 동일.

---

## 9. 수락 기준

- [ ] `dotnet build AltKey.csproj` 성공.
- [ ] `dotnet test AltKey.Tests.csproj` 전부 녹색. 신규 테스트 포함(§2.2, §8.2).
- [ ] 수동 §8.3 1~13 모두 통과.
- [ ] 접근성 §8.4 모두 체크.
- [ ] `App.xaml.cs`에 `UserDictionaryEditorViewModel` 싱글톤 등록.
- [ ] `SettingsView.xaml`에 새 버튼 추가, `SettingsViewModel`에 `OpenUserDictionaryEditorCommand` 구현.
- [ ] 편집기 창이 `Owner`를 가지며 메인 창 위에 중앙 표시됨.
- [ ] `Esc` 및 "닫기" 버튼으로 정상 닫힘. 닫힌 뒤 Flush가 호출됨(§5 `OnClosing`).

## 10. 회귀 금지

- [ ] 편집기를 열어도 자동완성·한글 조합이 정상 동작한다(편집기는 독립 창).
- [ ] 편집기에서 단어를 삭제해도 `KoreanDictionary`의 **내장 사전(builtin)은 손상되지 않는다**. 내장 단어는 여전히 제안에 나타나야 함.
- [ ] `AutoCompleteEnabled` 토글 OFF 상태에서도 편집기는 열 수 있어야 한다 (단어를 보고 정리하는 행위는 토글과 무관).
- [ ] 레이아웃 편집기와 동시에 열어도 둘 다 정상 동작.
- [ ] [`CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2 항목 전부 유지.

---

## 11. 하면 안 되는 것

- `HangulComposer.cs`, `KoreanInputModule.cs`, `InputService.cs`, `AutoCompleteService.cs` 수정 — 이 작업 범위 밖.
- 내장 사전(`ko-words.txt`, `en-words.txt`) 수정 — 리소스 건드리지 말 것.
- `ObservableProperty`를 부적절하게 `WordEntryVm.Word`에 붙이기 — `Word`는 키이므로 immutable.
- 편집기 창을 모달(`ShowDialog`)로 만들기 — 비모달(`Show`)이 `LayoutEditorWindow`와 일관.
- 단어 이름 자체를 편집 가능하게 하기 — 이름 변경은 "제거 + 새 이름 추가"로 처리하도록 사용자 유도(범위 외).
- 편집기 창 자체를 `SettingsView.xaml`에 임베드하기 — 공간 협소로 UX 나쁨.
- CSV/JSON 내보내기·가져오기 — 범위 외(후속 작업 여지).
- 정렬 순서 토글(빈도·가나다) — MVP는 고정 정렬(빈도 내림차순 + 단어 오름차순). 필요 시 후속.

---

## 12. FAQ

### Q1. `UserStore` getter가 캡슐화를 깨지 않나?

`WordFrequencyStore`는 자체적으로 락과 디바운스를 가진 자기완결적 서비스다. 외부에서 `GetAllWords`·`SetFrequency`·`RemoveWord`·`Clear`·`Flush`만 호출 가능하고, 내부 `_freq` 딕셔너리에 직접 접근할 수 없다. 따라서 getter 노출은 사실상 "서비스 위임"과 동등.

### Q2. 한국어/영어 외 다른 언어 추가 시?

현재 `IInputLanguageModule`은 한국어만 있고 `WordFrequencyStore`는 언어 코드별로 파일 분리를 지원한다. 향후 다른 언어가 추가되면 편집기에 탭을 더하기만 하면 된다. 이 작업에서는 MVP로 2탭만.

### Q3. 대량 단어(1000개 이상)에서 `DataGrid` 성능은?

WPF `DataGrid`는 가상화(Virtualization)가 기본 켜짐. 5000개(MaxWords) 정도까지는 부드럽게 동작. 만약 느리면 `VirtualizingPanel.IsVirtualizing="True"`, `ScrollViewer.CanContentScroll="True"` 명시적 지정.

### Q4. 검색이 느리지 않나?

`SearchQuery` 바인딩에 `Delay=150`ms 지연을 두어 타이핑 중 매번 필터링하지 않음. 5000단어 Contains는 100ms 미만.

### Q5. 빈도 필드에 문자 입력하면?

WPF 기본 int 바인딩이 파싱 실패 시 값을 갱신하지 않음. UI에는 빨간 테두리 오류 표시가 뜰 수 있지만, 다음 숫자 입력 시 복구. 필요 시 `DataGridTemplateColumn` 안에 `PreviewTextInput` 핸들러로 숫자만 허용하도록 필터 가능.

### Q6. 편집기 열린 채 다른 곳에서 `RecordWord`가 호출되면?

가능한 시나리오: 편집기는 열려 있고, 메인 키보드로 단어 입력 → `FinalizeComposition` → `RecordWord` → store 갱신. 이 경우:

- store 내부 `_freq`가 바뀌지만 편집기 VM의 `Words` 컬렉션은 즉시 반영되지 않는다(스냅샷).
- 편집기 닫고 다시 열면 최신 상태가 보임.
- MVP로는 이 정도 지연 수용. 향후 `WordFrequencyStore`에 `WordAdded`/`WordChanged` 이벤트를 추가하고 편집기 VM이 구독해 실시간 반영하는 확장 가능. **이번 작업에서는 하지 않는다**.

### Q7. 빈도를 0으로 입력하면 어떻게 되나?

`WordEntryVm.Frequency` setter → `FrequencyChanged` → `SetFrequency(word, 0)` → store가 제거 → VM이 `Words.Remove`. 사용자 기대에 부합.

---

## 13. 작업 완료 후 보고

- 변경·추가한 파일 목록 (경로 기준).
- 신규 ViewModel·View 라인 수 대략.
- 추가한 테스트 개수·이름.
- `dotnet test` 결과 (통과/실패).
- 수동 §8.3 1~13 각 단계 결과.
- 접근성 §8.4 모두 체크 여부.
- 편집기 창 스크린샷(선택) — 한국어 탭, 영어 탭, 검색 필터 적용 상태.

---

## 14. 이 폴더의 다른 문서

- [00-overview.md](00-overview.md) — 전체 작업 개요.
- [01-conditional-learning.md](01-conditional-learning.md) — 자동완성 토글 OFF 시 학습 스킵.
- [02-context-menu-remove.md](02-context-menu-remove.md) — 제안 바 우클릭 제거.

작업 03은 01·02 완료 후 진행하는 것이 이상적이지만, 02에서 추가한 `RemoveWord`만 있어도 이 문서의 §2 나머지 API(`SetFrequency`, `GetAllWords`, `Clear`)를 자체 추가해 독립 진행 가능.

---

## 15. 마지막 체크리스트 (커밋 전)

- [ ] [`CORE-LOGIC-PROTECTION.md`](../auto-complet/CORE-LOGIC-PROTECTION.md) §2 전부 유지.
- [ ] `WordFrequencyStore`의 `_jsonOptions`, `UnsafeRelaxedJsonEscaping`, `tmp + File.Move` 원자성 유지.
- [ ] 새로 추가한 store 메서드들이 `_saveLock`을 올바르게 잡고 `ScheduleSave`는 락 밖에서 호출.
- [ ] `SettingsViewModel` 생성자 변경에 따라 DI 자동 주입이 깨지지 않음 (빌드 성공 = 검증됨).
- [ ] 편집기 창 생성자가 `UserDictionaryEditorViewModel` 싱글톤을 **매번 주입받음**. VM은 싱글톤이지만 Window는 매번 신규. VM 내부 상태(현재 탭, 검색어)는 창 재오픈 시 초기화를 원하면 `OnLoaded`에서 리셋할 것. 혹은 명시적으로 `SearchQuery = ""; IsKoreanTabActive = true;`를 `OnLoaded` 처음에 두는 것 권장.
- [ ] 테마(라이트/다크)에 관계없이 편집기 창 색상은 하드코드 다크로 고정 — `LayoutEditorWindow`와 일관. 향후 라이트 모드 지원은 별도 작업.
