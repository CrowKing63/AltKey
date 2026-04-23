using System.Collections.ObjectModel;
using System.Windows.Threading;
using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

public record KeyRowVm(IReadOnlyList<KeySlotVm> Keys);

public record KeyColumnVm(double Gap, IReadOnlyList<KeyRowVm> Rows);

public class KeySlotVm(KeySlot slot, AutoCompleteService autoComplete) : ObservableObject
{
    public KeySlot Slot { get; } = slot;
    public double Width  { get; } = slot.Width;
    public double Height { get; } = slot.Height;

    private bool _isSticky;
    private bool _isLocked;
    private bool _isA11yFocused;
    public bool IsSticky { get => _isSticky; set => SetProperty(ref _isSticky, value); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }
    public bool IsA11yFocused { get => _isA11yFocused; set => SetProperty(ref _isA11yFocused, value); }

    public VirtualKeyCode? StickyVk =>
        Slot.Action is ToggleStickyAction ta &&
        Enum.TryParse<VirtualKeyCode>(ta.Vk, ignoreCase: true, out var vk)
            ? vk : null;

    public bool IsKoreanSubmodeToggle => Slot.Action is ToggleKoreanSubmodeAction;

    private InputSubmode _activeSubmode = InputSubmode.HangulJamo;
    public InputSubmode ActiveSubmode
    {
        get => _activeSubmode;
        set
        {
            if (SetProperty(ref _activeSubmode, value))
            {
                RefreshDisplay();
            }
        }
    }

    public string GetLabel(bool upperCase, InputSubmode submode)
    {
        if (IsKoreanSubmodeToggle)
            return _autoCompleteComposeStateLabel ?? "가";

        if (submode == InputSubmode.QuietEnglish && Slot.EnglishLabel is { Length: > 0 } eng)
        {
            string baseLabel = upperCase
                ? (Slot.EnglishShiftLabel ?? eng.ToUpperInvariant())
                : eng;
            return baseLabel;
        }

        return upperCase && Slot.ShiftLabel is { Length: > 0 } s
            ? s
            : Slot.Label;
    }

    public bool GetIsDimmed(InputSubmode submode) => false;

    public string DisplayLabel { get; private set; } = "";
    public string SubLabelText { get; private set; } = "";
    public bool IsDimmed { get; private set; }

    private string? _autoCompleteComposeStateLabel;
    private bool _showUpperCase;

