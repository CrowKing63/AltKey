using Xunit;

namespace AltKey.Tests.InputLanguage;

public class KoreanDictionaryTests
{
    [Fact]
    public void RecordWord_rejects_single_jamo()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("ㄱ");
        Assert.Equal(0, dict.UserWordCount);
    }

    [Fact]
    public void RecordWord_rejects_single_syllable()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("해");
        // 사용자 학습으로 "해"가 저장되지 않았으므로 제안에 "해" 단독이 올라오면 안 됨.
        // (내장 사전에 "해달" 등이 있을 수 있으므로 전체가 비었는지는 확인하지 않음)
        var sugg = dict.GetSuggestions("해");
        Assert.DoesNotContain("해", sugg);
        Assert.Equal(0, dict.UserWordCount);
    }

    [Fact]
    public void RecordWord_accepts_two_or_more_syllables()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("해달");
        var sugg = dict.GetSuggestions("해");
        Assert.Contains("해달", sugg);
    }

    [Fact]
    public void RecordWord_rejects_whitespace_only()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("   ");
        Assert.Equal(0, dict.UserWordCount);
    }

    [Fact]
    public void RecordWord_rejects_null_or_empty()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("");
        Assert.Equal(0, dict.UserWordCount);
    }
}
