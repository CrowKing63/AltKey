using System.Windows;
using System.Windows.Controls;
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
