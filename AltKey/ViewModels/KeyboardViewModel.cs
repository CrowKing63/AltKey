using System.Collections.ObjectModel;
using System.Windows.Threading;
using AltKey.Models;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

// ── ViewModel 타입 ──────────────────────────────────────────────────────────

public record KeyRowVm(IReadOnlyList<KeySlotVm> Keys);

public class KeySlotVm(KeySlot slot) : ObservableObject
{
    public KeySlot Slot { get; } = slot;
    public double Width  { get; } = slot.Width;
    public double Height { get; } = slot.Height;

    // T-4.7: Sticky / Locked 상태 (KeyboardViewModel이 갱신)
    private bool _isSticky;
    private bool _isLocked;
    public bool IsSticky { get => _isSticky; set => SetProperty(ref _isSticky, value); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }

    /// 이 슬롯의 ToggleSticky VK 코드 (없으면 null)
    public VirtualKeyCode? StickyVk =>
        Slot.Action is ToggleStickyAction ta &&
        Enum.TryParse<VirtualKeyCode>(ta.Vk, ignoreCase: true, out var vk)
            ? vk : null;

    /// 메인 레이블 (영문 기준). Shift/CapsLock 상태에 따라 대소문자·기호 전환.
    public string GetLabel(bool upperCase)
    {
        // 기호 키: shift_label 우선
        if (upperCase && Slot.ShiftLabel is { } s)
            return s;

        // 알파벳: 자동 대소문자
        bool isAlphaKey = Slot.Label.Length == 1 && char.IsLetter(Slot.Label[0]);
        return isAlphaKey
            ? (upperCase ? Slot.Label.ToUpperInvariant() : Slot.Label.ToLowerInvariant())
            : Slot.Label;
    }

    /// 서브 레이블 (한글 자모). 통합 레이아웃에서 키 우상단에 항상 표시.
    public string GetSubLabel(bool upperCase)
    {
        if (Slot.HangulLabel is null) return "";
        return upperCase && Slot.HangulShiftLabel is { } hs ? hs : Slot.HangulLabel;
    }
}

// ── KeyboardViewModel ───────────────────────────────────────────────────────

public partial class KeyboardViewModel : ObservableObject
{
    private readonly InputService _inputService;
    private readonly SoundService _soundService;
    private readonly AutoCompleteService _autoComplete;
    private readonly ConfigService _configService;

    // 한/영 내부 상태 (IME와 무관하게 자체 관리)
    private bool _isKoreanInput = true;

    // 현재 레이아웃이 한글 자모/VK_HANGUL 키를 포함하는지 여부
    private bool _layoutSupportsKorean;

    // T-2.7: 100ms 주기 폴링으로 CapsLock 및 IME 한/영 상태 동기화
    private readonly DispatcherTimer _capsLockTimer;
    private bool _lastImeKorean = true;

    // 이벤트: IME 한/영 상태가 바뀌었을 때 발생
    public event Action<bool>? ImeModeChanged;

    // ── Observable 속성 ─────────────────────────────────────────────────────
    [ObservableProperty]
    private ObservableCollection<KeyRowVm> rows = [];

    /// Shift 고정 또는 CapsLock ON 시 true → 키 라벨 대문자 표시
    [ObservableProperty]
    private bool showUpperCase;

    /// T-2.10: UAC 상승 앱 경고 배너 표시 여부
    [ObservableProperty]
    private bool showElevatedWarning;

    // T-4.10: 반응형 키 크기
    [ObservableProperty]
    private double keyUnit = 48.0;


    // ── 생성자 ──────────────────────────────────────────────────────────────
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

