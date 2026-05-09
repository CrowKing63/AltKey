using AltKey.Models;
using AltKey.Services;
using System.Diagnostics;

namespace AltKey.Tests;

public class InputServiceTests
{
    private sealed class TrackingInputService : InputService
    {
        public List<VirtualKeyCode> KeyDowns { get; } = [];
        public List<VirtualKeyCode> KeyUps { get; } = [];
        public List<string> ReleaseAllReasons { get; } = [];
        public List<string> ReleaseHighRiskReasons { get; } = [];
        public List<string> ReleaseHeldReasons { get; } = [];
        public ProcessStartInfo? LastStartedProcess { get; private set; }

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

        public override void ReleaseAllHeldKeys(string reason = "manual")
        {
            ReleaseHeldReasons.Add(reason);
            base.ReleaseAllHeldKeys(reason);
        }

        protected override void StartProcess(ProcessStartInfo psi)
        {
            LastStartedProcess = psi;
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

    [Fact]
    public void HandleAction_shell_command_preserves_literal_quotes_for_powershell()
    {
        var svc = new TrackingInputService();
        const string command = "Write-Output \"C:\\Temp\\a\\b\"";

        svc.HandleAction(new ShellCommandAction(command, "powershell"));

        Assert.NotNull(svc.LastStartedProcess);
        Assert.Equal("powershell.exe", svc.LastStartedProcess!.FileName);
        Assert.Equal("-NoProfile", svc.LastStartedProcess.ArgumentList[0]);
        Assert.Equal("-Command", svc.LastStartedProcess.ArgumentList[1]);
        Assert.Equal(command, svc.LastStartedProcess.ArgumentList[2]);
    }

    [Fact]
    public void HandleAction_shell_command_preserves_literal_quotes_for_cmd()
    {
        var svc = new TrackingInputService();
        const string command = "echo \"C:\\Temp\\a\\b\" && dir /b";

        svc.HandleAction(new ShellCommandAction(command, "cmd"));

        Assert.NotNull(svc.LastStartedProcess);
        Assert.Equal("cmd.exe", svc.LastStartedProcess!.FileName);
        Assert.Equal("/d", svc.LastStartedProcess.ArgumentList[0]);
        Assert.Equal("/s", svc.LastStartedProcess.ArgumentList[1]);
        Assert.Equal("/c", svc.LastStartedProcess.ArgumentList[2]);
        Assert.Equal(command, svc.LastStartedProcess.ArgumentList[3]);
    }

    [Fact]
    public void HandleAction_run_app_keeps_user_argument_string_as_is()
    {
        var svc = new TrackingInputService();
        const string path = "notepad.exe";
        const string args = "\"C:\\Temp\\memo \\\"draft\\\".txt\" /A";

        svc.HandleAction(new RunAppAction(path, args));

        Assert.NotNull(svc.LastStartedProcess);
        Assert.Equal(path, svc.LastStartedProcess!.FileName);
        Assert.Equal(args, svc.LastStartedProcess.Arguments);
        Assert.True(svc.LastStartedProcess.UseShellExecute);
    }
}
