using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

public abstract class KoreanInputModuleTestBase
{
    protected static KeySlot ㅎ_slot => TestSlotFactory.Jamo("ㅎ", null, VirtualKeyCode.VK_H);
    protected static KeySlot ㅐ_slot => TestSlotFactory.Jamo("ㅐ", null, VirtualKeyCode.VK_O);
    protected static KeySlot ㄷ_slot => TestSlotFactory.Jamo("ㄷ", null, VirtualKeyCode.VK_E);
    protected static KeySlot ㅏ_slot => TestSlotFactory.Jamo("ㅏ", null, VirtualKeyCode.VK_K);
    protected static KeySlot ㄹ_slot => TestSlotFactory.Jamo("ㄹ", null, VirtualKeyCode.VK_T);
    protected static KeySlot ㄱ_slot => TestSlotFactory.Jamo("ㄱ", null, VirtualKeyCode.VK_R);
    protected static KeySlot ㅇ_slot => TestSlotFactory.Jamo("ㅇ", null, VirtualKeyCode.VK_D);
    protected static KeySlot ㄴ_slot => TestSlotFactory.Jamo("ㄴ", null, VirtualKeyCode.VK_N);
    protected static KeySlot ㅣ_slot => TestSlotFactory.Jamo("ㅣ", null, VirtualKeyCode.VK_I);
    protected static KeySlot ㅕ_slot => TestSlotFactory.Jamo("ㅕ", null, VirtualKeyCode.VK_J);
    protected static KeySlot ㅅ_with_shift_ㅆ_slot => TestSlotFactory.Jamo("ㅅ", "ㅆ", VirtualKeyCode.VK_T);
    protected static KeySlot ㅃ_slot => TestSlotFactory.Jamo("ㅂ", "ㅃ", VirtualKeyCode.VK_Q);
    protected static KeySlot ㅉ_slot => TestSlotFactory.Jamo("ㅈ", "ㅉ", VirtualKeyCode.VK_W);
    protected static KeySlot ㄸ_slot => TestSlotFactory.Jamo("ㄷ", "ㄸ", VirtualKeyCode.VK_E);
    protected static KeySlot ㄲ_slot => TestSlotFactory.Jamo("ㄱ", "ㄲ", VirtualKeyCode.VK_R);
    protected static KeySlot ㅒ_slot => TestSlotFactory.Jamo("ㅑ", "ㅒ", VirtualKeyCode.VK_O);
    protected static KeySlot ㅖ_slot => TestSlotFactory.Jamo("ㅕ", "ㅖ", VirtualKeyCode.VK_P);
    protected static KeySlot q_slot_with_english_label_q => TestSlotFactory.English("Q", null, VirtualKeyCode.VK_Q);
    protected static KeySlot u_slot_with_english_label_u => TestSlotFactory.English("U", null, VirtualKeyCode.VK_U);

    protected static KeyContext ctxNoModifiers => new(false, false, false, InputMode.Unicode, 0);
    protected static KeyContext ctxShiftOnly => new(true, true, false, InputMode.Unicode, 1);
    protected static KeyContext ctxCtrlShift => new(true, true, true, InputMode.Unicode, 0);

    protected KoreanInputModule CreateModule(out FakeInputService input, bool autoCompleteEnabled = true)
    {
        input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = autoCompleteEnabled;
        return new KoreanInputModule(input, koDict, enDict, config);
    }
}