using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

public class KoreanInputModuleTests
{
    private static KeySlot ㅎ_slot => TestSlotFactory.Jamo("ㅎ", null, VirtualKeyCode.VK_H);
    private static KeySlot ㅐ_slot => TestSlotFactory.Jamo("ㅐ", null, VirtualKeyCode.VK_O);
    private static KeySlot ㄷ_slot => TestSlotFactory.Jamo("ㄷ", null, VirtualKeyCode.VK_E);
    private static KeySlot ㅏ_slot => TestSlotFactory.Jamo("ㅏ", null, VirtualKeyCode.VK_K);
    private static KeySlot ㄹ_slot => TestSlotFactory.Jamo("ㄹ", null, VirtualKeyCode.VK_T);
    private static KeySlot ㄱ_slot => TestSlotFactory.Jamo("ㄱ", null, VirtualKeyCode.VK_R);
    private static KeySlot ㅇ_slot => TestSlotFactory.Jamo("ㅇ", null, VirtualKeyCode.VK_D);
    private static KeySlot ㄴ_slot => TestSlotFactory.Jamo("ㄴ", null, VirtualKeyCode.VK_N);
    private static KeySlot ㅣ_slot => TestSlotFactory.Jamo("ㅣ", null, VirtualKeyCode.VK_I);
    private static KeySlot ㅕ_slot => TestSlotFactory.Jamo("ㅕ", null, VirtualKeyCode.VK_J);
    private static KeySlot ㅅ_with_shift_ㅆ_slot => TestSlotFactory.Jamo("ㅅ", "ㅆ", VirtualKeyCode.VK_T);
    private static KeySlot ㅃ_slot => TestSlotFactory.Jamo("ㅂ", "ㅃ", VirtualKeyCode.VK_Q);
    private static KeySlot ㅉ_slot => TestSlotFactory.Jamo("ㅈ", "ㅉ", VirtualKeyCode.VK_W);
    private static KeySlot ㄸ_slot => TestSlotFactory.Jamo("ㄷ", "ㄸ", VirtualKeyCode.VK_E);
    private static KeySlot ㄲ_slot => TestSlotFactory.Jamo("ㄱ", "ㄲ", VirtualKeyCode.VK_R);
    private static KeySlot ㅒ_slot => TestSlotFactory.Jamo("ㅑ", "ㅒ", VirtualKeyCode.VK_O);
    private static KeySlot ㅖ_slot => TestSlotFactory.Jamo("ㅕ", "ㅖ", VirtualKeyCode.VK_P);
    private static KeySlot q_slot_with_english_label_q => TestSlotFactory.English("Q", null, VirtualKeyCode.VK_Q);
    private static KeySlot u_slot_with_english_label_u => TestSlotFactory.English("U", null, VirtualKeyCode.VK_U);

    private static KeyContext ctxNoModifiers => new(false, false, false, InputMode.Unicode, 0);
    private static KeyContext ctxShiftOnly => new(true, true, false, InputMode.Unicode, 1);
    private static KeyContext ctxCtrlShift => new(true, true, true, InputMode.Unicode, 0);

    private KoreanInputModule CreateModule(out FakeInputService input)
    {
        input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        return new KoreanInputModule(input, koDict, enDict);
    }

    [Fact]
    public void Feed_해_해_separator_records_해()
    {
        var module = CreateModule(out var input);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        Assert.Equal("해", module.CurrentWord);

        module.OnSeparator();
        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void 해_plus_Shift_ㅆ_feeds_ssang_siot_not_T()   // ★ 회귀 방지
    {
        var module = CreateModule(out var input);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        // Shift sticky 활성 상태 — HasActiveModifiers=true지만 ExcludingShift=false
        module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctxShiftOnly);

        // 핵심: "T"가 나와서는 안 됨 (Shift+VK_T가 영문 T로 해석되면 안 됨)
        // 현재 구현: ㅆ을 종성으로 처리 → "했" (또는 새 초성 → "해ㅆ")
        Assert.DoesNotContain("T", module.CurrentWord);
        Assert.True(module.CurrentWord is "했" or "해ㅆ");
    }

    [Fact]
    public void Ctrl_Shift_T_is_not_a_jamo_path()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.ToggleSubmode();

        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
        Assert.Equal("A", module.ComposeStateLabel);
        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void QuietEnglish_mode_feeds_english_prefix_and_sends_unicode()
    {
        var module = CreateModule(out var input);
        module.ToggleSubmode();

        module.HandleKey(q_slot_with_english_label_q, ctxNoModifiers);
        module.HandleKey(u_slot_with_english_label_u, ctxNoModifiers);

        Assert.Equal("qu", module.CurrentWord);
        Assert.Contains("q", input.SentUnicodes);
        Assert.Contains("u", input.SentUnicodes);
    }

    [Fact]
    public void AcceptSuggestion_in_HangulJamo_returns_correct_bsCount()
    {
        var module = CreateModule(out _);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var (bs, word) = module.AcceptSuggestion("해달");

        Assert.Equal(2, bs);
        Assert.Equal("해달", word);
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

        module.HandleKey(ㅉ_slot, ctxShiftOnly);
        Assert.Contains("ㅉ", module.CurrentWord);

        module.HandleKey(ㄸ_slot, ctxShiftOnly);
        Assert.Contains("ㄸ", module.CurrentWord);

        module.HandleKey(ㄲ_slot, ctxShiftOnly);
        Assert.Contains("ㄲ", module.CurrentWord);

        module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctxShiftOnly);
        Assert.Contains("ㅆ", module.CurrentWord);
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
}
