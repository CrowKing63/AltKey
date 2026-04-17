using System.Collections.ObjectModel;
using System.Windows.Threading;
using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

public record KeyRowVm(IReadOnlyList<KeySlotVm> Keys);

public class KeySlotVm(KeySlot slot) : ObservableObject
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

    public bool GetIsDimmed(InputSubmode submode)
        => submode == InputSubmode.QuietEnglish && Slot.EnglishLabel is null;

    public string DisplayLabel { get; private set; } = "";
    public bool IsDimmed { get; private set; }

    private string? _autoCompleteComposeStateLabel;
    private bool _showUpperCase;

    public void RefreshDisplay()
    {
        DisplayLabel = GetLabel(_showUpperCase, _activeSubmode);
        IsDimmed = GetIsDimmed(_activeSubmode);
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(IsDimmed));
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
        return upperCase && Slot.EnglishShiftLabel is { } hs ? hs : Slot.EnglishLabel;
    }
}

public partial class KeyboardViewModel : ObservableObject
{
    private readonly InputService _inputService;
    private readonly SoundService _soundService;
    private readonly AutoCompleteService _autoComplete;
    private readonly ConfigService _configService;

    private readonly DispatcherTimer _capsLockTimer;

    [ObservableProperty]
    private ObservableCollection<KeyRowVm> rows = [];

    [ObservableProperty]
    private bool showUpperCase;

    [ObservableProperty]
    private bool showElevatedWarning;

    [ObservableProperty]
    private double keyUnit = 48.0;

    public KeyboardViewModel(InputService inputService, SoundService soundService,
        AutoCompleteService autoComplete, ConfigService configService)
    {
        _inputService = inputService;
        _soundService = soundService;
        _autoComplete = autoComplete;
        _configService = configService;
        _inputService.StickyStateChanged += UpdateModifierState;
        _inputService.ElevatedAppDetected += OnElevatedAppDetected;

        _autoComplete.SubmodeChanged += OnSubmodeChanged;

        _capsLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _capsLockTimer.Tick += OnTimerTick;
        _capsLockTimer.Start();
    }

    public void LoadLayout(LayoutConfig layout)
    {
        Rows = new ObservableCollection<KeyRowVm>(
            layout.Rows.Select(r => new KeyRowVm(
                r.Keys.Select(k => new KeySlotVm(k)).ToList()
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
