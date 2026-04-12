# Phase 3: 레이아웃 시스템

> 목표: JSON 파일로 정의된 키보드 레이아웃을 파싱하고, WPF ItemsControl로 동적 렌더링하며, 레이아웃 전환이 가능한 상태를 만든다.

**의존성**: Phase 0, Phase 2 (KeyAction 모델 정의)

---

## T-3.1: 레이아웃 데이터 모델 정의

**설명**: JSON 레이아웃 파일 구조에 대응하는 C# 레코드/클래스를 정의한다.

**파일**: `Models/LayoutConfig.cs`, `Models/KeySlot.cs`

**구현 내용**:
```csharp
// Models/LayoutConfig.cs
public record LayoutConfig(
    string Name,
    string? Language,
    List<KeyRow> Rows
);

public record KeyRow(List<KeySlot> Keys);

// Models/KeySlot.cs
public record KeySlot(
    string Label,
    string? ShiftLabel,
    KeyAction Action,
    double Width = 1.0,
    double Height = 1.0,
    string CssClass = ""   // WPF에서는 StyleKey로 활용
);
```

**System.Text.Json 직렬화 옵션**:
```csharp
// 공통 JsonSerializerOptions (앱 전체 재사용)
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
```

**검증**: 예제 JSON `{"name":"test","rows":[{"keys":[{"label":"A","action":{"type":"SendKey","Vk":"VK_A"},"width":1.0}]}]}` 역직렬화 성공.

---

## T-3.2: 기본 QWERTY 한국어 레이아웃 JSON 작성

**설명**: 한국어 표준 QWERTY 레이아웃을 JSON 파일로 작성한다.

**파일**: `layouts/qwerty-ko.json`

**5개 행 구성**:
1. **숫자 행**: `` ` 1 2 3 4 5 6 7 8 9 0 - = `` + Backspace(width:2)
2. **QWERTY 행**: Tab(1.5) + Q W E R T Y U I O P `[` `]` `\`(1.5)
3. **ASDF 행**: CapsLock(1.75) + A S D F G H J K L ; ' + Enter(2.25)
4. **ZXCV 행**: Shift(2.25) + Z X C V B N M `,` `.` `/` + Shift(2.75)
5. **하단 행**: Ctrl(1.25) Win(1.25) Alt(1.25) 한/영(1.5) Space(6) 한자(1.5) Alt(1.25) Ctrl(1.25) ← ↑ ↓ →

각 키 형식:
```json
{ "label": "Q", "shift_label": null, "action": { "type": "SendKey", "Vk": "VK_Q" }, "width": 1.0 }
```

수식자 키 형식:
```json
{ "label": "Shift", "action": { "type": "ToggleSticky", "Vk": "VK_SHIFT" }, "width": 2.25 }
```

**검증**: `LayoutService.Load("qwerty-ko")` 성공, 5행 모두 파싱됨.

---

## T-3.3: 기본 QWERTY 영문 레이아웃 JSON 작성

**설명**: 영문 표준 QWERTY 레이아웃 JSON을 작성한다. 구조는 T-3.2와 동일하고 한/영, 한자 키 대신 Alt 키를 배치한다.

**파일**: `layouts/qwerty-en.json`

**검증**: JSON 문법 유효성, `LayoutService.Load("qwerty-en")` 파싱 성공.

---

## T-3.4: LayoutService 구현

**설명**: `layouts/` 폴더에서 JSON 파일을 읽어 `LayoutConfig`를 반환하는 서비스를 작성한다.

**파일**: `Services/LayoutService.cs`

**구현 내용**:
```csharp
public class LayoutService
{
    private readonly string _layoutsDir;
    private readonly Dictionary<string, LayoutConfig> _cache = [];

    public LayoutService(ConfigService configService)
    {
        _layoutsDir = PathResolver.LayoutsDir; // exe 옆 layouts/ 폴더
    }

