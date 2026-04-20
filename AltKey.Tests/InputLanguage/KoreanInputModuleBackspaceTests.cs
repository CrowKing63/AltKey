using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.Linq;

namespace AltKey.Tests.InputLanguage;

public class KoreanInputModuleBackspaceTests : KoreanInputModuleTestBase
{
    [Fact]
    public void Backspace_in_HangulJamo_reduces_composer_correctly()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        Assert.Equal("가", module.CurrentWord);

        var backSlot = TestSlotFactory.Backspace();
        module.HandleKey(backSlot, ctxNoModifiers);
        Assert.Equal("ㄱ", module.CurrentWord);

        module.HandleKey(backSlot, ctxNoModifiers);
        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void Backspace_in_QuietEnglish_reduces_prefix_correctly()
    {
        var module = CreateModule(out _);
        module.ToggleSubmode();

        var aSlot = TestSlotFactory.English("a", "A", VirtualKeyCode.VK_A);
        var bSlot = TestSlotFactory.English("b", "B", VirtualKeyCode.VK_B);
        var cSlot = TestSlotFactory.English("c", "C", VirtualKeyCode.VK_C);
        module.HandleKey(aSlot, ctxNoModifiers);
        module.HandleKey(bSlot, ctxNoModifiers);
        module.HandleKey(cSlot, ctxNoModifiers);
        Assert.Equal("abc", module.CurrentWord);

        var backSlot = TestSlotFactory.Backspace();
        module.HandleKey(backSlot, ctxNoModifiers);
        Assert.Equal("ab", module.CurrentWord);

        module.HandleKey(backSlot, ctxNoModifiers);
        Assert.Equal("a", module.CurrentWord);
    }

    [Fact]
    public void Backspace_during_composition_updates_composer_only()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();
        var bsSlot = TestSlotFactory.Backspace();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);

        int beforeBsPressCount = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);
        module.HandleKey(bsSlot, ctxNoModifiers);
        int afterBsPressCount = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);

        Assert.Equal("해", module.CurrentWord);
        Assert.Equal(beforeBsPressCount, afterBsPressCount);
    }

    [Fact]
    public void Backspace_after_composition_ended_does_not_send_backspace_via_module()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();
        var bsSlot = TestSlotFactory.Backspace();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.OnSeparator();

        int before = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);
        module.HandleKey(bsSlot, ctxNoModifiers);
        int after = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);

        Assert.Equal(before, after);
        Assert.Equal("", module.CurrentWord);
    }
}