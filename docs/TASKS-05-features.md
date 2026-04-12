# Phase 5: 고급 기능

> 목표: 체류 클릭, 앱별 프로필 자동 전환, 시스템 트레이, 전역 단축키, 설정 패널을 구현한다.

**의존성**: Phase 1~4 완료

---

## T-5.1: 체류 클릭 — DispatcherTimer 로직

**설명**: 마우스 커서가 `KeyButton` 위에 일정 시간 머무르면 자동 클릭되는 로직을 구현한다.

**파일**: `Controls/KeyButton.cs`

**구현 내용**:
```csharp
public class KeyButton : Button
{
    private DispatcherTimer? _dwellTimer;
    private DateTime _dwellStart;

    public static readonly DependencyProperty DwellEnabledProperty =
        DependencyProperty.Register(nameof(DwellEnabled), typeof(bool), typeof(KeyButton));
    public static readonly DependencyProperty DwellTimeProperty =
        DependencyProperty.Register(nameof(DwellTime), typeof(int), typeof(KeyButton),
            new PropertyMetadata(800));

    public bool DwellEnabled { get; set; }
    public int DwellTime { get; set; }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (!DwellEnabled) return;
        _dwellStart = DateTime.UtcNow;
        _dwellTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _dwellTimer.Tick += DwellTick;
        _dwellTimer.Start();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        CancelDwell();
    }

    private void DwellTick(object? s, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _dwellStart).TotalMilliseconds;
        DwellProgress = elapsed / DwellTime; // 0.0 ~ 1.0, UI 바인딩용
        if (elapsed >= DwellTime)
        {
            CancelDwell();
            RaiseEvent(new RoutedEventArgs(ClickEvent)); // 클릭 이벤트 발생
        }
    }

    private void CancelDwell()
    {
        _dwellTimer?.Stop();
        _dwellTimer = null;
        DwellProgress = 0;
    }

    [ObservableProperty] private double dwellProgress; // 0.0~1.0
}
```

**검증**: `DwellEnabled=true`, `DwellTime=800` 설정 후 키 위에 800ms 유지 시 입력 발생.

---

## T-5.2: 체류 클릭 — 프로그레스 링 UI

**설명**: 체류 진행도를 `KeyButton` 위에 원형 프로그레스로 표시한다.

**파일**: `Controls/KeyButton.xaml` (ControlTemplate 내 추가)

**구현 내용**:
```xml
<!-- ControlTemplate 안에 오버레이 -->
<Grid>
    <!-- 기존 키 내용 -->
    <Border x:Name="Root" .../>

    <!-- 체류 프로그레스 링 -->
    <Ellipse x:Name="DwellRing"
             Width="36" Height="36"
             Stroke="{StaticResource KeyFgSticky}"
             StrokeThickness="3"
             StrokeDashArray="100"
             StrokeDashOffset="{Binding DwellProgress,
                 RelativeSource={RelativeSource TemplatedParent},
                 Converter={StaticResource ProgressToOffsetConverter}}"
             Opacity="{Binding DwellProgress,
                 RelativeSource={RelativeSource TemplatedParent}}"
             IsHitTestVisible="False"/>
</Grid>
```

`ProgressToOffsetConverter`: `progress → 100 * (1 - progress)` (Ellipse 둘레를 비율로 채움)

**검증**: 체류 시작 시 파란색 링이 시계 방향으로 채워짐.

---

## T-5.3: WinEventHook 포그라운드 앱 감지

**설명**: `SetWinEventHook`으로 포그라운드 앱 전환 이벤트를 구독하고, 프로세스 이름을 이벤트로 발행한다.

**파일**: `Services/ProfileService.cs`

