using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using InputMode = AltKey.Services.InputMode;
using ScratchPadImePolicy = AltKey.Services.ScratchPadImePolicy;

namespace AltKey.Tests.ScratchPad;

public class ScratchPadInputMethodTests
{
    [Fact]
    public void ConfigureInputMethod_메모에서_OS_IME_사용을_요청한다()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var textBox = new TextBox();
                ScratchPadImePolicy.ConfigureTextBox(textBox, InputMode.VirtualKey);

                Assert.True(InputMethod.GetIsInputMethodEnabled(textBox));
                Assert.Equal(InputMethodState.On, InputMethod.GetPreferredImeState(textBox));
                Assert.Equal(ImeConversionModeValues.Native, InputMethod.GetPreferredImeConversionMode(textBox));

                ScratchPadImePolicy.ConfigureTextBox(textBox, InputMode.Unicode);
                Assert.Equal(InputMethodState.DoNotCare, InputMethod.GetPreferredImeState(textBox));
                Assert.Equal(ImeConversionModeValues.DoNotCare, InputMethod.GetPreferredImeConversionMode(textBox));
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new Xunit.Sdk.XunitException($"STA 입력 메서드 설정 검사 실패: {failure}");
    }
}
