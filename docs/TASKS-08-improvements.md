# Phase 8: UX 개선

> **난이도**: ★★☆ 중간  
> **목표**: 사용성과 편의성을 높이는 신규 기능을 추가한다.  
> **의존성**: Phase 0~7 완료

---

## T-8.1: 윈도우 시작 시 자동 실행 옵션

**설명**: 설정 패널에 "Windows 시작 시 자동 실행" 토글을 추가한다.
포터블 방식이므로 레지스트리 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`을 사용한다.
삭제 시 해당 항목을 레지스트리에서 제거하면 된다.

**파일 (신규)**: `AltKey/Services/StartupService.cs`

```csharp
using Microsoft.Win32;

namespace AltKey.Services;

public class StartupService
{
    private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AltKey";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: false);
            return key?.GetValue(AppName) is string path
                && path.Equals(ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true)
            ?? throw new InvalidOperationException("레지스트리 키를 열 수 없습니다.");
        key.SetValue(AppName, $"\"{ExePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static string ExePath =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule!.FileName!;
}
```

**파일**: `AltKey/Models/AppConfig.cs`

```csharp
public bool RunOnStartup { get; set; } = false;
```

**파일**: `AltKey/ViewModels/SettingsViewModel.cs`

```csharp
[ObservableProperty]
private bool runOnStartup;

partial void OnRunOnStartupChanged(bool value)
{
    if (value) _startupService.Enable();
    else        _startupService.Disable();
    _configService.Update(c => c.RunOnStartup = value);
}
```

생성자에서 초기값 로드:
```csharp
RunOnStartup = _startupService.IsEnabled;
```

**파일**: `AltKey/Views/SettingsView.xaml`

설정 패널에 토글 추가:
```xml
<CheckBox Content="Windows 시작 시 자동 실행"
          IsChecked="{Binding RunOnStartup}"
          Margin="0,4"/>
```

**주의사항**:
- `single-file publish` 시 `Environment.ProcessPath`가 임시 디렉터리를 가리킬 수 있음.
  `AppContext.BaseDirectory`와 `Process.GetCurrentProcess().MainModule.FileName`을 비교해 올바른 경로를 선택한다.
- 자동 시작 등록 시 exe 경로를 따옴표로 감싸 공백 경로 문제를 방지한다.

**검증**:
1. 토글 ON → `regedit`에서 `HKCU\...\Run`에 `AltKey` 항목 확인
2. 재시작 후 자동 실행 확인
3. 토글 OFF → 레지스트리 항목 삭제 확인

---

## T-8.2: 키 클릭 사운드

**설명**: 키를 클릭할 때 소리가 재생되는 옵션을 추가한다.
기본 사운드는 앱에 내장하고, 사용자가 WAV 파일을 지정할 수 있게 한다.

### 1단계: SoundService 구현

**파일 (신규)**: `AltKey/Services/SoundService.cs`

```csharp
using System.Media;

namespace AltKey.Services;

public class SoundService : IDisposable
{
    private SoundPlayer? _player;
    private bool _enabled;
    private string? _soundPath;

    public void Configure(bool enabled, string? customPath)
    {
        _enabled = enabled;
        _soundPath = customPath;
        _player?.Dispose();
        _player = null;

        if (!enabled) return;

        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
        {
            _player = new SoundPlayer(customPath);
        }
        else
        {
            // 내장 기본 사운드 (EmbeddedResource)
            var stream = GetType().Assembly
                .GetManifestResourceStream("AltKey.Assets.Sounds.click.wav");
            if (stream is not null)
                _player = new SoundPlayer(stream);
        }

        _player?.Load();
    }

    public void Play()
    {
        if (_enabled)
            _player?.Play();
    }