**구현 내용**:
```csharp
public class ProfileService : IDisposable
{
    private IntPtr _hook;
    private Win32.WinEventDelegate? _delegateRef; // GC 방지용 참조 보관

    public event Action<string>? ForegroundAppChanged;

    public void Start()
    {
        _delegateRef = OnWinEvent;
        _hook = Win32.SetWinEventHook(
            0x0003, // EVENT_SYSTEM_FOREGROUND
            0x0003,
            IntPtr.Zero,
            _delegateRef,
            0, 0,
            0x0000); // WINEVENT_OUTOFCONTEXT
    }

    private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd,
        int idObj, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero) return;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            ForegroundAppChanged?.Invoke(proc.ProcessName.ToLower() + ".exe");
        }
        catch { /* 프로세스가 종료된 경우 무시 */ }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) Win32.UnhookWinEvent(_hook);
    }
}
```

**검증**: Chrome → 메모장으로 전환 시 콘솔에 "notepad.exe" 출력됨.

---

## T-5.4: 앱 프로필 자동 레이아웃 전환

**설명**: `ForegroundAppChanged` 이벤트에 구독하여 `config.json`의 `profiles` 매핑을 참조, 레이아웃을 전환한다.

**파일**: `Services/ProfileService.cs` 또는 `ViewModels/MainViewModel.cs`

**구현 내용**:
```csharp
_profileService.ForegroundAppChanged += processName =>
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        var config = _configService.Current;
        if (!config.AutoProfileSwitch) return;

        if (config.Profiles.TryGetValue(processName, out var layoutName))
        {
            try { SwitchLayout(layoutName); }
            catch (Exception ex) { Log.Warning(ex, "프로필 전환 실패: {name}", layoutName); }
        }
        // 매핑 없으면 기본 레이아웃 유지
    });
};
```

**검증**: Photoshop 포커스 시 `profiles: { "photoshop.exe": "photoshop" }` 매핑에 따라 커스텀 레이아웃 로드.

---

## T-5.5: 시스템 트레이 (NotifyIcon)

**설명**: `System.Windows.Forms.NotifyIcon`을 사용하여 시스템 트레이 아이콘과 컨텍스트 메뉴를 구성한다.

**파일**: `Services/TrayService.cs`

**구현 내용**:
```csharp
public class TrayService : IDisposable
{
    private NotifyIcon _notifyIcon = null!;
    private Window? _mainWindow;

    public void Initialize(Window window)
    {
        _mainWindow = window;
        _notifyIcon = new NotifyIcon
        {
            Icon = new System.Drawing.Icon("Assets/icon.ico"),
            Text = "AltKey",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("보이기/숨기기", null, (_, _) => ToggleVisibility());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("설정", null, (_, _) => ShowSettings());
        menu.Items.Add("종료", null, (_, _) => Application.Current.Shutdown());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();
    }

    private void ToggleVisibility()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_mainWindow!.IsVisible) _mainWindow.Hide();
            else { _mainWindow.Show(); _mainWindow.Activate(); }
        });
    }

    public void Dispose() => _notifyIcon.Dispose();
}
```

**검증**: 창 X 버튼 → 트레이로 이동 (종료 아님). 트레이 아이콘 더블클릭 → 창 복귀.

---

## T-5.6: 창 닫기 → 트레이 최소화 처리

**설명**: 사용자가 창의 X 버튼을 누르면 종료 대신 트레이로 숨긴다.

**파일**: `MainWindow.cs`

**구현 내용**:
```csharp
protected override void OnClosing(CancelEventArgs e)
{
    // 앱이 실제 종료 중(Shutdown 호출)이 아니라면 트레이로 숨김
    if (!_isShuttingDown)
    {
        e.Cancel = true;
        Hide();
        // 첫 번째 숨김 시 트레이 풍선 알림 (한 번만)
        if (!_trayNotified)
        {
            _trayService.ShowBalloon("AltKey가 트레이에서 실행 중입니다.");
            _trayNotified = true;
        }
    }
    else
    {
        _configService.Save(); // 종료 전 설정 저장
    }
}
```

