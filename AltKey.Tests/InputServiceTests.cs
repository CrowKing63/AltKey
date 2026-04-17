using AltKey.Models;
using AltKey.Services;

namespace AltKey.Tests;

public class InputServiceTests
{
    [Fact]
    public void HasActiveModifiersExcludingShift_false_when_only_shift_sticky()
    {
        var svc = new InputService();
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        Assert.True(svc.HasActiveModifiers);
        Assert.False(svc.HasActiveModifiersExcludingShift);
    }

    [Fact]
    public void HasActiveModifiersExcludingShift_true_when_ctrl_sticky()
    {
        var svc = new InputService();
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        Assert.True(svc.HasActiveModifiersExcludingShift);
    }

    [Fact]
    public void HasActiveModifiersExcludingShift_true_when_ctrl_and_shift_sticky()
    {
        var svc = new InputService();
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        Assert.True(svc.HasActiveModifiersExcludingShift);
    }

    [Fact]
    public void ToggleModifier_cycle_sticky_locked_released()
    {
        var svc = new InputService();

        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        Assert.True(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.False(svc.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT));

        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        Assert.True(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.True(svc.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT));

        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        Assert.False(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.False(svc.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT));
    }

    [Fact]
    public void IsElevated_returns_boolean()
    {
        var svc = new InputService();
        Assert.IsType<bool>(svc.IsElevated);
    }

    [Fact]
    public void TrySetMode_unicode_to_virtualKey()
    {
        var svc = new InputService();
        svc.TrySetMode(InputMode.VirtualKey);
        Assert.Equal(InputMode.VirtualKey, svc.Mode);
    }
}
