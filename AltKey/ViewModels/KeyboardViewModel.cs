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
        slot.Action is ToggleStickyAction ta &&
        Enum.TryParse<VirtualKeyCode>(ta.Vk, ignoreCase: true, out var vk)
            ? vk : null;

    public string GetLabel(bool upperCase) =>
        upperCase && slot.ShiftLabel is { } s ? s : slot.Label;
}

// ── KeyboardViewModel ───────────────────────────────────────────────────────

public partial class KeyboardViewModel : ObservableObject
{
    private readonly InputService _inputService;

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
    public KeyboardViewModel(InputService inputService)
    {
        _inputService = inputService;
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

    // ── 커맨드 ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void KeyPressed(KeySlot slot)
    {
        if (slot.Action is not null)
            _inputService.HandleAction(slot.Action);

        UpdateModifierState();
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
            }
        }
    }

    // T-4.10: 창 너비 변경 시 KeyUnit 재계산
    public void OnWindowSizeChanged(double newWidth)
    {
        const double minUnit  = 32.0;
        const double maxUnit  = 64.0;
        const double baseWidth = 900.0;
        const double baseUnit  = 48.0;

        var unit = baseUnit * (newWidth / baseWidth);
        KeyUnit = Math.Clamp(unit, minUnit, maxUnit);
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
