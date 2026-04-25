using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AltKey.Models;
using AltKey.Services;
using AltKey.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Controls;

/// <summary>
/// [역할] 가상 키보드의 개별 버튼 하나를 나타내는 커스텀 컨트롤입니다.
/// [기능] 키 라벨 표시, 크기 조절, 체류 클릭(Dwell), 키 반복 입력(Repeat), 접근성 탐색 등을 담당합니다.
/// </summary>
public class KeyButton : System.Windows.Controls.Button
{
    static KeyButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(KeyButton),
            new FrameworkPropertyMetadata(typeof(KeyButton)));
    }

    // ── Dependency Properties (WPF UI와 데이터 연결용) ────────────────────────

    // 이 버튼이 나타내는 키의 데이터(글자, 크기 등)가 들어있는 객체입니다.
    public static readonly DependencyProperty SlotProperty =
        DependencyProperty.Register(
            nameof(Slot), typeof(KeySlotVm), typeof(KeyButton),
            new PropertyMetadata(null, OnSlotChanged));

    // Shift 키나 Caps Lock이 켜져서 대문자를 보여줘야 하는지 여부입니다.
    public static readonly DependencyProperty ShowUpperCaseProperty =
        DependencyProperty.Register(
            nameof(ShowUpperCase), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false, OnShowUpperCaseChanged));

    // 키 우측 상단에 작게 표시되는 보조 라벨(예: 한글 입력 중의 영어 라벨)입니다.
    public static readonly DependencyProperty SubLabelProperty =
        DependencyProperty.Register(
            nameof(SubLabel), typeof(string), typeof(KeyButton),
            new PropertyMetadata(""));

    // 버튼 정중앙에 크게 표시되는 주 라벨입니다.
    public static readonly DependencyProperty DisplayLabelProperty =
        DependencyProperty.Register(
            nameof(DisplayLabel), typeof(string), typeof(KeyButton),
            new PropertyMetadata("", OnDisplayLabelChanged));

    // 현재 모드에서 사용할 수 없는 키를 흐리게 표시할지 여부입니다.
    public static readonly DependencyProperty IsDimmedProperty =
        DependencyProperty.Register(
            nameof(IsDimmed), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false, OnIsDimmedChanged));

    // Shift/Ctrl 등이 '한 번만 눌린 고정 상태'인지 나타냅니다.
    public static readonly DependencyProperty IsStickyProperty =
        DependencyProperty.Register(
            nameof(IsSticky), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    // Caps Lock 처럼 '영구적으로 고정된 상태'인지 나타냅니다.
    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(
            nameof(IsLocked), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    // 버튼의 크기 기준(픽셀)입니다. 이 값이 커지면 버튼이 커집니다.
    public static readonly DependencyProperty KeyUnitProperty =
        DependencyProperty.Register(
            nameof(KeyUnit), typeof(double), typeof(KeyButton),
            new PropertyMetadata(48.0, OnKeyUnitChanged));

    // [접근성] 마우스를 올리고만 있어도 클릭되는 기능의 활성화 여부입니다.
    public static readonly DependencyProperty DwellEnabledProperty =
        DependencyProperty.Register(
            nameof(DwellEnabled), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    // 체류 클릭이 발동되기까지 기다리는 시간(밀리초)입니다.
    public static readonly DependencyProperty DwellTimeProperty =
        DependencyProperty.Register(
            nameof(DwellTime), typeof(int), typeof(KeyButton),
            new PropertyMetadata(800));

    // 체류 클릭 진행도 (0.0 ~ 1.0). 버튼 테두리의 게이지 애니메이션에 사용됩니다.
    public static readonly DependencyProperty DwellProgressProperty =
        DependencyProperty.Register(
            nameof(DwellProgress), typeof(double), typeof(KeyButton),
            new PropertyMetadata(0.0));

    // 키를 꾹 누르고 있을 때 연속 입력이 되는 기능의 활성화 여부입니다.
    public static readonly DependencyProperty KeyRepeatEnabledProperty =
        DependencyProperty.Register(
            nameof(KeyRepeatEnabled), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    // 연속 입력이 시작되기 전까지의 대기 시간(밀리초)입니다.
    public static readonly DependencyProperty KeyRepeatDelayMsProperty =
        DependencyProperty.Register(
            nameof(KeyRepeatDelayMs), typeof(int), typeof(KeyButton),
            new PropertyMetadata(300));

    // 연속 입력이 반복되는 간격(밀리초)입니다. 작을수록 빠르게 입력됩니다.
    public static readonly DependencyProperty KeyRepeatIntervalMsProperty =
        DependencyProperty.Register(
            nameof(KeyRepeatIntervalMs), typeof(int), typeof(KeyButton),
            new PropertyMetadata(50));

    // [접근성] 탭(Tab) 키로 버튼 사이를 이동하며 조작할 수 있는지 여부입니다.
    public static readonly DependencyProperty KeyboardA11yNavigationEnabledProperty =
        DependencyProperty.Register(
            nameof(KeyboardA11yNavigationEnabled), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false, OnKeyboardA11yNavigationEnabledChanged));

    // 접근성 모드에서 현재 이 버튼이 선택(포커스)되어 있는지 나타냅니다.
    public static readonly DependencyProperty IsA11yFocusedProperty =
        DependencyProperty.Register(
            nameof(IsA11yFocused), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    // [접근성][L2] 애니메이션 최소화 모드 활성화 여부입니다. true이면 Hover/Pressed 확대 효과가 즉시 적용됩니다.
    public static readonly DependencyProperty ReducedMotionEnabledProperty =
        DependencyProperty.Register(
            nameof(ReducedMotionEnabled), typeof(bool), typeof(KeyButton),
            new PropertyMetadata(false));

    // [접근성][L2] 마우스를 올렸을 때 TTS로 라벨을 읽어줄지 여부입니다.
    public static readonly DependencyProperty TtsOnHoverProperty =
        DependencyProperty.Register(
            nameof(TtsOnHover), typeof(bool), typeof(KeyButton),
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

    public bool ReducedMotionEnabled
    {
        get => (bool)GetValue(ReducedMotionEnabledProperty);
        set => SetValue(ReducedMotionEnabledProperty, value);
    }

    public bool TtsOnHover
    {
        get => (bool)GetValue(TtsOnHoverProperty);
        set => SetValue(TtsOnHoverProperty, value);
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

        // L2: 마우스 올렸을 때 TTS 읽기 (설정 ON인 경우에만)
        if (TtsOnHover && !string.IsNullOrWhiteSpace(DisplayLabel))
        {
            try
            {
                App.Services?.GetService<AccessibilityService>()?.SpeakLabel(DisplayLabel);
            }
            catch
            {
                // TTS 실패는 무시
            }
        }

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

    /// <summary>
    /// 버튼을 실제로 '누르는' 처리를 수행합니다. 
    /// 연결된 명령(Command)을 실행하여 텍스트를 입력하거나 기능을 동작시킵니다.
    /// </summary>
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
        // XAML 바인딩(DisplayLabel, SubLabel)이 끊어지지 않도록 SetCurrentValue 사용
        SetCurrentValue(DisplayLabelProperty, Slot.DisplayLabel);
        SetCurrentValue(SubLabelProperty, Slot.GetSubLabel(ShowUpperCase));
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

    /// <summary>
    /// 버튼의 가로/세로 크기를 계산된 KeyUnit에 맞춰 실시간으로 변경합니다.
    /// </summary>
    private void UpdateSize()
    {
        if (Slot is null) return;
        Width  = Slot.Width  * KeyUnit;
        Height = Slot.Height * KeyUnit;
    }
}