    public void RefreshDisplay()
    {
        DisplayLabel = GetLabel(_showUpperCase, _activeSubmode);
        SubLabelText = GetSubLabel(_showUpperCase);
        IsDimmed = GetIsDimmed(_activeSubmode);
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(SubLabelText));
        OnPropertyChanged(nameof(IsDimmed));
        OnPropertyChanged(nameof(AccessibleName));
    }

    public void SetShowUpperCase(bool value)
    {
        if (_showUpperCase != value)
        {
            _showUpperCase = value;
            RefreshDisplay();
        }
    }

    public void SetComposeStateLabel(string? label)
    {
        if (_autoCompleteComposeStateLabel != label)
        {
            _autoCompleteComposeStateLabel = label;
            if (IsKoreanSubmodeToggle)
                RefreshDisplay();
        }
    }

    public string GetSubLabel(bool upperCase)
    {
        if (IsKoreanSubmodeToggle)
            return "";

        if (Slot.EnglishLabel is { Length: > 0 } eng)
        {
            if (_activeSubmode == InputSubmode.HangulJamo)
            {
                return upperCase
                    ? (Slot.EnglishShiftLabel ?? eng.ToUpperInvariant())
                    : eng;
            }
            else if (_activeSubmode == InputSubmode.QuietEnglish)
            {
                return upperCase && Slot.ShiftLabel is { Length: > 0 } s
                    ? s
                    : Slot.Label;
            }
        }
        return "";
    }

    // ── Accessibility ────────────────────────────────────────────────────────

    public string AccessibleName => ComputeAccessibleName();
    public string AccessibleHelp => ComputeAccessibleHelp();
    public string AutomationId   => Slot.Action?.GetType().Name ?? "UnknownAction";

    private string ComputeAccessibleName()
    {
        if (Slot.Action is ToggleKoreanSubmodeAction)
        {
            return autoComplete.ActiveSubmode == InputSubmode.HangulJamo
                ? "한국어 입력 중, 누르면 영어로 전환"
                : "영어 입력 중, 누르면 한국어로 전환";
        }

        var submode = _activeSubmode;
        if (submode == InputSubmode.HangulJamo)
        {
            string label = _showUpperCase && Slot.ShiftLabel is { } s ? s : Slot.Label;
            string? jamoName = JamoNameResolver.ResolveKorean(label);
            if (jamoName is not null) return jamoName;
        }
        else
        {
            string? letter = _showUpperCase
                ? (Slot.EnglishShiftLabel ?? Slot.EnglishLabel?.ToUpperInvariant())
                : Slot.EnglishLabel;
            if (letter is not null) return $"{letter} 키";
        }

        return ResolveFunctionKeyName(Slot);
    }

    private string ComputeAccessibleHelp()
    {
        if (IsSticky) return "일회성 고정 상태";
        if (IsLocked) return "영구 고정 상태";
        return "";
    }

    private static string ResolveFunctionKeyName(KeySlot slot)
    {
        if (slot.Action is SendKeyAction { Vk: var vkStr }
            && Enum.TryParse<VirtualKeyCode>(vkStr, ignoreCase: true, out var vk))
        {
            return vk switch
            {
                VirtualKeyCode.VK_SHIFT   => "시프트 키",
                VirtualKeyCode.VK_LSHIFT  => "왼쪽 시프트 키",
                VirtualKeyCode.VK_RSHIFT  => "오른쪽 시프트 키",
                VirtualKeyCode.VK_CONTROL => "컨트롤 키",
                VirtualKeyCode.VK_LCONTROL => "왼쪽 컨트롤 키",
                VirtualKeyCode.VK_RCONTROL => "오른쪽 컨트롤 키",
                VirtualKeyCode.VK_MENU    => "알트 키",
                VirtualKeyCode.VK_LMENU   => "왼쪽 알트 키",
                VirtualKeyCode.VK_RMENU   => "오른쪽 알트 키",
                VirtualKeyCode.VK_RETURN  => "엔터 키",
                VirtualKeyCode.VK_SPACE   => "스페이스 키",
                VirtualKeyCode.VK_TAB     => "탭 키",
                VirtualKeyCode.VK_BACK    => "백스페이스 키",
                VirtualKeyCode.VK_DELETE  => "딜리트 키",
                VirtualKeyCode.VK_INSERT  => "인서트 키",
                VirtualKeyCode.VK_HOME    => "홈 키",
                VirtualKeyCode.VK_END     => "엔드 키",
                VirtualKeyCode.VK_LEFT    => "왼쪽 화살표 키",
                VirtualKeyCode.VK_RIGHT   => "오른쪽 화살표 키",
                VirtualKeyCode.VK_UP      => "위쪽 화살표 키",
                VirtualKeyCode.VK_DOWN    => "아래쪽 화살표 키",
                VirtualKeyCode.VK_PRIOR   => "페이지 업 키",
                VirtualKeyCode.VK_NEXT    => "페이지 다운 키",
                VirtualKeyCode.VK_ESCAPE  => "이스케이프 키",
                VirtualKeyCode.VK_CAPITAL => "캡스 락 키",
                VirtualKeyCode.VK_F1 => "F1 키", VirtualKeyCode.VK_F2 => "F2 키",
                VirtualKeyCode.VK_F3 => "F3 키", VirtualKeyCode.VK_F4 => "F4 키",
                VirtualKeyCode.VK_F5 => "F5 키", VirtualKeyCode.VK_F6 => "F6 키",
                VirtualKeyCode.VK_F7 => "F7 키", VirtualKeyCode.VK_F8 => "F8 키",
                VirtualKeyCode.VK_F9 => "F9 키", VirtualKeyCode.VK_F10 => "F10 키",
                VirtualKeyCode.VK_F11 => "F11 키", VirtualKeyCode.VK_F12 => "F12 키",
                VirtualKeyCode.VK_HANGUL => "한글 키",
                VirtualKeyCode.VK_HANJA  => "한자 키",
                _ => slot.Label,
            };
        }

        if (slot.Action is ToggleStickyAction { Vk: var stickyVk })
        {
            return $"{stickyVk} 고정 키";
        }

        if (slot.Action is SwitchLayoutAction { Name: var layoutName })
        {
            return $"{layoutName} 레이아웃 전환";
        }

        return slot.Label;
    }
}

