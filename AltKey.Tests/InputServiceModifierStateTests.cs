using AltKey.Models;
using AltKey.Platform;
using AltKey.Services;
using System.Diagnostics;
using TrackingInputService = AltKey.Tests.InputLanguage.TestSlotFactory.TrackingInputService;
using CapturingInputService = AltKey.Tests.InputLanguage.TestSlotFactory.CapturingInputService;

namespace AltKey.Tests;

/// <summary>
/// InputService의 모디파이어/FunctionLayer/HeldKey 상태 관리 테스트.
/// (Dispatch 경로와 HandleAction 프로세스 실행 테스트는 InputServiceDispatchTests.cs에 있다.)
/// </summary>
public class InputServiceModifierStateTests
{
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
    public void TrySetMode_clears_transient_input_state_but_keeps_locked_states()
    {
        var svc = new TrackingInputService();
        int resetRequestCount = 0;
        svc.InputModeGestureResetRequested += () => resetRequestCount++;

        svc.BeginHeldKey(VirtualKeyCode.VK_W);
        svc.ArmHeldKeyGesture(VirtualKeyCode.VK_D);
        svc.TrackedOnScreenLength = 2;
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        svc.ToggleFunctionLayer();
        svc.ToggleFunctionLayer();

        svc.TrySetMode(InputMode.VirtualKey);
        svc.HandleAction(new SendKeyAction(nameof(VirtualKeyCode.VK_D)));

        Assert.Equal(InputMode.VirtualKey, svc.Mode);
        Assert.Equal(1, resetRequestCount);
        Assert.Empty(svc.HeldKeys);
        Assert.Equal(0, svc.TrackedOnScreenLength);
        Assert.Contains(VirtualKeyCode.VK_W, svc.KeyUps);
        Assert.Contains(VirtualKeyCode.VK_CONTROL, svc.KeyUps);
        Assert.DoesNotContain(VirtualKeyCode.VK_D, svc.HeldKeys);
        Assert.Contains(VirtualKeyCode.VK_D, svc.KeyPresses);
        Assert.True(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.True(svc.LockedKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.False(svc.StickyKeys.Contains(VirtualKeyCode.VK_CONTROL));
        Assert.Equal(FunctionLayerState.Locked, svc.FunctionLayerState);
    }

    [Fact]
    public void TrySetMode_clears_one_shot_function_layer()
    {
        var svc = new TrackingInputService();

        svc.ToggleFunctionLayer();
        svc.TrySetMode(InputMode.VirtualKey);

        Assert.Equal(FunctionLayerState.Inactive, svc.FunctionLayerState);
    }

    [Fact]
    public void ToggleFunctionLayer_cycles_oneShot_locked_inactive()
    {
        var svc = new TrackingInputService();

        svc.ToggleFunctionLayer();
        Assert.Equal(FunctionLayerState.OneShot, svc.FunctionLayerState);

        svc.ToggleFunctionLayer();
        Assert.Equal(FunctionLayerState.Locked, svc.FunctionLayerState);

        svc.ToggleFunctionLayer();
        Assert.Equal(FunctionLayerState.Inactive, svc.FunctionLayerState);
    }

    [Fact]
    public void ConsumeFunctionLayerAfterAction_clears_only_oneShot()
    {
        var svc = new TrackingInputService();

        svc.ToggleFunctionLayer();
        svc.ConsumeFunctionLayerAfterAction();
        Assert.Equal(FunctionLayerState.Inactive, svc.FunctionLayerState);

        svc.ToggleFunctionLayer();
        svc.ToggleFunctionLayer();
        svc.ConsumeFunctionLayerAfterAction();
        Assert.Equal(FunctionLayerState.Locked, svc.FunctionLayerState);
    }

    [Fact]
    public void HandleAction_begins_held_key_once_when_gesture_is_armed()
    {
        var svc = new TrackingInputService();
        svc.TrySetMode(InputMode.VirtualKey);

        svc.ArmHeldKeyGesture(VirtualKeyCode.VK_W);
        svc.HandleAction(new SendKeyAction(nameof(VirtualKeyCode.VK_W)));
        svc.ArmHeldKeyGesture(VirtualKeyCode.VK_W);
        svc.HandleAction(new SendKeyAction(nameof(VirtualKeyCode.VK_W)));

        Assert.True(svc.IsHeldKey(VirtualKeyCode.VK_W));
        Assert.Single(svc.KeyDowns, vk => vk == VirtualKeyCode.VK_W);
        Assert.DoesNotContain(VirtualKeyCode.VK_W, svc.KeyUps);
    }

    [Fact]
    public void HandleAction_begins_held_key_once_when_gesture_is_armed_in_unicode_mode()
    {
        var svc = new TrackingInputService();
        svc.TrySetMode(InputMode.Unicode);

        svc.ArmHeldKeyGesture(VirtualKeyCode.VK_BACK);
        svc.HandleAction(new SendKeyAction(nameof(VirtualKeyCode.VK_BACK)));
        svc.ArmHeldKeyGesture(VirtualKeyCode.VK_BACK);
        svc.HandleAction(new SendKeyAction(nameof(VirtualKeyCode.VK_BACK)));

        Assert.True(svc.IsHeldKey(VirtualKeyCode.VK_BACK));
        Assert.Single(svc.KeyDowns, vk => vk == VirtualKeyCode.VK_BACK);
        Assert.DoesNotContain(VirtualKeyCode.VK_BACK, svc.KeyUps);
    }

    [Fact]
    public void EndHeldKey_releases_key_up_only_once()
    {
        var svc = new TrackingInputService();

        svc.BeginHeldKey(VirtualKeyCode.VK_A);
        svc.EndHeldKey(VirtualKeyCode.VK_A);
        svc.EndHeldKey(VirtualKeyCode.VK_A);

        Assert.False(svc.IsHeldKey(VirtualKeyCode.VK_A));
        Assert.Single(svc.KeyDowns, vk => vk == VirtualKeyCode.VK_A);
        Assert.Single(svc.KeyUps, vk => vk == VirtualKeyCode.VK_A);
    }

    [Fact]
    public void ReleaseAllHeldKeys_releases_every_active_key()
    {
        var svc = new TrackingInputService();

        svc.BeginHeldKey(VirtualKeyCode.VK_W);
        svc.BeginHeldKey(VirtualKeyCode.VK_D);
        svc.ReleaseAllHeldKeys("hide");

        Assert.Empty(svc.HeldKeys);
        Assert.Contains(VirtualKeyCode.VK_W, svc.KeyUps);
        Assert.Contains(VirtualKeyCode.VK_D, svc.KeyUps);
        Assert.Contains("hide", svc.ReleaseHeldReasons);
    }

    [Fact]
    public void Sticky_modifier_and_held_key_keep_separate_state()
    {
        var svc = new TrackingInputService();

        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.BeginHeldKey(VirtualKeyCode.VK_W);
        svc.ReleaseAllHeldKeys("hold-release");

        Assert.True(svc.StickyKeys.Contains(VirtualKeyCode.VK_SHIFT));
        Assert.DoesNotContain(VirtualKeyCode.VK_SHIFT, svc.KeyUps);
        Assert.Contains(VirtualKeyCode.VK_W, svc.KeyUps);
    }
}
