using AltKey.Views;
using Xunit;

namespace AltKey.Tests.Clipboard;

public class ClipboardPanelWindowTargetTests
{
    [Fact]
    public void 패널이_활성화된_상태에서는_가장_최근_외부_창을_선택한다()
    {
        IntPtr captured = new(10);
        IntPtr latestExternal = new(20);

        var selected = ClipboardPanelWindow.SelectRestoreTarget(
            currentForeground: new IntPtr(1),
            capturedForeground: captured,
            latestExternalForeground:            latestExternal,
            capturedIsScratchPad: false,
            isCurrentForegroundOwn: true,
            isValidWindow: hwnd => hwnd != IntPtr.Zero);

        Assert.Equal(latestExternal, selected);
    }

    [Fact]
    public void 외부_창이_이미_포그라운드면_그_창을_붙여넣기_대상으로_유지한다()
    {
        IntPtr currentExternal = new(30);

        var selected = ClipboardPanelWindow.SelectRestoreTarget(
            currentForeground: currentExternal,
            capturedForeground: new IntPtr(10),
            latestExternalForeground: new IntPtr(20),
            capturedIsScratchPad: false,
            isCurrentForegroundOwn: false,
            isValidWindow: hwnd => hwnd != IntPtr.Zero);

        Assert.Equal(currentExternal, selected);
    }

    [Fact]
    public void 최근_외부_창이_무효면_열기_당시_유효한_창으로_폴백한다()
    {
        IntPtr captured = new(10);

        var selected = ClipboardPanelWindow.SelectRestoreTarget(
            currentForeground: new IntPtr(1),
            capturedForeground: captured,
            latestExternalForeground: new IntPtr(20),
            capturedIsScratchPad: false,
            isCurrentForegroundOwn: true,
            isValidWindow: hwnd => hwnd == captured);

        Assert.Equal(captured, selected);
    }

    [Fact]
    public void 붙여넣기_대상이_열기_당시_창이면_해당_창의_입력_컨트롤_포커스를_복원한다()
    {
        IntPtr target = new(10);
        IntPtr focusedControl = new(11);

        var selected = ClipboardPanelWindow.SelectCapturedFocusTarget(
            restoreTarget: target,
            capturedTarget: target,
            capturedFocus: focusedControl,
            capturedIsScratchPad: false,
            isValidChild: hwnd => hwnd == focusedControl);

        Assert.Equal(focusedControl, selected);
    }

    [Fact]
    public void 다른_외부_창이_최신_대상이면_이전_창의_입력_포커스를_복원하지_않는다()
    {
        IntPtr capturedTarget = new(10);
        IntPtr currentTarget = new(20);
        IntPtr staleFocus = new(11);

        var selected = ClipboardPanelWindow.SelectCapturedFocusTarget(
            restoreTarget: currentTarget,
            capturedTarget: capturedTarget,
            capturedFocus: staleFocus,
            capturedIsScratchPad: false,
            isValidChild: hwnd => hwnd == staleFocus);

        Assert.Equal(IntPtr.Zero, selected);
    }

    [Fact]
    public void 스크래치패드가_붙여넣기_대상이면_패널_숨김_시_포그라운드를_복원하지_않는다()
    {
        IntPtr scratchPad = new(30);

        bool shouldRestore = ClipboardPanelWindow.ShouldRestoreInputForegroundOnHide(
            currentForeground: scratchPad,
            scratchPadWindow: scratchPad);

        Assert.False(shouldRestore);
    }

    [Fact]
    public void 다른_창이_포그라운드면_패널_숨김_시_기존_복원을_유지한다()
    {
        bool shouldRestore = ClipboardPanelWindow.ShouldRestoreInputForegroundOnHide(
            currentForeground: new IntPtr(10),
            scratchPadWindow: new IntPtr(30));

        Assert.True(shouldRestore);
    }
}