public partial class KeyboardViewModel : ObservableObject
{
    private readonly InputService _inputService;
    private readonly SoundService _soundService;
    private readonly AutoCompleteService _autoComplete;
    private readonly ConfigService _configService;
    private readonly LiveRegionService _liveRegion;
    private readonly List<KeySlotVm> _a11yNavigableSlots = [];
    private int _a11yFocusIndex = -1;

    private readonly DispatcherTimer _capsLockTimer;

    [ObservableProperty]
    private ObservableCollection<KeyColumnVm> columns = [];

    // 동적 크기 계산용 프로퍼티 (열 기준)
    [ObservableProperty] private double maxRowUnits = 15.0;
    [ObservableProperty] private double maxRowCount = 14.0;
    [ObservableProperty] private double rowCount    = 5.0;

    /// 빈 행을 포함해 모든 행이 확보해야 할 픽셀 높이 (KeyUnit + 키 Margin 상하 합)
    public double KeyRowHeight => KeyUnit + 4.0;

    partial void OnColumnsChanged(ObservableCollection<KeyColumnVm> value)
    {
        RecalculateLayoutMetrics();
    }

    partial void OnKeyUnitChanged(double value)
    {
        OnPropertyChanged(nameof(KeyRowHeight));
    }

    /// 열 단위 기준으로 가로·세로 단위를 재계산.
    /// - MaxRowUnits = Σ(열별 가장 긴 행의 단위) + Σ(열 간격)
    /// - MaxRowCount = Σ(열별 가장 긴 행의 키 개수)
    /// - RowCount    = max(열별 행 개수)
    private void RecalculateLayoutMetrics()
    {
        if (Columns.Count == 0 || Columns.All(c => c.Rows.Count == 0))
        {
            MaxRowUnits = 15.0;
            MaxRowCount = 14.0;
            RowCount    = 5.0;
            return;
        }

        double totalUnits = 0;
        double totalKeys  = 0;
        double maxRows    = 0;
        bool first = true;

        foreach (var col in Columns)
        {
            if (!first) totalUnits += col.Gap;
            first = false;

            if (col.Rows.Count == 0) continue;

            double colUnits = col.Rows.Max(r =>
                r.Keys.Sum(k => k.Width) + r.Keys.Sum(k => k.Slot.GapBefore));
            int    colKeys  = col.Rows.Max(r => r.Keys.Count);

            totalUnits += colUnits;
            totalKeys  += colKeys;
            maxRows     = Math.Max(maxRows, col.Rows.Count);
        }

        MaxRowUnits = Math.Max(1, totalUnits);
        MaxRowCount = Math.Max(1, totalKeys);
        RowCount    = Math.Max(1, maxRows);
    }

    [ObservableProperty]
    private bool showUpperCase;

    [ObservableProperty]
    private bool showElevatedWarning;

    [ObservableProperty]
    private double keyUnit = 48.0;

    [ObservableProperty]
    private string modeAnnouncement = "";

