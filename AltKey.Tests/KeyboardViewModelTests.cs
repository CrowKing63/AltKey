using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using AltKey.ViewModels;

namespace AltKey.Tests;

public class KeyboardViewModelTests
{
    [Fact]
    public void KeySlotVm_uses_function_label_when_function_layer_active()
    {
        var slotVm = CreateSlotVm(new KeySlot(
            "A", null, new SendKeyAction("VK_A"), 1.0, 1.0, "", 0.0,
            "a", null,
            new SendKeyAction("VK_F1"), "F1", null, "f1", null));

        slotVm.ActiveSubmode = InputSubmode.HangulJamo;
        slotVm.SetModifierDisplayState(showShiftLabels: false, isCapsLockOn: false);
        slotVm.SetFunctionLayerState(FunctionLayerState.OneShot);

        Assert.Equal("F1", slotVm.DisplayLabel);
        Assert.Equal("f1", slotVm.SubLabelText);
    }

    [Fact]
    public void KeySlotVm_caps_lock_uppercases_only_alphabetic_english_label()
    {
        var alphabetSlot = CreateSlotVm(new KeySlot(
            "ㅁ", null, new SendKeyAction("VK_A"), 1.0, 1.0, "", 0.0,
            "a", null));
        var symbolSlot = CreateSlotVm(new KeySlot(
            "1", "!", new SendKeyAction("VK_1"), 1.0, 1.0, "", 0.0,
            "1", "!"));

        alphabetSlot.ActiveSubmode = InputSubmode.QuietEnglish;
        symbolSlot.ActiveSubmode = InputSubmode.QuietEnglish;

        alphabetSlot.SetModifierDisplayState(showShiftLabels: false, isCapsLockOn: true);
        symbolSlot.SetModifierDisplayState(showShiftLabels: false, isCapsLockOn: true);

        Assert.Equal("A", alphabetSlot.DisplayLabel);
        Assert.Equal("1", symbolSlot.DisplayLabel);
    }

    [Fact]
    public void KeySlotVm_reports_function_toggle_accessibility_help()
    {
        var slotVm = CreateSlotVm(new KeySlot(
            "Fn", null, new ToggleFunctionLayerAction()));

        slotVm.SetFunctionLayerState(FunctionLayerState.OneShot);
        Assert.Equal("Fn 한 번만 적용", slotVm.AccessibleHelp);

        slotVm.SetFunctionLayerState(FunctionLayerState.Locked);
        Assert.Equal("Fn 고정 상태", slotVm.AccessibleHelp);
    }

    [Fact]
    public void KeySlotVm_marks_function_accent_only_for_fn_targets()
    {
        var baseKey = CreateSlotVm(new KeySlot(
            "A", null, new SendKeyAction("VK_A")));
        var fnTargetKey = CreateSlotVm(new KeySlot(
            "A", null, new SendKeyAction("VK_A"), 1.0, 1.0, "", 0.0,
            null, null,
            new SendKeyAction("VK_F1")));
        var fnToggleKey = CreateSlotVm(new KeySlot(
            "Fn", null, new ToggleFunctionLayerAction()));

        Assert.False(baseKey.HasFunctionLayerAccent);
        Assert.True(fnTargetKey.HasFunctionLayerAccent);
        Assert.True(fnToggleKey.HasFunctionLayerAccent);
    }

    private static KeySlotVm CreateSlotVm(KeySlot slot)
    {
        var autoComplete = new AutoCompleteService(new FakeInputLanguageModule());
        var vm = new KeySlotVm(slot, autoComplete);
        vm.RefreshDisplay();
        return vm;
    }

    private sealed class FakeInputLanguageModule : IInputLanguageModule
    {
        public string LanguageCode => "ko";
        public InputSubmode ActiveSubmode => InputSubmode.HangulJamo;
        public string ComposeStateLabel => "가";
        public string CurrentWord => "";
        public event Action<IReadOnlyList<string>>? SuggestionsChanged { add { } remove { } }
        public event Action<InputSubmode>? SubmodeChanged { add { } remove { } }
        public bool HandleKey(KeySlot slot, KeyContext ctx) => false;
        public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion) => (0, suggestion);
        public void ToggleSubmode() { }
        public void OnSeparator() { }
        public void Reset() { }
        public void CommitCurrentWord() { }
        public void CancelComposition() { }
    }
}
