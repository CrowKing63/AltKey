using System.Collections.ObjectModel;
using System.Windows.Threading;
using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

public record KeyRowVm(IReadOnlyList<KeySlotVm> Keys);

public class KeySlotVm(KeySlot slot, AutoCompleteService autoComplete) : ObservableObject
{
    public KeySlot Slot { get; } = slot;
    public double Width  { get; } = slot.Width;
    public double Height { get; } = slot.Height;

    private bool _isSticky;
    private bool _isLocked;
    public bool IsSticky { get => _isSticky; set => SetProperty(ref _isSticky, value); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }

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
        if (Slot.EnglishLabel is null) return "";
        if (_activeSubmode == InputSubmode.HangulJamo) return "";
        return upperCase && Slot.EnglishShiftLabel is { } hs ? hs : Slot.EnglishLabel;
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

    private readonly DispatcherTimer _capsLockTimer;

    [ObservableProperty]
    private ObservableCollection<KeyRowVm> rows = [];

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
        Rows = new ObservableCollection<KeyRowVm>(
            layout.Rows.Select(r => new KeyRowVm(
                r.Keys.Select(k => new KeySlotVm(k, _autoComplete)).ToList()
            ))
        );

        _autoComplete.ResetState();
        OnSubmodeChanged(_autoComplete.ActiveSubmode);
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

    private static bool IsSeparatorKey(KeySlot slot) => slot.Action switch
    {
        SendKeyAction { Vk: "VK_SPACE" }  => true,
        SendKeyAction { Vk: "VK_RETURN" } => true,
        SendKeyAction { Vk: "VK_TAB" }    => true,
        _ => false,
    };

    private void OnSubmodeChanged(InputSubmode submode)
    {
        foreach (var row in Rows)
            foreach (var keyVm in row.Keys)
            {
                keyVm.ActiveSubmode = submode;
                keyVm.SetComposeStateLabel(_autoComplete.ComposeStateLabel);
            }

        _liveRegion.Announce(submode == InputSubmode.HangulJamo
            ? "한국어 입력 상태"
            : "영어 입력 상태");
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

        foreach (var row in Rows)
        {
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
