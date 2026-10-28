using AltKey.Models;
using AltKey.Platform;
using AltKey.Services;
using System.Diagnostics;
using TrackingInputService = AltKey.Tests.InputLanguage.TestSlotFactory.TrackingInputService;
using CapturingInputService = AltKey.Tests.InputLanguage.TestSlotFactory.CapturingInputService;

namespace AltKey.Tests;

/// <summary>
/// InputService의 Dispatch 경로(스캔코드/유니코드)와 HandleAction 프로세스 실행 테스트.
/// (모디파이어/FunctionLayer/HeldKey 상태 테스트는 InputServiceModifierStateTests.cs에 있다.)
/// </summary>
public class InputServiceDispatchTests
{
    [Theory]
    [InlineData(VirtualKeyCode.VK_W)]
    [InlineData(VirtualKeyCode.VK_C)]
    public void SendKeyPress_uses_scan_code_by_default(VirtualKeyCode vk)
    {
        var svc = new CapturingInputService();

        svc.SendKeyPress(vk);

        var inputs = svc.LastDispatch;
        Assert.Equal(0, inputs[0].U.Ki.WVk);
        Assert.NotEqual(0, inputs[0].U.Ki.WScan);
        Assert.Equal(Win32.KEYEVENTF_SCANCODE, inputs[0].U.Ki.DwFlags);
        Assert.Equal(0, inputs[1].U.Ki.WVk);
        Assert.Equal(inputs[0].U.Ki.WScan, inputs[1].U.Ki.WScan);
        Assert.Equal(Win32.KEYEVENTF_SCANCODE | Win32.KEYEVENTF_KEYUP, inputs[1].U.Ki.DwFlags);
    }

    [Fact]
    public void SendKeyPress_marks_arrow_keys_as_extended_scan_codes()
    {
        var svc = new CapturingInputService();

        svc.SendKeyPress(VirtualKeyCode.VK_UP);

        var inputs = svc.LastDispatch;
        Assert.Equal(0, inputs[0].U.Ki.WVk);
        Assert.NotEqual(0, inputs[0].U.Ki.WScan);
        Assert.True((inputs[0].U.Ki.DwFlags & Win32.KEYEVENTF_SCANCODE) != 0);
        Assert.True((inputs[0].U.Ki.DwFlags & Win32.KEYEVENTF_EXTENDEDKEY) != 0);
        Assert.True((inputs[1].U.Ki.DwFlags & Win32.KEYEVENTF_KEYUP) != 0);
        Assert.True((inputs[1].U.Ki.DwFlags & Win32.KEYEVENTF_EXTENDEDKEY) != 0);
    }

    [Fact]
    public void SendKeyPress_keeps_ime_and_media_keys_on_virtual_key_path()
    {
        var svc = new CapturingInputService();

        svc.SendKeyPress(VirtualKeyCode.VK_HANGUL);
        svc.SendKeyPress((VirtualKeyCode)0xAF);

        Assert.Equal((ushort)VirtualKeyCode.VK_HANGUL, svc.Dispatches[0][0].U.Ki.WVk);
        Assert.Equal(0, svc.Dispatches[0][0].U.Ki.WScan);
        Assert.Equal(0xAF, svc.Dispatches[1][0].U.Ki.WVk);
        Assert.Equal(0, svc.Dispatches[1][0].U.Ki.WScan);
    }

    [Fact]
    public void SendUnicode_and_atomic_replace_keep_unicode_path()
    {
        var svc = new CapturingInputService();

        svc.SendUnicode("가");
        svc.SendAtomicReplace(1, "나");

        var unicodeInputs = svc.Dispatches[0];
        Assert.Equal(0, unicodeInputs[0].U.Ki.WVk);
        Assert.Equal(Win32.KEYEVENTF_UNICODE, unicodeInputs[0].U.Ki.DwFlags);
        Assert.Equal(Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP, unicodeInputs[1].U.Ki.DwFlags);

        // 백스페이스 배치가 Unicode 문자 배치와 분리되어 먼저 전송되는지 확인한다.
        var backspaceInputs = svc.Dispatches[1];
        Assert.Equal(2, backspaceInputs.Length);
        Assert.Equal((ushort)VirtualKeyCode.VK_BACK, backspaceInputs[0].U.Ki.WVk);
        Assert.Equal(0, backspaceInputs[0].U.Ki.WScan);
        Assert.True((backspaceInputs[1].U.Ki.DwFlags & Win32.KEYEVENTF_KEYUP) != 0);

        var replaceCharInputs = svc.Dispatches[2];
        Assert.Equal(0, replaceCharInputs[0].U.Ki.WVk);
        Assert.Equal(Win32.KEYEVENTF_UNICODE, replaceCharInputs[0].U.Ki.DwFlags);
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
    public void HandleAction_run_app_keeps_user_argument_string_as_is()
    {
        var svc = new TrackingInputService();
        const string path = "notepad.exe";
        const string args = "\"C:\\Temp\\memo \"draft\".txt\" /A";

        svc.HandleAction(new RunAppAction(path, args));

        Assert.NotNull(svc.LastStartedProcess);
        Assert.Equal(path, svc.LastStartedProcess!.FileName);
        Assert.Equal(args, svc.LastStartedProcess.Arguments);
        Assert.True(svc.LastStartedProcess.UseShellExecute);
    }

    [Fact]
    public void HandleAction_task_manager_combo_uses_direct_launch_instead_of_sendinput()
    {
        var svc = new TrackingInputService();

        svc.HandleAction(new SendComboAction([
            nameof(VirtualKeyCode.VK_CONTROL),
            nameof(VirtualKeyCode.VK_SHIFT),
            nameof(VirtualKeyCode.VK_ESCAPE)
        ]));

        Assert.NotNull(svc.LastStartedProcess);
        Assert.Equal("taskmgr.exe", svc.LastStartedProcess!.FileName);
        Assert.Empty(svc.KeyDowns);
        Assert.Empty(svc.KeyUps);
    }

    [Fact]
    public void HandleAction_system_shortcut_releases_transient_modifiers_after_direct_launch()
    {
        var svc = new TrackingInputService();
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);

        svc.HandleAction(new SendComboAction([
            nameof(VirtualKeyCode.VK_CONTROL),
            nameof(VirtualKeyCode.VK_SHIFT),
            nameof(VirtualKeyCode.VK_ESCAPE)
        ]));

        Assert.Empty(svc.StickyKeys);
        Assert.Contains(VirtualKeyCode.VK_CONTROL, svc.KeyUps);
    }

    [Fact]
    public void HandleAction_non_system_combo_keeps_existing_sendinput_path()
    {
        var svc = new TrackingInputService();

        svc.HandleAction(new SendComboAction([
            nameof(VirtualKeyCode.VK_LWIN),
            nameof(VirtualKeyCode.VK_E)
        ]));

        Assert.Null(svc.LastStartedProcess);
        Assert.Contains(VirtualKeyCode.VK_LWIN, svc.KeyDowns);
        Assert.Contains(VirtualKeyCode.VK_E, svc.KeyDowns);
        Assert.Contains(VirtualKeyCode.VK_E, svc.KeyUps);
        Assert.Contains(VirtualKeyCode.VK_LWIN, svc.KeyUps);
    }
}