    public KeyboardViewModel(InputService inputService, SoundService soundService,
        AutoCompleteService autoComplete, ConfigService configService, LiveRegionService liveRegion)
    {
        _inputService = inputService;
        _soundService = soundService;
        _autoComplete = autoComplete;
        _configService = configService;
        _liveRegion = liveRegion;
        _inputService.StickyStateChanged += UpdateModifierState;
        _inputService.ElevatedAppDetected += OnElevatedAppDetected;
        _configService.ConfigChanged += OnConfigChanged;

        _autoComplete.SubmodeChanged += OnSubmodeChanged;

        _liveRegion.Announced += msg =>
        {
            ModeAnnouncement = msg;
            RaiseLiveRegionChanged();
        };

        _capsLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _capsLockTimer.Tick += OnTimerTick;
        _capsLockTimer.Start();
    }

    public event Action? LiveRegionChanged;

    private void RaiseLiveRegionChanged()
    {
        LiveRegionChanged?.Invoke();
    }

    public void LoadLayout(LayoutConfig layout)
    {
        if (layout.Columns is { Count: > 0 })
        {
            Columns = new ObservableCollection<KeyColumnVm>(
                layout.Columns.Select(col => new KeyColumnVm(
                    col.Gap,
                    col.Rows?.Select(r => new KeyRowVm(
                        r.Keys.Select(k => new KeySlotVm(k, _autoComplete)).ToList()
                    )).ToList() ?? []
                ))
            );
        }
        else
        {
            Columns = [];
        }

        _autoComplete.ResetState();
        RefreshKeyLabels(_autoComplete.ActiveSubmode);
        ResetA11yNavigationState();
    }

    public event Action? KeyTapped;

    [RelayCommand]
    private void KeyPressed(KeySlot slot)
    {
        _soundService.Play();

        if (slot.Action is ToggleKoreanSubmodeAction)
        {
            _autoComplete.ToggleKoreanSubmode();
            UpdateModifierState();
            KeyTapped?.Invoke();
            return;
        }

        if (_inputService.IsForegroundOwnWindow())
        {
            _autoComplete.CancelComposition();

            if (FocusTracker.LastFocused is { IsVisible: true } tb)
                System.Windows.Input.Keyboard.Focus(tb);

            if (IsSeparatorKey(slot))
            {
                if (slot.Action is not null)
                    _inputService.HandleAction(slot.Action);
            }
            else if (slot.Action is not null)
            {
                _inputService.HandleAction(slot.Action);
            }

            UpdateModifierState();
            KeyTapped?.Invoke();
            return;
        }

        if (IsSeparatorKey(slot))
        {
            _autoComplete.OnSeparator();
            if (slot.Action is not null)
                _inputService.HandleAction(slot.Action);
            UpdateModifierState();
            KeyTapped?.Invoke();
            return;
        }

        var ctx = new KeyContext(
            ShowUpperCase,
            _inputService.HasActiveModifiers,
            _inputService.HasActiveModifiersExcludingShift,
            _inputService.Mode,
            _inputService.TrackedOnScreenLength);

        bool handled = _autoComplete.OnKey(slot, ctx);
        if (!handled && slot.Action is not null)
            _inputService.HandleAction(slot.Action);

        UpdateModifierState();
        KeyTapped?.Invoke();
    }

    public void MoveA11yFocus(bool reverse)
    {
        if (!_configService.Current.KeyboardA11yNavigationEnabled)
            return;

        RebuildA11yNavigableSlots();
        if (_a11yNavigableSlots.Count == 0)
            return;

        int count = _a11yNavigableSlots.Count;
        int nextIndex;

        if (_a11yFocusIndex < 0 || _a11yFocusIndex >= count)
        {
            nextIndex = reverse ? count - 1 : 0;
        }
        else
        {
            nextIndex = reverse
                ? (_a11yFocusIndex - 1 + count) % count
                : (_a11yFocusIndex + 1) % count;
        }

        SetA11yFocus(nextIndex);
    }

