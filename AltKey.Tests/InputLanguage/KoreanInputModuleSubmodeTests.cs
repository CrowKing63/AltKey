using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// KoreanInputModule.SetInputLanguage(외부 판별 결과로 내부 서브모드를 설정)의 동작을 검증합니다.
/// </summary>
public class KoreanInputModuleSubmodeTests
{
    private static KoreanInputModule Build()
    {
        var input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        return new KoreanInputModule(input, koDict, enDict, config);
    }

    /// 외부 설정 상태가 실제 상태로 반영되는지 확인합니다.
    [Fact]
    public void set_input_language_changes_state()
    {
        var module = Build();
        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);

        module.SetInputLanguage(InputSubmode.QuietEnglish);

        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
    }

    /// 이미 동일 상태면 no-op(이벤트 없음) 이어야 합니다.
    [Fact]
    public void set_input_language_same_state_is_noop()
    {
        var module = Build();
        var submodeCount = 0;
        var suggestionCount = 0;
        module.SubmodeChanged += _ => submodeCount++;
        module.SuggestionsChanged += _ => suggestionCount++;

        module.SetInputLanguage(InputSubmode.HangulJamo);

        Assert.Equal(0, submodeCount);
        Assert.Equal(0, suggestionCount);
        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);
    }

    /// 리셋이 서브모드를 한글로 되돌릴 때 변경 이벤트를 발화하는지 확인합니다.
    /// (자동완성 ON 전환 시 라벨과 내부 모드가 어긋나지 않도록)
    [Fact]
    public void reset_from_english_fires_submode_changed()
    {
        var module = Build();
        module.SetInputLanguage(InputSubmode.QuietEnglish);
        var submodes = new List<InputSubmode>();
        module.SubmodeChanged += s => submodes.Add(s);

        module.Reset();

        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);
        Assert.Equal(new[] { InputSubmode.HangulJamo }, submodes);
    }

    /// 이미 한글 상태라면 리셋에서 이벤트를 발화하지 않습니다.
    [Fact]
    public void reset_from_hangul_is_noop()
    {
        var module = Build();
        var count = 0;
        module.SubmodeChanged += _ => count++;

        module.Reset();

        Assert.Equal(0, count);
    }

    /// 상태 전환 시 SubmodeChanged와 빈 제안 이벤트가 발화하는지 확인합니다.
    [Fact]
    public void set_input_language_fires_submode_changed_and_empty_suggestions()
    {
        var module = Build();
        var submodes = new List<InputSubmode>();
        var suggestions = new List<IReadOnlyList<string>>();
        module.SubmodeChanged += s => submodes.Add(s);
        module.SuggestionsChanged += s => suggestions.Add(s);

        module.SetInputLanguage(InputSubmode.QuietEnglish);

        Assert.Equal(new[] { InputSubmode.QuietEnglish }, submodes);
        Assert.Contains(suggestions, s => s.Count == 0);
    }

    /// 설정 후 CurrentWord/ComposeStateLabel이 새 서브모드와 일치하는지 확인합니다.
    [Fact]
    public void set_input_language_updates_word_and_label()
    {
        var module = Build();
        module.SetInputLanguage(InputSubmode.QuietEnglish);

        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
        Assert.Equal("A", module.ComposeStateLabel);
        Assert.Equal("", module.CurrentWord);
    }
}