    public void Dispose() => _player?.Dispose();
}
```

### 2단계: 기본 사운드 파일 준비

1. `AltKey/Assets/Sounds/` 폴더를 만들고 `click.wav` 파일을 추가한다.
   - 짧은 클릭 소리 WAV 파일 (10~50ms, 16-bit 44.1kHz 권장)
   - 저작권 없는 소리 파일 사용 (예: [freesound.org](https://freesound.org) CC0 라이선스)

2. `AltKey.csproj`에 EmbeddedResource로 추가:
```xml
<ItemGroup>
  <EmbeddedResource Include="Assets\Sounds\click.wav"/>
</ItemGroup>
```

### 3단계: AppConfig 및 Settings 연동

**파일**: `AltKey/Models/AppConfig.cs`

```csharp
public bool SoundEnabled { get; set; } = false;
public string? SoundFilePath { get; set; } = null;  // null = 기본 내장 사운드
```

**파일**: `AltKey/ViewModels/SettingsViewModel.cs`

```csharp
[ObservableProperty] private bool soundEnabled;
[ObservableProperty] private string soundFilePath = "";

partial void OnSoundEnabledChanged(bool value)
{
    _soundService.Configure(value, SoundFilePath);
    _configService.Update(c => c.SoundEnabled = value);
}

[RelayCommand]
private void BrowseSoundFile()
{
    var dlg = new OpenFileDialog { Filter = "WAV 파일|*.wav|모든 파일|*.*" };
    if (dlg.ShowDialog() == true)
    {
        SoundFilePath = dlg.FileName;
        _soundService.Configure(SoundEnabled, SoundFilePath);
        _configService.Update(c => c.SoundFilePath = SoundFilePath);
    }
}
```

### 4단계: KeyboardViewModel에서 소리 재생

**파일**: `AltKey/ViewModels/KeyboardViewModel.cs`

```csharp
[RelayCommand]
private void KeyPressed(KeySlot slot)
{
    _soundService.Play();  // ← 추가 (InputService 호출 전)
    if (slot.Action is not null)
        _inputService.HandleAction(slot.Action);
    UpdateModifierState();
}
```

### 5단계: 설정 UI

**파일**: `AltKey/Views/SettingsView.xaml`

```xml
<CheckBox Content="키 클릭 사운드" IsChecked="{Binding SoundEnabled}" Margin="0,4"/>
<StackPanel Orientation="Horizontal" Margin="16,2,0,4"
            Visibility="{Binding SoundEnabled, Converter={StaticResource BoolToVis}}">
    <TextBlock Text="사운드 파일:" VerticalAlignment="Center" Margin="0,0,6,0"/>
    <TextBlock Text="{Binding SoundFilePath, FallbackValue='기본 내장 사운드'}"
               VerticalAlignment="Center" Opacity="0.7" MaxWidth="140"
               TextTrimming="CharacterEllipsis"/>
    <Button Content="찾아보기" Command="{Binding BrowseSoundFileCommand}" Margin="6,0,0,0" Padding="6,2"/>
    <Button Content="초기화" Margin="4,0,0,0" Padding="6,2"
            Command="{Binding ResetSoundFileCommand}"/>
</StackPanel>
```

**검증**:
1. 사운드 ON → 키 클릭 시 소리 재생
2. WAV 파일 지정 → 해당 소리로 변경
3. 초기화 → 기본 내장 사운드로 복귀

---

## T-8.3: 이모지 빠른 입력 패널

**설명**: 키보드 하단에 토글 가능한 이모지 패널을 추가한다.
이모지 클릭 시 포커스된 앱에 직접 유니코드 문자를 전송한다 (Windows 이모지 창을 열지 않음).

### 1단계: 유니코드 SendInput 지원

**파일**: `AltKey/Services/InputService.cs`

이모지(유니코드) 입력을 위한 메서드를 추가한다:
```csharp
public void SendUnicode(string text)
{
    // 서로게이트 쌍 처리 포함
    var inputs = new List<Win32.INPUT>();
    foreach (var ch in text)
    {
        inputs.Add(MakeUnicodeKeyDown(ch));
        inputs.Add(MakeUnicodeKeyUp(ch));
    }
    Win32.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Win32.INPUT>());
    ReleaseTransientModifiers();
}

private static Win32.INPUT MakeUnicodeKeyDown(char ch) => new()
{
    Type = Win32.INPUT_KEYBOARD,
    Data = new() { Keyboard = new()
    {
        Vk = 0,
        Scan = ch,
        Flags = Win32.KEYEVENTF_UNICODE
    }}
};