**검증**: X 버튼 클릭 → 창 사라짐, 트레이 아이콘 유지. `Ctrl+Alt+K` → 창 복귀.

---

## T-5.7: 전역 단축키 등록 (RegisterHotKey)

**설명**: `RegisterHotKey` Win32 API로 전역 단축키를 등록하고, WPF 메시지 루프에서 처리한다.

**파일**: `Services/HotkeyService.cs` (신규)

**구현 내용**:
```csharp
public class HotkeyService : IDisposable
{
    private const int HOTKEY_ID = 9001;
    private HwndSource? _source;
    public event Action? HotkeyPressed;

    public void Register(IntPtr hwnd, uint modifiers, uint vk)
    {
        _source = HwndSource.FromHwnd(hwnd);
        _source.AddHook(HwndHook);
        Win32.RegisterHotKey(hwnd, HOTKEY_ID, modifiers, vk);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HOTKEY_ID) // WM_HOTKEY
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is not null)
            Win32.UnregisterHotKey(_source.Handle, HOTKEY_ID);
    }
}
```

호출:
```csharp
// Ctrl+Alt+K → modifiers=0x0003 (MOD_ALT|MOD_CONTROL), vk=0x4B ('K')
_hotkeyService.Register(hwnd, 0x0003, 0x4B);
_hotkeyService.HotkeyPressed += () => _trayService.ToggleVisibility();
```

**검증**: 앱 포커스 없는 상태에서 Ctrl+Alt+K 입력 시 창 토글됨.

---

## T-5.8: 전역 단축키 사용자 커스텀

**설명**: `config.json`의 `global_hotkey` 문자열 값을 파싱하여 단축키를 동적으로 등록한다.

**파일**: `Services/HotkeyService.cs`

**구현 내용**:
```csharp
/// "Ctrl+Alt+K" 형식 문자열을 modifiers + vk 쌍으로 파싱
public static (uint modifiers, uint vk) ParseHotkey(string hotkey)
{
    uint mods = 0;
    var parts = hotkey.Split('+').Select(s => s.Trim()).ToList();
    if (parts.Contains("Ctrl"))  mods |= 0x0002; // MOD_CONTROL
    if (parts.Contains("Alt"))   mods |= 0x0001; // MOD_ALT
    if (parts.Contains("Shift")) mods |= 0x0004; // MOD_SHIFT
    if (parts.Contains("Win"))   mods |= 0x0008; // MOD_WIN

    var keyStr = parts.Last();
    var vk = (uint)(keyStr.Length == 1 ? char.ToUpper(keyStr[0]) : 0);
    return (mods, vk);
}
```

설정 변경 시: 기존 단축키 `UnregisterHotKey` → 새 단축키 `RegisterHotKey`.

**검증**: `"global_hotkey": "Ctrl+Shift+K"` 변경 후 앱 재시작 없이(설정 저장 시 재등록) 새 단축키 동작.

---

## T-5.9: AppConfig 모델 및 ConfigService 구현

**설명**: `config.json`의 C# 모델과 CRUD 서비스를 작성한다.

**파일**: `Models/AppConfig.cs`, `Services/ConfigService.cs`

**AppConfig 구조**:
```csharp
public class AppConfig
{
    public string Version { get; set; } = "1.0.0";
    public string Language { get; set; } = "ko";
    public string DefaultLayout { get; set; } = "qwerty-ko";
    public bool AlwaysOnTop { get; set; } = true;
    public double OpacityIdle { get; set; } = 0.4;
    public double OpacityActive { get; set; } = 1.0;
    public int FadeDelayMs { get; set; } = 5000;
    public bool DwellEnabled { get; set; } = false;
    public int DwellTimeMs { get; set; } = 800;
    public bool StickyKeysEnabled { get; set; } = true;
    public string Theme { get; set; } = "system"; // "light"|"dark"|"system"
    public string GlobalHotkey { get; set; } = "Ctrl+Alt+K";
    public bool AutoProfileSwitch { get; set; } = true;
    public Dictionary<string, string> Profiles { get; set; } = [];
    public WindowConfig Window { get; set; } = new();
}

public class WindowConfig
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 700;
    public double Width { get; set; } = 900;
    public double Height { get; set; } = 320;
}
```

