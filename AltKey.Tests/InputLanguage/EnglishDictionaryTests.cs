using Xunit;

namespace AltKey.Tests.InputLanguage;

public class EnglishDictionaryTests
{
    [Fact]
    public void TryRemoveUserWord_Normalizes_To_LowerCase()
    {
        var dict = new EnglishDictionaryTestable();
        dict.RecordWord("Hello");
        Assert.Contains("hello", dict.GetSuggestions("he"));

        Assert.True(dict.TryRemoveUserWord("HELLO"));
        Assert.DoesNotContain("hello", dict.GetSuggestions("he"));
    }

    [Fact]
    public void TryRemoveUserWord_Returns_False_For_NonExistent()
    {
        var dict = new EnglishDictionaryTestable();
        Assert.False(dict.TryRemoveUserWord("nonexistent"));
    }
}