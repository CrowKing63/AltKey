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

    [Fact]
    public void GetSuggestions_with_choseong_jamo_returns_words_starting_with_that_choseong()
    {
        var dict = new KoreanDictionaryTestable();
        var sugg = dict.GetSuggestions("ㄱ", 5);
        // 결과가 비어있지 않아야 함 (내장 사전에 ㄱ으로 시작하는 단어가 있다고 가정)
        Assert.NotEmpty(sugg);
        // 모든 단어의 첫 글자가 ㄱ 초성인 완성 음절이어야 함
        Assert.All(sugg, w =>
        {
            Assert.InRange(w[0], '\uAC00', '\uD7A3');
            int choIdx = (w[0] - 0xAC00) / (21 * 28);
            Assert.Equal(0, choIdx); // ㄱ = index 0
        });
    }

    [Fact]
    public void GetSuggestions_with_complete_syllable_prefix_unchanged()
    {
        var dict = new KoreanDictionaryTestable();
        var sugg = dict.GetSuggestions("가", 5);
        // 완성 음절 prefix 매칭이 여전히 동작해야 함
        Assert.All(sugg, w =>
        {
            Assert.StartsWith("가", w);
            Assert.True(w.Length > 1);
        });
    }

    [Fact]
    public void TryRemoveUserWord_Removes_Only_From_User_Store_Not_BuiltIn()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("해달테스트");
        Assert.Contains("해달테스트", dict.GetSuggestions("해달"));

        Assert.True(dict.TryRemoveUserWord("해달테스트"));
        Assert.DoesNotContain("해달테스트", dict.GetSuggestions("해달"));

        Assert.False(dict.TryRemoveUserWord("사랑"));
    }

    [Fact]
    public void GetSuggestions_choseong_user_word_included()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("해달");
        var sugg = dict.GetSuggestions("ㅎ", 5);
        Assert.Contains("해달", sugg);
    }

    [Fact]
    public void GetSuggestions_with_prev_null_is_same_as_no_context()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("해달");
        var a = dict.GetSuggestions("해");
        var b = dict.GetSuggestions("해", null);
        Assert.Equal(a, b);
    }

    [Fact]
    public void GetSuggestions_with_prev_promotes_bigram_match_to_top()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("하세요");
        dict.RecordWord("해달");
        for (int i = 0; i < 3; i++) dict.RecordBigram("안녕", "하세요");

        var withCtx = dict.GetSuggestions("하", "안녕", 5);
        Assert.Equal("하세요", withCtx[0]);
    }

    [Fact]
    public void GetSuggestions_new_bigram_candidate_is_inserted_but_capped()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("가족");
        dict.RecordBigram("우리", "가나다");
        var sugg = dict.GetSuggestions("가", "우리", 5);
        Assert.Contains("가나다", sugg);
        Assert.True(sugg.Count <= 5);
    }

    [Fact]
    public void RecordBigram_skips_when_either_side_is_single_syllable()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordBigram("가", "하세요");
        dict.RecordBigram("안녕", "해");
        Assert.Equal(0, dict.BigramStore.Count);
    }

    [Fact]
    public void RecordBigram_accepts_two_syllable_pair()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordBigram("안녕", "하세요");
        Assert.True(dict.BigramStore.Contains("안녕", "하세요"));
    }
}
