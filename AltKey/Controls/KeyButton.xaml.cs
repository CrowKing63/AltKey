using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
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

    // ── 체류 클릭 타이머 ─────────────────────────────────────────────────────

    private DispatcherTimer? _dwellTimer;
    private DateTime         _dwellStart;

    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (!DwellEnabled) return;

        _dwellStart  = DateTime.UtcNow;
        _dwellTimer  = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _dwellTimer.Tick += DwellTick;
        _dwellTimer.Start();
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        CancelDwell();
    }

    private void DwellTick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _dwellStart).TotalMilliseconds;
        DwellProgress = elapsed / DwellTime; // 0.0 ~ 1.0

        if (elapsed >= DwellTime)
        {
            CancelDwell();
            RaiseEvent(new RoutedEventArgs(ClickEvent)); // 클릭 이벤트 발생
        }
    }

    private void CancelDwell()
    {
        _dwellTimer?.Stop();
        _dwellTimer  = null;
        DwellProgress = 0;
    }

    // ── 변경 콜백 ────────────────────────────────────────────────────────────

    private static void OnSlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not KeyButton kb || e.NewValue is not KeySlotVm slot) return;
        kb.UpdateSize();
        kb.Content = slot.GetLabel(kb.ShowUpperCase);
        ToolTipService.SetToolTip(kb, slot.Slot.StyleKey is { Length: > 0 } sk ? sk : null);
    }

    private static void OnShowUpperCaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyButton kb && kb.Slot is { } slot)
            kb.Content = slot.GetLabel((bool)e.NewValue);
    }

    private static void OnKeyUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyButton kb)
            kb.UpdateSize();
    }

    private void UpdateSize()
    {
        if (Slot is null) return;
        Width  = Slot.Width  * KeyUnit;
        Height = Slot.Height * KeyUnit;
    }
}
