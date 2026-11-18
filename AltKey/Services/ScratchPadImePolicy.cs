using System.Windows.Controls;
using System.Windows.Input;

namespace AltKey.Services;

/// <summary>빠른 메모가 현재 입력 모드에 맞는 WPF/OS IME 상태를 사용하도록 설정합니다.</summary>
internal static class ScratchPadImePolicy
{
    public static void ConfigureTextBox(System.Windows.Controls.TextBox textBox, InputMode mode)
    {
        // [VirtualKey] OS IME가 한글 자모를 조합해야 하므로 입력기 사용과 한글 변환 모드를 요청합니다.
        // [Unicode] AltKey가 직접 조합하므로 OS IME의 열림·변환 모드는 강제하지 않습니다.
        if (!InputMethod.GetIsInputMethodEnabled(textBox))
            InputMethod.SetIsInputMethodEnabled(textBox, true);

        var preferredState = mode == InputMode.VirtualKey
            ? InputMethodState.On
            : InputMethodState.DoNotCare;
        if (InputMethod.GetPreferredImeState(textBox) != preferredState)
            InputMethod.SetPreferredImeState(textBox, preferredState);

        var preferredConversion = mode == InputMode.VirtualKey
            ? ImeConversionModeValues.Native
            : ImeConversionModeValues.DoNotCare;
        if (InputMethod.GetPreferredImeConversionMode(textBox) != preferredConversion)
            InputMethod.SetPreferredImeConversionMode(textBox, preferredConversion);
    }
}
