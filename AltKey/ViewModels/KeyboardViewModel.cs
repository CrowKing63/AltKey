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

    // T-2.7: 100ms 주기 폴링으로 CapsLock 상태 동기화
    private readonly DispatcherTimer _capsLockTimer;

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
        _capsLockTimer.Tick += (_, _) => UpdateModifierState();
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

    }

    /// 키가 눌릴 때 발생하는 이벤트 (패널 자동 닫기 등 외부 연동용)
    public event Action? KeyTapped;

    // ── 커맨드 ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void KeyPressed(KeySlot slot)
    {
        // T-8.2: 키 클릭 사운드 재생
        _soundService.Play();

        // 한글 자모 자동 완성 추적
        bool isUpperCase = ShowUpperCase;
        string? hangulJamo = isUpperCase && slot.HangulShiftLabel is { Length: > 0 } hs
            ? hs : slot.HangulLabel;
        if (hangulJamo is { Length: > 0 } && _configService.Current.KoreanAutoCompleteEnabled)
            _autoComplete.OnHangulInput(hangulJamo);

        if (slot.Action is not null)
            _inputService.HandleAction(slot.Action);

        UpdateModifierState();
        KeyTapped?.Invoke();
    }

    // ── 내부 메서드 ──────────────────────────────────────────────────────────
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
