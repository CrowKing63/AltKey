using AltKey.Controls;
using AltKey.Models;
using AltKey.Services;
using CommunityToolkit.Mvvm.Input;
using System.Threading;

namespace AltKey.Tests;

public class KeyButtonRepeatPolicyTests
{
    [Fact]
    public void Unicode_letter_key_repeat_is_disabled()
    {
        var action = new SendKeyAction(nameof(VirtualKeyCode.VK_A));

        Assert.Equal("Disabled", KeyButton.DescribeRepeatPolicyForTests(InputMode.Unicode, action));
        Assert.Null(KeyButton.GetHoldableVirtualKeyForTests(InputMode.Unicode, action));
    }

    [Fact]
    public void Unicode_backspace_repeat_uses_held_virtual_key()
    {
        var action = new SendKeyAction(nameof(VirtualKeyCode.VK_BACK));

        Assert.Equal("HeldVirtualKey", KeyButton.DescribeRepeatPolicyForTests(InputMode.Unicode, action));
        Assert.Equal(VirtualKeyCode.VK_BACK, KeyButton.GetHoldableVirtualKeyForTests(InputMode.Unicode, action));
        Assert.True(KeyButton.ShouldCancelCompositionOnRepeatStartForTests(InputMode.Unicode, action));
    }

    [Fact]
    public void Unicode_arrow_repeat_keeps_composition_and_uses_hold()
    {
        var action = new SendKeyAction(nameof(VirtualKeyCode.VK_LEFT));

        Assert.Equal("HeldVirtualKey", KeyButton.DescribeRepeatPolicyForTests(InputMode.Unicode, action));
        Assert.Equal(VirtualKeyCode.VK_LEFT, KeyButton.GetHoldableVirtualKeyForTests(InputMode.Unicode, action));
        Assert.False(KeyButton.ShouldCancelCompositionOnRepeatStartForTests(InputMode.Unicode, action));
    }

    [Fact]
    public void Unicode_space_repeat_is_disabled_in_first_stage()
    {
        var action = new SendKeyAction(nameof(VirtualKeyCode.VK_SPACE));

        Assert.Equal("Disabled", KeyButton.DescribeRepeatPolicyForTests(InputMode.Unicode, action));
        Assert.Null(KeyButton.GetHoldableVirtualKeyForTests(InputMode.Unicode, action));
    }

    [Fact]
    public void Virtual_key_mode_keeps_legacy_repeat_policy()
    {
        var action = new SendKeyAction(nameof(VirtualKeyCode.VK_A));

        Assert.Equal("LegacyRepeat", KeyButton.DescribeRepeatPolicyForTests(InputMode.VirtualKey, action));
        Assert.Equal(VirtualKeyCode.VK_A, KeyButton.GetHoldableVirtualKeyForTests(InputMode.VirtualKey, action));
        Assert.False(KeyButton.ShouldCancelCompositionOnRepeatStartForTests(InputMode.VirtualKey, action));
    }

    [Fact]
    public void Repeat_path_suppresses_next_wpf_click_once()
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                int executionCount = 0;
                var button = new TestKeyButton
                {
                    Command = new RelayCommand(() => executionCount++)
                };

                button.TriggerClick();
                button.SuppressNextClickForTests();
                button.TriggerClick();
                button.TriggerClick();

                Assert.Equal(2, executionCount);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
            throw captured;
    }

    private sealed class TestKeyButton : KeyButton
    {
        public void TriggerClick() => base.OnClick();
    }
}
