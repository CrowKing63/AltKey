using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

public class KoreanInputModuleQuietEnglishTests : KoreanInputModuleTestBase
{
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
    public void QuietEnglish_Shift_A_updates_prefix_with_uppercase_A()
    {
        var module = CreateModule(out var input);
        module.ToggleSubmode();

        var aSlot = TestSlotFactory.English("a", "A", VirtualKeyCode.VK_A);
        module.HandleKey(aSlot, ctxShiftOnly);

        Assert.Equal("A", module.CurrentWord);
        Assert.Contains("A", input.SentUnicodes);
    }

    [Fact]
    public void QuietEnglish_number_and_symbol_keys_are_sent()
    {
        var module = CreateModule(out var input);
        module.ToggleSubmode();

        var key1 = TestSlotFactory.Number("1", "!", VirtualKeyCode.VK_1);
        module.HandleKey(key1, ctxNoModifiers);

        var keyDash = TestSlotFactory.Symbol("-", "_", VirtualKeyCode.VK_OEM_MINUS);
        module.HandleKey(keyDash, ctxNoModifiers);

        Assert.Equal("1-", module.CurrentWord);
        Assert.Contains("1", input.SentUnicodes);
        Assert.Contains("-", input.SentUnicodes);
    }

    [Fact]
    public void AcceptSuggestion_in_QuietEnglish_returns_correct_bsCount()
    {
        var module = CreateModule(out _);
        module.ToggleSubmode();

        var hSlot = TestSlotFactory.English("h", "H", VirtualKeyCode.VK_H);
        var eSlot = TestSlotFactory.English("e", "E", VirtualKeyCode.VK_E);
        var lSlot = TestSlotFactory.English("l", "L", VirtualKeyCode.VK_L);
        var l2Slot = TestSlotFactory.English("l", "L", VirtualKeyCode.VK_L);
        var oSlot = TestSlotFactory.English("o", "O", VirtualKeyCode.VK_O);
        module.HandleKey(hSlot, ctxNoModifiers);
        module.HandleKey(eSlot, ctxNoModifiers);
        module.HandleKey(lSlot, ctxNoModifiers);
        module.HandleKey(l2Slot, ctxNoModifiers);
        module.HandleKey(oSlot, ctxNoModifiers);
        Assert.Equal("hello", module.CurrentWord);

        var (bs, word) = module.AcceptSuggestion("help");

        Assert.Equal(5, bs);
        Assert.Equal("help", word);
        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void Bigram_recorded_in_QuietEnglish_mode()
    {
        var (module, _, _, enDict) = TestSlotFactory.CreateModuleWithBothDicts();

        module.ToggleSubmode();

        var ctxEnglish = new KeyContext(false, false, false, InputMode.Unicode, 0);
        TestSlotFactory.FeedEnglish(module, "hello", ctxEnglish);
        module.OnSeparator();

        TestSlotFactory.FeedEnglish(module, "world", ctxEnglish);
        module.OnSeparator();

        Assert.True(enDict.BigramStore.Contains("hello", "world"));
    }
}