    public IReadOnlyList<string> GetAvailableLayouts()
    {
        return Directory.GetFiles(_layoutsDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();
    }

    public LayoutConfig Load(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;

        var path = Path.Combine(_layoutsDir, $"{name}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"레이아웃 파일 없음: {path}");

        var json = File.ReadAllText(path);
        var layout = JsonSerializer.Deserialize<LayoutConfig>(json, JsonOptions.Default)
            ?? throw new InvalidDataException($"레이아웃 파싱 실패: {name}");

        _cache[name] = layout;
        return layout;
    }

    /// config 변경 시 캐시 무효화
    public void InvalidateCache() => _cache.Clear();
}
```

**검증**: `GetAvailableLayouts()` → `["qwerty-ko", "qwerty-en"]` 반환.

---

## T-3.5: 포터블 경로 해결 (PathResolver)

**설명**: 포터블 모드와 설치 모드 모두에서 layouts/ 폴더를 올바르게 찾는 유틸을 작성한다.

**파일**: `Services/PathResolver.cs` (신규)

**구현 내용**:
```csharp
public static class PathResolver
{
    private static readonly string _exeDir =
        Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";

    /// exe 옆에 config.json이 있으면 포터블 모드
    public static bool IsPortable =>
        File.Exists(Path.Combine(_exeDir, "config.json"));

    public static string DataDir => IsPortable
        ? _exeDir
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AltKey");

    public static string LayoutsDir  => Path.Combine(DataDir, "layouts");
    public static string ConfigPath  => Path.Combine(DataDir, "config.json");
}
```

**검증**: exe 옆에 `config.json` 배치 시 `IsPortable == true`, DataDir이 exe 폴더를 가리킴.

---

## T-3.6: KeyRowViewModel / KeySlotViewModel 정의

**설명**: `LayoutConfig`를 WPF 바인딩용 ViewModel로 변환한다.

**파일**: `ViewModels/KeyboardViewModel.cs`

**구현 내용**:
```csharp
// LayoutConfig → ViewModel 변환 (mapper)
public partial class KeyboardViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<KeyRowVm> rows = [];

    public void LoadLayout(LayoutConfig layout)
    {
        Rows = new ObservableCollection<KeyRowVm>(
            layout.Rows.Select(r => new KeyRowVm(
                r.Keys.Select(k => new KeySlotVm(k)).ToList()
            ))
        );
    }
}

public record KeyRowVm(IReadOnlyList<KeySlotVm> Keys);

public class KeySlotVm(KeySlot slot) : ObservableObject
{
    public KeySlot Slot { get; } = slot;
    public double Width { get; } = slot.Width;

    // ShowUpperCase 바인딩으로 라벨 동적 변경
    public string GetLabel(bool upperCase) =>
        upperCase && slot.ShiftLabel is { } s ? s : slot.Label;
}
```

**검증**: `LoadLayout(layout)` 호출 후 `Rows.Count == layout.Rows.Count`.

---

## T-3.7: KeyboardView XAML — ItemsControl로 행/키 렌더링

**설명**: `KeyboardViewModel.Rows`를 WPF `ItemsControl`로 렌더링한다.

**파일**: `Views/KeyboardView.xaml`

**구현 내용**:
```xml
<ItemsControl ItemsSource="{Binding Rows}">
    <ItemsControl.ItemTemplate>
        <DataTemplate DataType="{x:Type vm:KeyRowVm}">
            <!-- 행 -->
            <ItemsControl ItemsSource="{Binding Keys}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel Orientation="Horizontal"/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate DataType="{x:Type vm:KeySlotVm}">
                        <controls:KeyButton
                            Slot="{Binding}"
                            ShowUpperCase="{Binding DataContext.ShowUpperCase,
                                RelativeSource={RelativeSource AncestorType=UserControl}}"
                            Command="{Binding DataContext.KeyPressedCommand,
                                RelativeSource={RelativeSource AncestorType=UserControl}}"
                            CommandParameter="{Binding Slot}"/>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

**검증**: 앱 실행 시 QWERTY 레이아웃의 모든 키가 행별로 렌더링됨.

