using AltKey.Models;
using AltKey.Services;

namespace AltKey.Tests;

public class InputServiceTests
{
    private sealed class TrackingInputService : InputService
    {
        public List<VirtualKeyCode> KeyDowns { get; } = [];
        public List<VirtualKeyCode> KeyUps { get; } = [];
        public List<string> ReleaseAllReasons { get; } = [];
        public List<string> ReleaseHighRiskReasons { get; } = [];

        public override void SendKeyDown(VirtualKeyCode vk) => KeyDowns.Add(vk);

        public override void SendKeyUp(VirtualKeyCode vk) => KeyUps.Add(vk);

        public override void ReleaseAllModifiers(string reason = "manual")
        {
            ReleaseAllReasons.Add(reason);
            base.ReleaseAllModifiers(reason);
        }

        public override void ReleaseHighRiskModifiers(string reason)
        {
            ReleaseHighRiskReasons.Add(reason);
            base.ReleaseHighRiskModifiers(reason);
        }
    }

    [Fact]
    public void HasActiveModifiersExcludingShift_false_when_only_shift_sticky()
    {
        var svc = new TrackingInputService();
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        Assert.True(svc.HasActiveModifiers);
        Assert.False(svc.HasActiveModifiersExcludingShift);
    }

    [Fact]
    public void HasActiveModifiersExcludingShift_true_when_ctrl_sticky()
    {
        var svc = new TrackingInputService();
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        Assert.True(svc.HasActiveModifiersExcludingShift);
    }

    [Fact]
    public void HasActiveModifiersExcludingShift_true_when_ctrl_and_shift_sticky()
    {
        var svc = new TrackingInputService();
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        Assert.True(svc.HasActiveModifiersExcludingShift);
    }

    [Fact]
    public void ToggleModifier_cycle_sticky_locked_released()
    {
        var svc = new TrackingInputService();

        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        Assert.True(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.False(svc.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT));

        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        Assert.True(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.True(svc.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT));

        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        Assert.False(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.False(svc.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.Contains(VirtualKeyCode.VK_SHIFT, svc.KeyDowns);
        Assert.Contains(VirtualKeyCode.VK_SHIFT, svc.KeyUps);
    }

    [Fact]
    public void ReleaseAllModifiers_clears_ctrl_sticky_state()
    {
        var svc = new TrackingInputService();

        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        svc.ReleaseAllModifiers("test");

        Assert.Empty(svc.StickyKeys);
        Assert.Empty(svc.LockedKeys);
        Assert.Contains(VirtualKeyCode.VK_CONTROL, svc.KeyUps);
        Assert.Contains("test", svc.ReleaseAllReasons);
    }

    [Fact]
    public void ReleaseHighRiskModifiers_keeps_shift_but_releases_ctrl()
    {
        var svc = new TrackingInputService();

        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        svc.ReleaseHighRiskModifiers("hide");

        Assert.True(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.False(svc.StickyKeys.Contains(VirtualKeyCode.VK_CONTROL));
        Assert.DoesNotContain(VirtualKeyCode.VK_SHIFT, svc.KeyUps);
        Assert.Contains(VirtualKeyCode.VK_CONTROL, svc.KeyUps);
        Assert.Contains("hide", svc.ReleaseHighRiskReasons);
    }

    [Fact]
    public void SendCombo_releases_transient_ctrl_sticky_after_combo()
    {
        var svc = new TrackingInputService();

        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        svc.SendCombo([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_V]);

        Assert.Empty(svc.StickyKeys);
        Assert.Contains(VirtualKeyCode.VK_V, svc.KeyDowns);
        Assert.True(svc.KeyUps.Count(vk => vk == VirtualKeyCode.VK_CONTROL) >= 2);
    }

    [Fact]
    public void IsElevated_returns_boolean()
    {
        var svc = new TrackingInputService();
        Assert.IsType<bool>(svc.IsElevated);
    }

    [Fact]
    public void TrySetMode_unicode_to_virtualKey()
    {
        var svc = new TrackingInputService();
        svc.TrySetMode(InputMode.VirtualKey);
        Assert.Equal(InputMode.VirtualKey, svc.Mode);
    }
}
