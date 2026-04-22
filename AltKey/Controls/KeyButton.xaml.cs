using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AltKey.Models;
using AltKey.ViewModels;

namespace AltKey.Controls;

public class KeyButton : System.Windows.Controls.Button
{
    static KeyButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KeyButton),
            new FrameworkPropertyMetadata(typeof(KeyButton)));
    }

    // ── Dependency Properties ────────────────────────────────────────────────

    public static readonly DependencyProperty SlotProperty =
        DependencyProperty.Register(
            nameof(Slot), typeof(KeySlotVm), typeof(KeyButton),
            new PropertyMetadata(null, OnSlotChanged));

    public static readonly DependencyProperty ShowUpperCaseProperty =
        DependencyProperty.Register(
            nameof(ShowUpperCase), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false, OnShowUpperCaseChanged));

    // 한글 서브 레이블 (통합 레이아웃: 키 우상단에 항상 표시)
    public static readonly DependencyProperty SubLabelProperty =
        DependencyProperty.Register(
            nameof(SubLabel), typeof(string), typeof(KeyButton),
            new PropertyMetadata(""));

    // DisplayLabel — KeySlotVm.DisplayLabel 바인딩용
    public static readonly DependencyProperty DisplayLabelProperty =
        DependencyProperty.Register(
            nameof(DisplayLabel), typeof(string), typeof(KeyButton),
            new PropertyMetadata("", OnDisplayLabelChanged));

    // IsDimmed — QuietEnglish에서 영어 라벨 없는 키 흐림 표시
    public static readonly DependencyProperty IsDimmedProperty =
        DependencyProperty.Register(
            nameof(IsDimmed), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false, OnIsDimmedChanged));

    // T-4.7: Sticky / Locked 상태 DependencyProperty
    public static readonly DependencyProperty IsStickyProperty =
        DependencyProperty.Register(
            nameof(IsSticky), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(
            nameof(IsLocked), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    public static readonly DependencyProperty KeyUnitProperty =
        DependencyProperty.Register(
            nameof(KeyUnit), typeof(double), typeof(KeyButton),
            new PropertyMetadata(48.0, OnKeyUnitChanged));

    // T-5.1: 체류 클릭 DependencyProperties
    public static readonly DependencyProperty DwellEnabledProperty =
        DependencyProperty.Register(
            nameof(DwellEnabled), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    public static readonly DependencyProperty DwellTimeProperty =
        DependencyProperty.Register(
            nameof(DwellTime), typeof(int), typeof(KeyButton),
            new PropertyMetadata(800));

    // T-5.1/5.2: 체류 진행도 (0.0~1.0) — ControlTemplate 바인딩용
    public static readonly DependencyProperty DwellProgressProperty =
        DependencyProperty.Register(
            nameof(DwellProgress), typeof(double), typeof(KeyButton),
            new PropertyMetadata(0.0));

// T-10: 키 반복 입력 DependencyProperties
    public static readonly DependencyProperty KeyRepeatEnabledProperty =
        DependencyProperty.Register(
            nameof(KeyRepeatEnabled), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    public static readonly DependencyProperty KeyRepeatDelayMsProperty =
        DependencyProperty.Register(
            nameof(KeyRepeatDelayMs), typeof(int), typeof(KeyButton),
            new PropertyMetadata(300));

    public static readonly DependencyProperty KeyRepeatIntervalMsProperty =
        DependencyProperty.Register(
            nameof(KeyRepeatIntervalMs), typeof(int), typeof(KeyButton),
            new PropertyMetadata(50));

    // L1: 포커스 가시화 + 탭 탐색 모드
    public static readonly DependencyProperty KeyboardA11yNavigationEnabledProperty =
        DependencyProperty.Register(
            nameof(KeyboardA11yNavigationEnabled), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false, OnKeyboardA11yNavigationEnabledChanged));

    public static readonly DependencyProperty IsA11yFocusedProperty =
        DependencyProperty.Register(
            nameof(IsA11yFocused), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    // ── Properties ──────────────────────────────────────────────────────────

    public KeySlotVm? Slot
    {
        get => (KeySlotVm?)GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }

    public bool ShowUpperCase
    {
        get => (bool)GetValue(ShowUpperCaseProperty);
        set => SetValue(ShowUpperCaseProperty, value);
    }

    public string SubLabel
    {
        get => (string)GetValue(SubLabelProperty);
        set => SetValue(SubLabelProperty, value);
    }

    public string DisplayLabel
    {
        get => (string)GetValue(DisplayLabelProperty);
        set => SetValue(DisplayLabelProperty, value);
    }

    public bool IsDimmed
    {
        get => (bool)GetValue(IsDimmedProperty);
        set => SetValue(IsDimmedProperty, value);
    }

    public bool IsSticky
    {
        get => (bool)GetValue(IsStickyProperty);
        set => SetValue(IsStickyProperty, value);
    }

    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    public double KeyUnit
    {
        get => (double)GetValue(KeyUnitProperty);
        set => SetValue(KeyUnitProperty, value);
    }

    public bool DwellEnabled
    {
        get => (bool)GetValue(DwellEnabledProperty);
        set => SetValue(DwellEnabledProperty, value);
    }

    public int DwellTime
    {
        get => (int)GetValue(DwellTimeProperty);
        set => SetValue(DwellTimeProperty, value);
    }

    public double DwellProgress
    {
        get => (double)GetValue(DwellProgressProperty);
        private set => SetValue(DwellProgressProperty, value);
    }

    public bool KeyRepeatEnabled
    {
        get => (bool)GetValue(KeyRepeatEnabledProperty);
        set => SetValue(KeyRepeatEnabledProperty, value);
    }

    public int KeyRepeatDelayMs
    {
        get => (int)GetValue(KeyRepeatDelayMsProperty);
        set => SetValue(KeyRepeatDelayMsProperty, value);
    }

    public int KeyRepeatIntervalMs
    {
        get => (int)GetValue(KeyRepeatIntervalMsProperty);
        set => SetValue(KeyRepeatIntervalMsProperty, value);
    }

    public bool KeyboardA11yNavigationEnabled
    {
        get => (bool)GetValue(KeyboardA11yNavigationEnabledProperty);
        set => SetValue(KeyboardA11yNavigationEnabledProperty, value);
    }

    public bool IsA11yFocused
    {
        get => (bool)GetValue(IsA11yFocusedProperty);
        set => SetValue(IsA11yFocusedProperty, value);
    }

    // ── 체류 클릭 타이머 ─────────────────────────────────────────────────────

    private DispatcherTimer? _dwellTimer;
    private DateTime         _dwellStart;

    // T-10: 키 반복 입력 타이머
    private DispatcherTimer? _repeatDelayTimer;
    private DispatcherTimer? _repeatTimer;
    private bool _isRepeating;

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        System.Diagnostics.Debug.WriteLine($"[KeyButton] OnMouseEnter - DwellEnabled={DwellEnabled}, Slot={Slot?.Slot.Label}");
        if (!DwellEnabled) return;

        _dwellStart  = DateTime.UtcNow;
        _dwellTimer  = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _dwellTimer.Tick += DwellTick;
        _dwellTimer.Start();
        System.Diagnostics.Debug.WriteLine($"[KeyButton] Dwell timer started");
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        CancelDwell();
        CancelRepeat();
    }

    protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DwellEnabled)
        {
            e.Handled = false;
            return;
        }

        if (CanStartRepeat())
        {
            e.Handled = true;
            CancelRepeat();
            _isRepeating = false;
            ExecuteKeyPress();

            _repeatDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, KeyRepeatDelayMs)) };
            _repeatDelayTimer.Tick += RepeatDelayTick;
            _repeatDelayTimer.Start();
        }
        else
        {
            e.Handled = false;
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DwellEnabled)
        {
            e.Handled = false;
            return;
        }

        if (CanStartRepeat())
        {
            e.Handled = true;
            CancelRepeat();
        }
        else
        {
            e.Handled = false;
        }
    }

    private void RepeatDelayTick(object? sender, EventArgs e)
    {
        if (_repeatDelayTimer is not null)
        {
            _repeatDelayTimer.Tick -= RepeatDelayTick;
            _repeatDelayTimer.Stop();
        }
        _repeatDelayTimer = null;

        if (!CanStartRepeat()) return;

        System.Diagnostics.Debug.WriteLine($"[KeyButton] Repeat started");
        _isRepeating = true;

        _repeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1, KeyRepeatIntervalMs)) };
        _repeatTimer.Tick += RepeatTick;
        _repeatTimer.Start();
    }

    private void RepeatTick(object? sender, EventArgs e)
    {
        if (_isRepeating)
        {
            ExecuteKeyPress();
        }
    }

    private void ExecuteKeyPress()
    {
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }

    private void CancelRepeat()
    {
        _isRepeating = false;
        if (_repeatDelayTimer is not null)
        {
            _repeatDelayTimer.Tick -= RepeatDelayTick;
            _repeatDelayTimer.Stop();
        }
        _repeatDelayTimer = null;
        if (_repeatTimer is not null)
        {
            _repeatTimer.Tick -= RepeatTick;
            _repeatTimer.Stop();
        }
        _repeatTimer = null;
    }

    private bool CanStartRepeat()
    {
        if (!KeyRepeatEnabled)
            return false;

        if (Slot?.Slot.Action is not SendKeyAction { Vk: var vkText })
            return false;

        if (!Enum.TryParse<VirtualKeyCode>(vkText, ignoreCase: true, out var vk))
            return true;

        // Sticky 키로 쓸 수 있는 modifier들은 홀드 반복을 허용하지 않는다.
        return !vk.IsModifier();
    }

    private void DwellTick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _dwellStart).TotalMilliseconds;
        DwellProgress = elapsed / DwellTime; // 0.0 ~ 1.0

        if (elapsed >= 100) // 100ms마다 로그
            System.Diagnostics.Debug.WriteLine($"[KeyButton] Dwell progress: {DwellProgress:P0}");

        if (elapsed >= DwellTime)
        {
            System.Diagnostics.Debug.WriteLine($"[KeyButton] DWELL CLICK - Slot={Slot?.Slot.Label}");
            CancelDwell();
            
            // Command 직접 실행 (RaiseEvent는 Command를 트리거하지 않음)
            if (Command?.CanExecute(CommandParameter) == true)
            {
                System.Diagnostics.Debug.WriteLine($"[KeyButton] Executing command...");
                Command.Execute(CommandParameter);
            }
        }
    }

    private void CancelDwell()
    {
        _dwellTimer?.Stop();
        _dwellTimer  = null;
        DwellProgress = 0;
        CancelRepeat();
    }

    // ── 변경 콜백 ────────────────────────────────────────────────────────────

    private static void OnSlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not KeyButton kb || e.NewValue is not KeySlotVm slot) return;
        kb.UpdateSize();
        kb.UpdateLabel();
        ToolTipService.SetToolTip(kb, slot.Slot.StyleKey is { Length: > 0 } sk ? sk : null);
    }

    private static void OnShowUpperCaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyButton kb)
            kb.UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (Slot is null) return;
        Slot.RefreshDisplay();
        Content = Slot.DisplayLabel;
        SubLabel = Slot.GetSubLabel(ShowUpperCase);
    }

    private static void OnDisplayLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyButton kb)
            kb.Content = e.NewValue as string;
    }

    private static void OnIsDimmedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyButton kb)
            kb.IsEnabled = !(bool)e.NewValue;
    }

    private static void OnKeyUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyButton kb)
            kb.UpdateSize();
    }

    private static void OnKeyboardA11yNavigationEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyButton kb)
        {
            var enabled = (bool)e.NewValue;
            kb.Focusable = enabled;
            kb.IsTabStop = enabled;
        }
    }

    private void UpdateSize()
    {
        if (Slot is null) return;
        Width  = Slot.Width  * KeyUnit;
        Height = Slot.Height * KeyUnit;
    }
}
