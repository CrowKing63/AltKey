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

    [Fact]
    public void Input_mode_reset_clears_suppressed_click()
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

                button.SuppressNextClickForTests();
                button.ResetTransientGestureStateForTests();
                button.TriggerClick();

                Assert.Equal(1, executionCount);
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

    /// <summary>
    /// 반복 입력 탭 뒤 WPF Click이 오지 않아 남은 클릭 억제 플래그가
    /// 새 제스처(반복 불가 정책)의 첫 클릭을 삼키지 않는지 검증한다.
    /// </summary>
    [Fact]
    public void Stale_suppress_flag_does_not_swallow_first_click_of_new_gesture()
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

                // 이전 반복 제스처가 Click 없이 끝나 억제 플래그가 남은 상태.
                button.SuppressNextClickForTests();

                // 새 제스처(누름 → Click): 첫 클릭이 반드시 실행되어야 한다.
                button.TriggerMouseDown();
                button.TriggerClick();
                Assert.Equal(1, executionCount);

                // 다음 제스처도 정상 실행되어야 한다.
                button.TriggerMouseDown();
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

        public void TriggerMouseDown()
        {
            var args = new System.Windows.Input.MouseButtonEventArgs(
                System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            {
                RoutedEvent = System.Windows.UIElement.PreviewMouseLeftButtonDownEvent
            };
            OnPreviewMouseLeftButtonDown(args);
        }
    }
}
