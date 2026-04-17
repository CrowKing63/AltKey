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

    public string GetLabel(bool upperCase)
    {
        if (upperCase && Slot.ShiftLabel is { } s)
            return s;

        bool isAlphaKey = Slot.Label.Length == 1 && char.IsLetter(Slot.Label[0]);
        return isAlphaKey
            ? (upperCase ? Slot.Label.ToUpperInvariant() : Slot.Label.ToLowerInvariant())
            : Slot.Label;
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

    private bool _layoutSupportsKorean;

    private readonly DispatcherTimer _capsLockTimer;
    private bool _lastImeKorean = true;

    public event Action<bool>? ImeModeChanged;

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

        _layoutSupportsKorean = layout.Rows.Any(r =>
            r.Keys.Any(k =>
                k.Action is SendKeyAction { Vk: "VK_HANGUL" } ||
                k.EnglishLabel is not null));

        if (_layoutSupportsKorean)
        {
            _autoComplete.ResetState();
        }
        else
        {
            _autoComplete.ResetState();
            _autoComplete.ToggleKoreanSubmode();
        }

        _lastImeKorean = _layoutSupportsKorean;
    }

    public event Action? KeyTapped;

    [RelayCommand]
    private void KeyPressed(KeySlot slot)
    {
        _soundService.Play();

        bool handled = false;

        if (_configService.Current.AutoCompleteEnabled)
        {
            handled = HandleKeyWithAutoComplete(slot);
        }

        if (!handled && slot.Action is not null)
            _inputService.HandleAction(slot.Action);

        UpdateModifierState();
        KeyTapped?.Invoke();
    }

    private bool HandleKeyWithAutoComplete(KeySlot slot)
    {
        if (_layoutSupportsKorean && slot.Action is SendKeyAction { Vk: "VK_HANGUL" })
        {
            _autoComplete.ToggleKoreanSubmode();
            _inputService.TrackedOnScreenLength = 0;
            return false;
        }

        if (IsSeparatorKey(slot))
        {
            _autoComplete.OnSeparator();
            _inputService.TrackedOnScreenLength = 0;
            return false;
        }

        var ctx = new KeyContext(
            ShowUpperCase,
            _inputService.HasActiveModifiers,
            _inputService.HasActiveModifiersExcludingShift,
            _inputService.Mode,
            _inputService.TrackedOnScreenLength
        );

        return _autoComplete.OnKey(slot, ctx);
    }

    private static bool IsSeparatorKey(KeySlot slot)
    {
        if (slot.Action is not SendKeyAction { Vk: var vkStr }
            || !Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
            return false;

        return vk is VirtualKeyCode.VK_SPACE or VirtualKeyCode.VK_RETURN
            or VirtualKeyCode.VK_TAB or VirtualKeyCode.VK_OEM_PERIOD
            or VirtualKeyCode.VK_OEM_COMMA;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateModifierState();
        UpdateImeState();
    }

    private void UpdateImeState()
    {
        if (_inputService.Mode != InputMode.VirtualKey) return;
        if (!_layoutSupportsKorean) return;

        bool imeKorean = _inputService.IsImeKorean();
        if (imeKorean != _lastImeKorean)
        {
            _lastImeKorean = imeKorean;
            ImeModeChanged?.Invoke(imeKorean);

            if (imeKorean)
            {
                _autoComplete.ResetState();
            }
            else
            {
                _autoComplete.ResetState();
                _autoComplete.ToggleKoreanSubmode();
            }
        }
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