private static Win32.INPUT MakeUnicodeKeyUp(char ch) => new()
{
    Type = Win32.INPUT_KEYBOARD,
    Data = new() { Keyboard = new()
    {
        Vk = 0,
        Scan = ch,
        Flags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP
    }}
};
```

> **주의**: 이모지는 보통 서로게이트 쌍(2개의 char)이다. `string text`를 그대로 순회하면 문자 하나씩 전송되어 문제가 없다.
> 단, `KEYEVENTF_UNICODE`를 사용하면 한글 IME와 충돌할 수 있으므로, 이모지 전송 전 IME를 잠시 비활성화하는 방법을 고려한다.

### 2단계: 이모지 데이터 파일

**파일 (신규)**: `AltKey/Assets/emoji.json`

자주 쓰는 이모지를 카테고리별로 정의한다:
```json
{
  "categories": [
    {
      "name": "표정",
      "emoji": ["😀","😂","😊","😍","🤔","😢","😡","👍","👎","❤️"]
    },
    {
      "name": "자연",
      "emoji": ["🌸","🌙","⭐","☀️","🌈","🔥","💧","🌊"]
    },
    {
      "name": "음식",
      "emoji": ["🍎","🍕","☕","🍺","🍜","🎂","🍩"]
    },
    {
      "name": "기호",
      "emoji": ["✅","❌","⚠️","ℹ️","📌","🔑","💡","🎯"]
    }
  ]
}
```

총 이모지 수: 200개 이내로 제한 (용량 최소화).

### 3단계: EmojiViewModel

**파일 (신규)**: `AltKey/ViewModels/EmojiViewModel.cs`

```csharp
public partial class EmojiViewModel : ObservableObject
{
    private readonly InputService _inputService;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private IReadOnlyList<EmojiCategory> categories = [];

    public EmojiViewModel(InputService inputService)
    {
        _inputService = inputService;
        LoadEmoji();
    }

    private void LoadEmoji()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "emoji.json");
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        // System.Text.Json으로 역직렬화
        var data = JsonSerializer.Deserialize<EmojiData>(json);
        Categories = data?.Categories ?? [];
    }

    [RelayCommand]
    private void SendEmoji(string emoji)
    {
        _inputService.SendUnicode(emoji);
    }

    [RelayCommand]
    private void TogglePanel()
    {
        IsVisible = !IsVisible;
    }
}

public record EmojiCategory(string Name, IReadOnlyList<string> Emoji);
```

### 4단계: 이모지 패널 UI

**파일 (신규)**: `AltKey/Views/EmojiPanel.xaml`

```xml
<UserControl ...>
    <Border Background="#CC1A1A1A" CornerRadius="8" Padding="8">
        <StackPanel>
            <!-- 카테고리 탭 -->
            <ScrollViewer HorizontalScrollBarVisibility="Auto"
                          VerticalScrollBarVisibility="Disabled">
                <StackPanel Orientation="Horizontal">
                    <ItemsControl ItemsSource="{Binding Categories}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <StackPanel Orientation="Horizontal"/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Button Content="{Binding Name}"
                                        Command="{Binding DataContext.SelectCategoryCommand,
                                            RelativeSource={RelativeSource AncestorType=UserControl}}"
                                        CommandParameter="{Binding}"
                                        Padding="8,4" Margin="2,0"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </StackPanel>
            </ScrollViewer>

            <!-- 이모지 그리드 -->
            <ScrollViewer MaxHeight="100" VerticalScrollBarVisibility="Auto">
                <WrapPanel>
                    <ItemsControl ItemsSource="{Binding SelectedCategory.Emoji}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <WrapPanel/>
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Button Content="{Binding}"
                                        Command="{Binding DataContext.SendEmojiCommand,
                                            RelativeSource={RelativeSource AncestorType=UserControl}}"
                                        CommandParameter="{Binding}"
                                        Width="36" Height="36" FontSize="18"
                                        Background="Transparent" BorderThickness="0"
                                        Cursor="Hand"/>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </WrapPanel>
            </ScrollViewer>
        </StackPanel>
    </Border>
</UserControl>
```

### 5단계: MainWindow에 패널 통합

**파일**: `AltKey/Views/KeyboardView.xaml`

키보드 그리드 위에 이모지 패널을 토글되는 방식으로 추가:
```xml
<!-- 이모지 패널 (키보드 위쪽, 토글) -->
<views:EmojiPanel DataContext="{Binding Emoji}"
                  Visibility="{Binding Emoji.IsVisible, Converter={StaticResource BoolToVis}}"
                  VerticalAlignment="Bottom" Margin="0,0,0,298"/>