---

## T-3.8: KeyButton 커스텀 컨트롤

**설명**: 개별 키를 표현하는 커스텀 WPF 컨트롤을 작성한다.

**파일**: `Controls/KeyButton.xaml` + `Controls/KeyButton.cs`

**구현 내용**:
```csharp
public class KeyButton : Button
{
    public static readonly DependencyProperty SlotProperty =
        DependencyProperty.Register(nameof(Slot), typeof(KeySlotVm), typeof(KeyButton));

    public static readonly DependencyProperty ShowUpperCaseProperty =
        DependencyProperty.Register(nameof(ShowUpperCase), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false, OnShowUpperCaseChanged));

    public KeySlotVm? Slot { get; set; }
    public bool ShowUpperCase { get; set; }

    private static void OnShowUpperCaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyButton kb && kb.Slot is { } slot)
            kb.Content = slot.GetLabel((bool)e.NewValue);
    }

    // Width 계산: slot.Width * 기준 단위
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == SlotProperty && Slot is { } s)
        {
            Width = s.Width * 48.0; // 기준: 48dp
            Height = s.Slot.Height * 48.0;
            Content = s.GetLabel(ShowUpperCase);
        }
    }
}
```

`KeyButton.xaml`:
- `ControlTemplate`에서 Border + ContentPresenter 조합
- `:hover`, `:pressed` 상태 트리거 (WPF Trigger)

**검증**: 각 키가 `slot.width` 비율에 맞는 너비로 렌더링됨.

---

## T-3.9: 레이아웃 초기 로드 (앱 시작 시)

**설명**: 앱 시작 시 `config.json`의 `default_layout` 값을 읽어 레이아웃을 로드한다.

**파일**: `ViewModels/MainViewModel.cs`

**구현 내용**:
```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string currentLayoutName = "";

    public async Task InitializeAsync()
    {
        var config = _configService.Current;
        var layout = _layoutService.Load(config.DefaultLayout);
        _keyboardViewModel.LoadLayout(layout);
        CurrentLayoutName = layout.Name;
    }
}
```

- `MainWindow.Loaded` 이벤트에서 `await viewModel.InitializeAsync()` 호출

**검증**: 앱 실행 시 `config.json`의 `default_layout`에 해당하는 레이아웃이 자동 표시됨.

---

## T-3.10: 레이아웃 전환 UI

**설명**: 드래그 핸들 영역에 현재 레이아웃 이름과 전환 버튼을 배치한다.

**파일**: `Views/KeyboardView.xaml`

**구현 내용**:
```xml
<StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
    <ComboBox ItemsSource="{Binding AvailableLayouts}"
              SelectedItem="{Binding CurrentLayoutName}"
              SelectionChanged="OnLayoutSelectionChanged"
              Width="140"/>
</StackPanel>
```

`SelectionChanged` 핸들러:
```csharp
private void OnLayoutSelectionChanged(object s, SelectionChangedEventArgs e)
{
    if (e.AddedItems.Count > 0 && e.AddedItems[0] is string name)
        _mainViewModel.SwitchLayout(name);
}
```

**검증**: 드롭다운에서 "qwerty-en" 선택 시 영문 레이아웃으로 전환됨.

---

## T-3.11: 레이아웃 전환 단위 테스트

**설명**: `LayoutService`의 로드/캐싱/존재하지 않는 파일 처리에 대한 단위 테스트를 작성한다.

**파일**: `AltKey.Tests/LayoutServiceTests.cs`

**테스트 케이스**:
```csharp
[Fact]
public void Load_ExistingLayout_ReturnsLayoutConfig()

[Fact]
public void Load_SameName_ReturnsCachedInstance()

[Fact]
public void Load_NonExistentFile_ThrowsFileNotFoundException()

[Fact]
public void GetAvailableLayouts_ReturnsAllJsonFiles()

[Fact]
public void InvalidateCache_ForcesReload()
```

**검증**: `dotnet test` — 전체 통과.