    public void ActivateA11yFocused()
    {
        if (!_configService.Current.KeyboardA11yNavigationEnabled)
            return;

        RebuildA11yNavigableSlots();
        if (_a11yNavigableSlots.Count == 0)
            return;

        if (_a11yFocusIndex < 0 || _a11yFocusIndex >= _a11yNavigableSlots.Count)
        {
            SetA11yFocus(0);
            return;
        }

        var focused = _a11yNavigableSlots[_a11yFocusIndex];
        KeyPressed(focused.Slot);
    }

    private void RebuildA11yNavigableSlots()
    {
        _a11yNavigableSlots.Clear();
        foreach (var vm in EnumerateSlotVms())
            _a11yNavigableSlots.Add(vm);
    }

    private IEnumerable<KeySlotVm> EnumerateSlotVms()
    {
        foreach (var col in Columns)
        foreach (var row in col.Rows)
        foreach (var slotVm in row.Keys)
            yield return slotVm;
    }

    private void SetA11yFocus(int nextIndex)
    {
        if (_a11yFocusIndex >= 0 && _a11yFocusIndex < _a11yNavigableSlots.Count)
            _a11yNavigableSlots[_a11yFocusIndex].IsA11yFocused = false;

        _a11yFocusIndex = nextIndex;

        if (_a11yFocusIndex >= 0 && _a11yFocusIndex < _a11yNavigableSlots.Count)
            _a11yNavigableSlots[_a11yFocusIndex].IsA11yFocused = true;
    }

    private void ResetA11yNavigationState()
    {
        foreach (var vm in EnumerateSlotVms())
            vm.IsA11yFocused = false;

        _a11yNavigableSlots.Clear();
        _a11yFocusIndex = -1;
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName is null or nameof(AppConfig.KeyboardA11yNavigationEnabled))
        {
            if (!_configService.Current.KeyboardA11yNavigationEnabled)
                ResetA11yNavigationState();
        }
    }

    private static bool IsSeparatorKey(KeySlot slot) => slot.Action switch
    {
        SendKeyAction { Vk: "VK_SPACE" }  => true,
        SendKeyAction { Vk: "VK_RETURN" } => true,
        SendKeyAction { Vk: "VK_TAB" }    => true,
        _ => false,
    };

    private void OnSubmodeChanged(InputSubmode submode)
    {
        RefreshKeyLabels(submode);

        _liveRegion.Announce(submode == InputSubmode.HangulJamo
            ? "한국어 입력 상태"
            : "영어 입력 상태");
    }

    private void RefreshKeyLabels(InputSubmode submode)
    {
        foreach (var col in Columns)
            foreach (var row in col.Rows)
                foreach (var keyVm in row.Keys)
                {
                    keyVm.ActiveSubmode = submode;
                    keyVm.SetComposeStateLabel(_autoComplete.ComposeStateLabel);
                }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateModifierState();
    }

    private void UpdateModifierState()
    {
        ShowUpperCase =
            _inputService.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT) ||
            _inputService.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT) ||
            _inputService.StickyKeys.Contains(VirtualKeyCode.VK_LSHIFT) ||
            _inputService.LockedKeys.Contains(VirtualKeyCode.VK_LSHIFT) ||
            _inputService.IsCapsLockOn;

        foreach (var col in Columns)
        foreach (var row in col.Rows)
        foreach (var slotVm in row.Keys)
        {
            if (slotVm.StickyVk is { } vk)
            {
                slotVm.IsSticky = _inputService.StickyKeys.Contains(vk);
                slotVm.IsLocked = _inputService.LockedKeys.Contains(vk);
            }

            if (slotVm.Slot.Action is SendKeyAction { Vk: "VK_CAPITAL" })
            {
                slotVm.IsLocked = _inputService.IsCapsLockOn;
            }

            slotVm.SetShowUpperCase(ShowUpperCase);
        }
    }

    private void OnElevatedAppDetected()
    {
        if (_inputService.Mode == InputMode.VirtualKey)
            return;

        ShowElevatedWarning = true;

        var dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        dismissTimer.Tick += (_, _) =>
        {
            ShowElevatedWarning = false;
            dismissTimer.Stop();
        };
        dismissTimer.Start();
    }
}
