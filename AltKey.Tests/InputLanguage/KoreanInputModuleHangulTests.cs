using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// KoreanInputModule 한글 조합 테스트: 자모 조합, 쌍자모(Shift), 상태 초기화.
/// 제안/학습 관련 테스트는 KoreanInputModuleSuggestionTests.cs에 있다.
/// </summary>
public class KoreanInputModuleHangulTests : KoreanInputModuleTestBase
{
    [Fact]
    public void Feed_해_해_separator_records_해()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        Assert.Equal("해", module.CurrentWord);

        module.OnSeparator();
        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void 해_plus_Shift_ㅆ_feeds_ssang_siot_not_T()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctxShiftOnly);

        Assert.DoesNotContain("T", module.CurrentWord);
        Assert.True(module.CurrentWord is "했" or "해ㅆ");
    }

    [Fact]
    public void Feed_ㄷ_ㅏ_ㄹ_ㄱ_composes_닭()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);
        module.HandleKey(ㄱ_slot, ctxNoModifiers);

        Assert.Equal("닭", module.CurrentWord);
    }

    [Fact]
    public void Feed_ㄱ_ㅏ_ㅇ_ㅣ_composes_가_then_이()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);

        Assert.Equal("가이", module.CurrentWord);
    }

    [Fact]
    public void Feed_ㅇ_ㅏ_ㄴ_ㄴ_ㅕ_ㅇ_composes_안녕()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅕ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);

        Assert.Equal("안녕", module.CurrentWord);
    }

    [Fact]
    public void SsangConsonants_via_ShiftLabel_compose_correctly()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅃ_slot, ctxShiftOnly);
        Assert.Contains("ㅃ", module.CurrentWord);

        module.HandleKey(ㄲ_slot, ctxShiftOnly);
        Assert.Contains("ㄲ", module.CurrentWord);
    }

    [Fact]
    public void SsangVowels_via_ShiftLabel_compose_correctly()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅒ_slot, ctxShiftOnly);
        Assert.Contains("ㅒ", module.CurrentWord);

        module.HandleKey(ㅖ_slot, ctxShiftOnly);
        Assert.Contains("ㅖ", module.CurrentWord);
    }

    [Fact]
    public void Reset_clears_all_state()
    {
        var module = CreateModule(out _);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        module.Reset();

        Assert.Equal("", module.CurrentWord);
        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);
    }

    [Fact]
    public void ToggleSubmode_back_to_HangulJamo_resets_correctly()
    {
        var module = CreateModule(out _);
        module.ToggleSubmode();
        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);

        module.ToggleSubmode();
        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);
        Assert.Equal("가", module.ComposeStateLabel);
    }

    [Fact]
    public void CancelComposition_resets_composer_and_tracked_length()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅅ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);
        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);

        Assert.Equal("시간", module.CurrentWord);

        module.CancelComposition();

        Assert.Equal("", module.CurrentWord);
        Assert.Equal(0, input.TrackedOnScreenLength);
    }

    [Fact]
    public void CommitCurrentWord_resets_composer_and_tracked_length()
    {
        var (module, input, koDict) = TestSlotFactory.CreateModuleWithInput();

        // "친척" = ㅊ ㅣ ㄴ ㅊ ㅓ ㄱ — 매 키마다 TrackedOnScreenLength 반영
        module.HandleKey(ㅊ_slot, CtxFromInput(input));
        module.HandleKey(ㅣ_slot, CtxFromInput(input));
        module.HandleKey(ㄴ_slot, CtxFromInput(input));
        module.HandleKey(ㅊ_slot, CtxFromInput(input));
        module.HandleKey(ㅓ_slot, CtxFromInput(input));
        module.HandleKey(ㄱ_slot, CtxFromInput(input));

        Assert.Equal("친척", module.CurrentWord);

        // CommitCurrentWord (첫 번째 슬롯 클릭)
        module.CommitCurrentWord();

        // composer가 리셋되었는지 확인
        Assert.Equal("", module.CurrentWord);
        Assert.Equal(0, input.TrackedOnScreenLength);

        // "ㅇ" 입력 — 이전 단어가 남아있지 않아야 함
        module.HandleKey(ㅇ_slot, CtxFromInput(input));
        Assert.Equal("ㅇ", module.CurrentWord);

        // "에게" 완성
        module.HandleKey(ㅔ_slot, CtxFromInput(input));
        module.HandleKey(ㄱ_slot, CtxFromInput(input));
        module.HandleKey(ㅔ_slot, CtxFromInput(input));
        Assert.Equal("에게", module.CurrentWord);

        // 스페이스
        module.OnSeparator();

        Assert.DoesNotContain("친척에게", koDict.GetSuggestions("친척", 10));
    }

    [Fact]
    public void Feed_number_one_without_shift_finalizes_composition()
    {
        var (module, _, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var numberSlot = TestSlotFactory.Symbol("1", "!", VirtualKeyCode.VK_1);
        module.HandleKey(numberSlot, ctxNoModifiers);

        // 자모가 아닌 키는 조합을 확정하므로 CurrentWord가 비어 있다.
        Assert.Equal("", module.CurrentWord);
    }

    /// <summary>
    /// Shift 버튼(ToggleSticky) 탭은 조합을 확정하지 않는다.
    /// "가" 입력 후 Shift 탭, 그다음 Shift+ㅅ(→ㅆ)이 쌍받침으로 붙어서 "갔"이 된다.
    /// </summary>
    [Fact]
    public void HangulMode_ShiftButton_Tap_DoesNotFinalizeComposition()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        Assert.Equal("가", module.CurrentWord);

        // Shift 버튼 탭 — 조합 상태와 추적 길이를 건드리면 안 된다.
        var shiftSlot = new KeySlot(
            Label: "Shift",
            ShiftLabel: null,
            Action: new ToggleStickyAction(VirtualKeyCode.VK_SHIFT.ToString()));
        module.HandleKey(shiftSlot, CtxFromInput(input));

        Assert.Equal("가", module.CurrentWord);
        Assert.Equal(1, input.TrackedOnScreenLength);

        // Shift+ㅅ(→ㅆ): "가"에 쌍받침이 붙어 "갔" — 백스페이스 1회 기준으로 교체.
        module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctxShiftOnly);

        Assert.Equal("갔", module.CurrentWord);
        // (prevLen=1, next="갔") — "가" 한 글자를 지우고 "갔"으로 교체한 기록이 있어야 한다.
        Assert.Contains((1, "갔"), input.AtomicReplaces);
    }

    /// <summary>
    /// 조합 중 Shift를 탭하면 완성되지 않은 음절("가")이 사전에 기록되어서는 안 된다.
    /// </summary>
    [Fact]
    public void HangulMode_ShiftTap_DoesNotRecordWord_WhenAutoCompleteOn()
    {
        var (module, input, koDict) = TestSlotFactory.CreateModuleWithInput(autoCompleteEnabled: true);

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);

        var shiftSlot = new KeySlot(
            Label: "Shift",
            ShiftLabel: null,
            Action: new ToggleStickyAction(VirtualKeyCode.VK_SHIFT.ToString()));
        module.HandleKey(shiftSlot, CtxFromInput(input));

        // 조합은 확정되지 않았으므로 사전에 새 단어가 없어야 한다.
        Assert.Equal(0, koDict.UserWordCount);
        Assert.Equal("가", module.CurrentWord);
    }
}