    // ── 레이아웃 로드 ────────────────────────────────────────────────────────
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
                k.HangulLabel is not null));

        _isKoreanInput = _layoutSupportsKorean;
        _lastImeKorean = _layoutSupportsKorean;
    }

    /// 키가 눌릴 때 발생하는 이벤트 (패널 자동 닫기 등 외부 연동용)
    public event Action? KeyTapped;

    // ── 커맨드 ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void KeyPressed(KeySlot slot)
    {
        _soundService.Play();

        bool handledByComposer = false;

        if (_configService.Current.AutoCompleteEnabled && _layoutSupportsKorean)
            handledByComposer = HandleKoreanLayoutKey(slot);
        else if (_configService.Current.AutoCompleteEnabled)
        {
            HandleEnglishLayoutKey(slot);
            handledByComposer = ShouldSkipHandleAction(slot);
        }

        if (!handledByComposer && slot.Action is not null)
            _inputService.HandleAction(slot.Action);

        UpdateModifierState();
        KeyTapped?.Invoke();
    }

    /// Unicode 모드 + 알파벳 키는 SendUnicode로 이미 전송했으므로 HandleAction 스킵
    private bool ShouldSkipHandleAction(KeySlot slot)
    {
        if (_inputService.Mode != InputMode.Unicode) return false;
        if (_inputService.HasActiveModifiers) return false;
        if (slot.Action is not SendKeyAction { Vk: var vkStr }
            || !Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
            return false;
        return vk >= VirtualKeyCode.VK_A && vk <= VirtualKeyCode.VK_Z;
    }

    /// 한국어 레이아웃 키 처리. true 반환 시 HandleAction 스킵.
    private bool HandleKoreanLayoutKey(KeySlot slot)
    {
        if (slot.Action is SendKeyAction { Vk: "VK_HANGUL" })
        {
            _isKoreanInput = !_isKoreanInput;
            FinalizeKoreanComposition();
            return false;
        }

        // modifier(Ctrl/Alt/Win/Shift)가 활성 상태면 조합키이므로 자모 처리 스킵
        bool isComboKey = _inputService.HasActiveModifiers;

        if (!_isKoreanInput)
            return HandleEnglishSubMode(slot, isComboKey);

        // ── 한국어 입력 모드 ──────────────────────────────────────────────

        // 조합키면 자모 처리하지 않고 VK 코드 전송
        if (isComboKey)
        {
            if (_inputService.Mode == InputMode.Unicode && _inputService.TrackedOnScreenLength > 0)
                FinalizeKoreanComposition();
            return false;
        }

        string? hangulJamo = GetHangulJamoFromSlot(slot);

        if (hangulJamo is not null)
        {
            if (_inputService.Mode == InputMode.Unicode)
            {
                int prevLen = _inputService.TrackedOnScreenLength;
                _autoComplete.OnHangulInput(hangulJamo);
                string newOutput = _autoComplete.CurrentWord;
                _inputService.SendAtomicReplace(prevLen, newOutput);
                return true;
            }
            else
            {
                _autoComplete.OnHangulInput(hangulJamo);
                return false;
            }
        }

        if (slot.Action is SendKeyAction { Vk: var vkStr }
            && Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
        {
            if (vk == VirtualKeyCode.VK_BACK)
            {
                if (_inputService.Mode == InputMode.Unicode && _inputService.TrackedOnScreenLength > 0)
                {
                    int prevLen = _inputService.TrackedOnScreenLength;
                    _autoComplete.OnHangulBackspace();
                    string newOutput = _autoComplete.CurrentWord;
                    _inputService.SendAtomicReplace(prevLen, newOutput);
                    return true;
                }
                else if (_autoComplete.CurrentWord.Length > 0)
                {
                    _autoComplete.OnHangulBackspace();
                }
                return false;
            }

            if (IsAutoCompleteSeparator(vk))
            {
                FinalizeKoreanComposition();
                return false;
            }

            if (_inputService.Mode == InputMode.Unicode && _inputService.TrackedOnScreenLength > 0)
                FinalizeKoreanComposition();
        }

        return false;
    }

    private bool HandleEnglishSubMode(KeySlot slot, bool isComboKey)
    {
        if (slot.Action is not SendKeyAction { Vk: var vkStr }
            || !Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
            return false;

        if (IsAutoCompleteSeparator(vk))
        {
            _autoComplete.CompleteCurrentWord();
            return false;
        }

        if (isComboKey || _inputService.Mode == InputMode.VirtualKey)
        {
            _autoComplete.OnKeyInput(vk);
            return false;
        }

        char ch = VkToEnglishChar(vk, ShowUpperCase);
        if (ch != '\0')
        {
            _autoComplete.OnKeyInput(vk);
            _inputService.SendUnicode(ch.ToString());
            return true;
        }
        return false;
    }

    private void HandleEnglishLayoutKey(KeySlot slot)
    {
        if (slot.Action is not SendKeyAction { Vk: var vkStr }
            || !Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
            return;

        if (IsAutoCompleteSeparator(vk))
        {
            _autoComplete.CompleteCurrentWord();
            return;
        }

        if (_inputService.HasActiveModifiers || _inputService.Mode == InputMode.VirtualKey)
        {
            _autoComplete.OnKeyInput(vk);
            return;
        }

        char ch = VkToEnglishChar(vk, ShowUpperCase);
        if (ch != '\0')
        {
            _autoComplete.OnKeyInput(vk);
            _inputService.SendUnicode(ch.ToString());
        }
    }

    private static char VkToEnglishChar(VirtualKeyCode vk, bool upperCase)
    {
        if (vk >= VirtualKeyCode.VK_A && vk <= VirtualKeyCode.VK_Z)
        {
            char c = (char)('a' + ((int)vk - (int)VirtualKeyCode.VK_A));
            return upperCase ? char.ToUpperInvariant(c) : c;
        }
        return '\0';
    }

    private void FinalizeKoreanComposition()
    {
        _autoComplete.CompleteCurrentWord();
        _inputService.TrackedOnScreenLength = 0;
    }

    private string? GetHangulJamoFromSlot(KeySlot slot)
    {
        if (ShowUpperCase && slot.ShiftLabel is { Length: 1 } && IsHangulJamo(slot.ShiftLabel))
            return slot.ShiftLabel;
        if (IsHangulJamo(slot.Label))
            return slot.Label;
        return null;
    }

    private static bool IsHangulJamo(string s) =>
        s.Length == 1 && (s[0] >= '\u3131' && s[0] <= '\u3163' || s[0] >= '\uAC00' && s[0] <= '\uD7A3');

    private static bool IsAutoCompleteSeparator(VirtualKeyCode vk) =>
        vk is VirtualKeyCode.VK_SPACE or VirtualKeyCode.VK_RETURN
            or VirtualKeyCode.VK_TAB or VirtualKeyCode.VK_OEM_PERIOD
            or VirtualKeyCode.VK_OEM_COMMA;

    // ── 내부 메서드 ──────────────────────────────────────────────────────────
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
            _isKoreanInput = imeKorean;
            ImeModeChanged?.Invoke(imeKorean);
            _autoComplete.CompleteCurrentWord();
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

        // T-4.7: 각 키 슬롯의 Sticky/Locked 상태 갱신
        foreach (var row in Rows)
        {
            foreach (var slotVm in row.Keys)
            {
                if (slotVm.StickyVk is { } vk)
                {
                    slotVm.IsSticky = _inputService.StickyKeys.Contains(vk);
                    slotVm.IsLocked = _inputService.LockedKeys.Contains(vk);
                }

                // T-7.3: CapsLock 키 슬롯 강조
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