**ConfigService**:
```csharp
public class ConfigService
{
    public AppConfig Current { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(PathResolver.ConfigPath)) { Save(); return; }
        var json = File.ReadAllText(PathResolver.ConfigPath);
        Current = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default) ?? new();
    }

    public void Save() =>
        File.WriteAllText(PathResolver.ConfigPath,
            JsonSerializer.Serialize(Current, JsonOptions.Default));

    public void Update(Action<AppConfig> updater)
    {
        updater(Current);
        Save();
    }
}
```

**검증**: `configService.Update(c => c.DwellEnabled = true)` → `config.json`에 `"dwell_enabled": true` 기록됨.

---

## T-5.10: 설정 패널 UI (SettingsView)

**설명**: 기어 아이콘 클릭 시 키보드 위에 오버레이로 표시되는 설정 패널을 작성한다.

**파일**: `Views/SettingsView.xaml`, `ViewModels/SettingsViewModel.cs`

**포함 항목**:
- 테마 선택 (라디오 버튼: 라이트 / 다크 / 시스템 따라)
- 항상 위 체크박스
- 유휴 투명도 슬라이더 (0.1 ~ 1.0)
- 페이딩 딜레이 슬라이더 (1~30초)
- 체류 클릭 토글 + 시간 슬라이더
- 레이아웃 선택 콤보박스
- 전역 단축키 표시 (텍스트)

**패널 표시**: `MainView`의 `Grid`에 `SettingsView`를 `Visibility=Collapsed`로 배치, 기어 버튼 클릭 시 `Visible`로 전환.

**검증**: 기어 아이콘 클릭 시 설정 패널 오버레이 표시. 값 변경 즉시 반영(양방향 바인딩).

---

## T-5.11: 트레이 레이아웃 서브메뉴

**설명**: 트레이 컨텍스트 메뉴에 사용 가능한 레이아웃 목록을 서브메뉴로 추가한다.

**파일**: `Services/TrayService.cs`

**구현 내용**:
```csharp
var layoutMenu = new ToolStripMenuItem("레이아웃");
foreach (var name in _layoutService.GetAvailableLayouts())
{
    var item = new ToolStripMenuItem(name);
    item.Click += (_, _) => _mainViewModel.SwitchLayout(name);
    layoutMenu.DropDownItems.Add(item);
}
menu.Items.Insert(1, layoutMenu);
```

**검증**: 트레이 메뉴 → "레이아웃" → "qwerty-en" 클릭 시 레이아웃 전환됨.

---

## T-5.12: 관리자 권한 모드 실행 옵션

**설명**: 설정 패널에서 "관리자 권한으로 재시작" 버튼을 제공하여 UAC 상승 앱에도 입력을 전달할 수 있게 한다.

**파일**: `ViewModels/SettingsViewModel.cs`

**구현 내용**:
```csharp
[RelayCommand]
private void RestartAsAdmin()
{
    var psi = new ProcessStartInfo
    {
        FileName = Environment.ProcessPath,
        Verb = "runas", // UAC 대화상자 요청
        UseShellExecute = true,
    };
    try
    {
        Process.Start(psi);
        Application.Current.Shutdown();
    }
    catch (Win32Exception)
    {
        // 사용자가 UAC 취소
    }
}
```

**UI**: "⚠️ 관리자 앱에 입력하려면 관리자 권한으로 재시작하세요" 경고 + 버튼.

**검증**: 버튼 클릭 → UAC 팝업 → 승인 시 관리자 권한으로 재시작됨.