```

키보드 레이아웃 JSON에 이모지 패널 토글 버튼을 추가하거나,
상단 바에 이모지 버튼을 추가한다.

**검증**:
1. 이모지 버튼 클릭 → 패널 표시
2. 이모지 클릭 → 포커스된 앱에 이모지 입력됨
3. 이모지 패널 토글 → 키보드와 함께 표시/숨김

---

## T-8.4: 클립보드 히스토리 패널

**설명**: 키보드 측면 또는 위에 클립보드 히스토리 패널을 표시한다.
항목 클릭 시 해당 텍스트를 포커스된 앱에 붙여넣기한다.

### 1단계: ClipboardService 구현

**파일 (신규)**: `AltKey/Services/ClipboardService.cs`

```csharp
using System.Windows;
using System.Windows.Threading;

namespace AltKey.Services;

public class ClipboardService : IDisposable
{
    private const int MaxHistory = 20;

    public event Action? HistoryChanged;

    // 최신 항목이 앞에 오는 리스트
    public IReadOnlyList<string> History => _history;

    private readonly List<string> _history = [];
    private readonly DispatcherTimer _pollTimer;
    private string? _lastClipboard;

    public ClipboardService()
    {
        // 클립보드 폴링 (500ms 간격)
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText()) return;
            var current = Clipboard.GetText();
            if (current == _lastClipboard) return;
            _lastClipboard = current;
            AddToHistory(current);
        }
        catch { /* 클립보드 접근 실패 무시 */ }
    }

    private void AddToHistory(string text)
    {
        // 중복 제거 후 맨 앞에 삽입
        _history.Remove(text);
        _history.Insert(0, text);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(_history.Count - 1);
        HistoryChanged?.Invoke();
    }

    public void PasteItem(string text)
    {
        // 클립보드에 텍스트 설정 후 Ctrl+V 전송
        Clipboard.SetText(text);
        _lastClipboard = text; // 히스토리 중복 추가 방지
    }

    public void Dispose() => _pollTimer.Stop();
}
```

> **대안**: 폴링 대신 `AddClipboardFormatListener` Win32 API로 클립보드 변경 이벤트를 받는 방식이 더 효율적이다.
> 폴링은 구현이 간단하지만 배터리/CPU를 약간 소모한다. 이 태스크에서는 폴링 방식으로 구현한다.

### 2단계: ClipboardViewModel

**파일 (신규)**: `AltKey/ViewModels/ClipboardViewModel.cs`

```csharp
public partial class ClipboardViewModel : ObservableObject
{
    private readonly ClipboardService _clipboardService;
    private readonly InputService _inputService;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private ObservableCollection<ClipboardItem> items = [];

    public ClipboardViewModel(ClipboardService clipboardService, InputService inputService)
    {
        _clipboardService = clipboardService;
        _inputService = inputService;
        _clipboardService.HistoryChanged += RefreshItems;
    }

    private void RefreshItems()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Items = new ObservableCollection<ClipboardItem>(
                _clipboardService.History
                    .Select(t => new ClipboardItem(t, Preview(t)))
            );
        });
    }

    [RelayCommand]
    private void PasteItem(string text)
    {
        _clipboardService.PasteItem(text);
        // Ctrl+V 전송으로 붙여넣기
        _inputService.HandleAction(new SendComboAction(["VK_CONTROL", "VK_V"]));
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _clipboardService.History.Clear();  // 내부 구현에 맞게 조정
        Items.Clear();
    }

    private static string Preview(string text)
    {
        // 최대 40자까지 미리보기
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= 40 ? singleLine : singleLine[..37] + "...";
    }
}

