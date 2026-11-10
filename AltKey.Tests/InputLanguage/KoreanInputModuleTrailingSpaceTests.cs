using AltKey.Models;
using AltKey.Platform;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// KoreanInputModule 꼬리 공백(trailing space) 테스트:
/// 제안 수락 시 fullWord 끝에 공백이 붙고, pending 상태에서 구분자 키가 오면
/// 그 공백을 백스페이스로 환원한 뒤 키를 통과시키는 동작.
/// (docs/auto-complet/TASK-11-trailing-space-on-accept.md)
/// </summary>
public class KoreanInputModuleTrailingSpaceTests : KoreanInputModuleTestBase
{
    private static KeySlot PeriodSlot => TestSlotFactory.Symbol(".", null, VirtualKeyCode.VK_OEM_PERIOD);
    private static KeySlot SpaceSlot => TestSlotFactory.Jamo(" ", null, VirtualKeyCode.VK_SPACE);

    /// <summary>수락한 뒤 구분자 키가 환원 백스페이스를 보냈는지 확인하는 헬퍼.</summary>
    private static bool HasRetraction(FakeInputService input) =>
        input.BackspaceCounts.Contains(1);

    [Fact]
    public void AcceptSuggestion_appends_trailing_space_and_marks_pending()
    {
        var module = CreateModule(out _);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var (_, word, _) = module.AcceptSuggestion("해달");

        Assert.Equal("해달 ", word);
        Assert.True(module.HasPendingTrailingSpace);
    }

    [Fact]
    public void Separator_after_accept_retracts_trailing_space_and_passes_key()
    {
        var module = CreateModule(out var input);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        bool handled = module.HandleKey(PeriodSlot, ctxNoModifiers);

        Assert.False(handled);
        Assert.True(HasRetraction(input));
        Assert.False(module.HasPendingTrailingSpace);
    }

    [Fact]
    public void Space_after_accept_retracts_trailing_space()
    {
        var module = CreateModule(out var input);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        bool handled = module.HandleKey(SpaceSlot, ctxNoModifiers);

        Assert.False(handled);
        Assert.True(HasRetraction(input));
        Assert.False(module.HasPendingTrailingSpace);
    }

    /// <summary>
    /// 실제 프로덕션 경로: SPACE·RETURN·TAB은 HandleKey를 거치지 않고
    /// OnSeparator()로만 들어온다. 이 경로에서도 환원이 일어나야 한다.
    /// </summary>
    [Fact]
    public void OnSeparator_after_accept_retracts_trailing_space()
    {
        var module = CreateModule(out var input);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        module.OnSeparator();

        Assert.True(HasRetraction(input));
        Assert.False(module.HasPendingTrailingSpace);
    }

    /// <summary>
    /// 포커스가 다른 앱으로 이동한 상태에서 OnSeparator가 오면 환원하지 않는다.
    /// </summary>
    [Fact]
    public void OnSeparator_after_accept_with_foreground_change_does_not_retract()
    {
        var module = CreateModule(out var input);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        input.ForegroundHwnd = new IntPtr(9999);

        module.OnSeparator();

        Assert.False(module.HasPendingTrailingSpace);
        Assert.False(HasRetraction(input));
    }

    [Fact]
    public void NonSeparatorChar_after_accept_releases_pending_without_retraction()
    {
        var module = CreateModule(out var input);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        module.HandleKey(ㅎ_slot, ctxNoModifiers);

        Assert.False(module.HasPendingTrailingSpace);
        Assert.False(HasRetraction(input));
    }

    [Fact]
    public void Backspace_after_accept_clears_pending_without_retraction()
    {
        var module = CreateModule(out var input);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        module.HandleKey(TestSlotFactory.Backspace(), ctxNoModifiers);

        Assert.False(module.HasPendingTrailingSpace);
        Assert.False(HasRetraction(input));
    }

    [Fact]
    public void ForegroundChange_after_accept_separator_does_not_retract()
    {
        var module = CreateModule(out var input);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        // 포커스가 다른 앱으로 이동한 상황 — 환원 백스페이스는 대상이 아니다.
        input.ForegroundHwnd = new IntPtr(9999);

        module.HandleKey(PeriodSlot, ctxNoModifiers);

        Assert.False(module.HasPendingTrailingSpace);
        Assert.False(HasRetraction(input));
    }

    [Fact]
    public void CancelComposition_after_accept_clears_pending()
    {
        var module = CreateModule(out _);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        module.CancelComposition();

        Assert.False(module.HasPendingTrailingSpace);
    }

    [Fact]
    public void Reset_after_accept_clears_pending()
    {
        var module = CreateModule(out _);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        module.Reset();

        Assert.False(module.HasPendingTrailingSpace);
    }

    [Fact]
    public void ToggleSubmode_after_accept_clears_pending()
    {
        var module = CreateModule(out _);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");

        module.ToggleSubmode();

        Assert.False(module.HasPendingTrailingSpace);
    }

    [Fact]
    public void QuietEnglish_accept_appends_trailing_space_and_separator_retracts()
    {
        var module = CreateModule(out var input);
        module.ToggleSubmode();
        TestSlotFactory.FeedEnglish(module, "he", ctxNoModifiers);

        var (_, word, _) = module.AcceptSuggestion("hello");
        Assert.Equal("hello ", word);
        Assert.True(module.HasPendingTrailingSpace);

        bool handled = module.HandleKey(PeriodSlot, ctxNoModifiers);

        Assert.False(handled);
        Assert.True(HasRetraction(input));
        Assert.False(module.HasPendingTrailingSpace);
    }
}
