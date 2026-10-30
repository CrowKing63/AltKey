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

    [Fact]
    public void Backspace_tap_in_unicode_composition_uses_atomic_replace_without_virtual_key_press()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();
        var bsSlot = TestSlotFactory.Backspace();

        TestSlotFactory.FeedSyllables(module, "한", CtxFromInput(input));

        int beforeAtomicReplace = input.AtomicReplaces.Count;
        int beforeKeyPress = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);

        bool handled = module.HandleKey(bsSlot, CtxFromInput(input));

        int afterKeyPress = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);

        Assert.True(handled);
        Assert.True(input.AtomicReplaces.Count > beforeAtomicReplace);
        Assert.Equal(beforeKeyPress, afterKeyPress);
        Assert.NotEqual("", module.CurrentWord);
    }

    /// <summary>
    /// 한글 조합 중 백스페이스는 지워진 꼬리만 지우고 전체를 다시 쓰지 않는다
    /// (suffix-only rewrite, docs/auto-complet/TASK-09-suffix-only-rewrite.md).
    /// </summary>
    [Fact]
    public void Backspace_in_HangulJamo_Unicode_erases_only_the_deleted_tail()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();
        var bsSlot = TestSlotFactory.Backspace();

        TestSlotFactory.FeedSyllables(module, "한", CtxFromInput(input));

        module.HandleKey(bsSlot, CtxFromInput(input));

        Assert.Equal("하", module.CurrentWord);
        // "한" → "하": 음절이 달라져 접두사 없음 — 전체 1자 지우고 "하" 전송.
        // (구/신 구현 모두 (1, "하")인 비차별 케이스: 접두사 보존 확인은 아래 테스트.)
        // ConcurrentBag은 삽입 반대순서(LIFO)로 열거되므로 .First()가 마지막 작업.
        var (bs, next) = input.AtomicReplaces.First();
        Assert.Equal(1, bs);
        Assert.Equal("하", next);
    }

    /// <summary>
    /// 백스페이스로 종성이 빠질 때 접두사가 보존되어 suffix만 지운다.
    /// "안녕" → "안녀": (1, "녀") — 기존 구현은 (2, "안녀")이었으므로 차별화 테스트.
    /// </summary>
    [Fact]
    public void Backspace_removing_jongseong_keeps_prefix_and_sends_only_suffix()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();
        var bsSlot = TestSlotFactory.Backspace();

        TestSlotFactory.FeedSyllables(module, "안녕", CtxFromInput(input));

        module.HandleKey(bsSlot, CtxFromInput(input));

        Assert.Equal("안녀", module.CurrentWord);
        // "안녕" → "안녀": 종성 ㄴ 제거, 접두사 1자 보존 — 1자만 지우고 "녀" 전송.
        var (bs, next) = input.AtomicReplaces.First();
        Assert.Equal(1, bs);
        Assert.Equal("녀", next);
    }
}