public record ClipboardItem(string FullText, string Preview);
```

### 3단계: 클립보드 패널 UI

**파일 (신규)**: `AltKey/Views/ClipboardPanel.xaml`

```xml
<UserControl ...>
    <Border Background="#CC1A1A1A" CornerRadius="8" Padding="4">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <!-- 헤더 -->
            <Grid Grid.Row="0" Margin="4,4,4,2">
                <TextBlock Text="클립보드" FontSize="11" Opacity="0.6" VerticalAlignment="Center"/>
                <Button Content="전체 삭제" HorizontalAlignment="Right"
                        Command="{Binding ClearHistoryCommand}"
                        Padding="4,2" FontSize="10" Background="Transparent"/>
            </Grid>

            <!-- 항목 목록 -->
            <ScrollViewer Grid.Row="1" MaxHeight="120"
                          VerticalScrollBarVisibility="Auto">
                <ItemsControl ItemsSource="{Binding Items}">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate DataType="{x:Type vm:ClipboardItem}">
                            <Button Command="{Binding DataContext.PasteItemCommand,
                                        RelativeSource={RelativeSource AncestorType=UserControl}}"
                                    CommandParameter="{Binding FullText}"
                                    HorizontalContentAlignment="Left"
                                    Background="Transparent" BorderThickness="0"
                                    Padding="6,4" Cursor="Hand">
                                <TextBlock Text="{Binding Preview}"
                                           FontSize="11" TextTrimming="CharacterEllipsis"
                                           Foreground="{StaticResource KeyFg}"/>
                            </Button>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>
        </Grid>
    </Border>
</UserControl>
```

### 4단계: AppConfig 및 토글

**파일**: `AltKey/Models/AppConfig.cs`

```csharp
public bool ClipboardPanelEnabled { get; set; } = false;
```

설정 패널에 토글 추가, `MainViewModel`에 `ClipboardViewModel` 추가.

**검증**:
1. 텍스트 복사 → 클립보드 패널에 항목 추가
2. 패널에서 항목 클릭 → 포커스된 앱에 해당 텍스트 붙여넣기
3. 전체 삭제 → 목록 비워짐

---

## T-8.5: 앱별 레이아웃 프로필 설정 UI

**설명**: 현재 `config.json`의 `profiles` 매핑을 코드로만 추가할 수 있다.
설정 패널에서 GUI로 프로세스↔레이아웃 매핑을 추가/삭제할 수 있게 한다.

**파일**: `AltKey/ViewModels/SettingsViewModel.cs`, `AltKey/Views/SettingsView.xaml`

**SettingsViewModel 추가**:
```csharp
[ObservableProperty]
private ObservableCollection<ProfileEntry> profiles = [];

[RelayCommand]
private void AddProfile()
{
    Profiles.Add(new ProfileEntry("", ""));
}

[RelayCommand]
private void RemoveProfile(ProfileEntry entry)
{
    Profiles.Remove(entry);
    SaveProfiles();
}

[RelayCommand]
private void SaveProfiles()
{
    _configService.Update(c =>
        c.Profiles = Profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.ProcessName))
            .ToDictionary(p => p.ProcessName.ToLower(), p => p.LayoutName));
}

public record ProfileEntry(string ProcessName, string LayoutName)
    : INotifyPropertyChanged { ... } // 편집 가능한 프로퍼티
```

**SettingsView.xaml 추가**:
```xml
<TextBlock Text="앱별 레이아웃" FontSize="12" FontWeight="SemiBold" Margin="0,8,0,4"/>
<ItemsControl ItemsSource="{Binding Profiles}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Grid Margin="0,2">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBox Text="{Binding ProcessName, UpdateSourceTrigger=PropertyChanged}"
                         PlaceholderText="예: code.exe" Grid.Column="0" Margin="0,0,4,0"/>
                <TextBlock Text="→" Grid.Column="1" VerticalAlignment="Center" Margin="0,0,4,0"/>
                <TextBox Text="{Binding LayoutName, UpdateSourceTrigger=PropertyChanged}"
                         PlaceholderText="레이아웃 이름" Grid.Column="2" Margin="0,0,4,0"/>
                <Button Content="✕" Grid.Column="3"
                        Command="{Binding DataContext.RemoveProfileCommand,
                            RelativeSource={RelativeSource AncestorType=UserControl}}"
                        CommandParameter="{Binding}"
                        Background="Transparent" BorderThickness="0"/>
            </Grid>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
<Button Content="+ 추가" Command="{Binding AddProfileCommand}"
        Margin="0,4,0,0" Padding="8,4"/>
<Button Content="저장" Command="{Binding SaveProfilesCommand}"
        Margin="4,4,0,0" Padding="8,4"/>
```

**검증**:
1. 프로필 추가: 프로세스명 `code.exe`, 레이아웃 `qwerty-en` → 저장
2. VS Code 포커스 시 자동으로 `qwerty-en` 레이아웃으로 전환됨
3. 항목 삭제 → config.json에서 제거됨
