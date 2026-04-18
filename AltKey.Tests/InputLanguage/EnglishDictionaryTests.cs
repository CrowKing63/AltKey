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

    [Fact]
    public void RecordBigram_is_case_insensitive_storage()
    {
        var dict = new EnglishDictionaryTestable();
        dict.RecordBigram("Hello", "World");
        Assert.True(dict.BigramStore.Contains("hello", "world"));
    }

    [Fact]
    public void GetSuggestions_english_prev_boosts_bigram_next()
    {
        var dict = new EnglishDictionaryTestable();
        dict.RecordWord("world");
        dict.RecordWord("work");
        for (int i = 0; i < 3; i++) dict.RecordBigram("hello", "world");

        var sugg = dict.GetSuggestions("wo", "hello", 5);
        Assert.Equal("world", sugg[0]);
    }
}