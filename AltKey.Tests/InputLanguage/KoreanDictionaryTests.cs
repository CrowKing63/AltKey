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
    public void RecordWord_accepts_single_syllable()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("해");
        // 1음절 단어도 사용자 학습으로 저장된다.
        Assert.True(dict.UserStore.Contains("해"));
    }

    [Fact]
    public void RecordWord_rejects_word_containing_raw_jamo()
    {
        var dict = new KoreanDictionaryTestable();
        // "가"는 1음절이지만 독립 자모가 섞인 미완성 조합이므로 거부.
        dict.RecordWord("ㄱ가");
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
    public void GetSuggestions_includes_exact_saved_single_syllable()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("가");

        var initialSuggestions = dict.GetSuggestions("ㄱ", 20);
        var completedSuggestions = dict.GetSuggestions("가", 20);

        Assert.Contains("가", initialSuggestions);
        Assert.Contains("가", completedSuggestions);
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
    public void GetSuggestions_includes_words_with_valid_compound_vowels_from_last_syllable()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("괴물");
        dict.RecordWord("괜찮을");
        dict.RecordWord("고양이");
        dict.RecordWord("구름");

        var suggestions = dict.GetSuggestions("고", 10).ToList();

        Assert.Contains("괴물", suggestions);
        Assert.Contains("괜찮을", suggestions);
        Assert.DoesNotContain("구름", suggestions);
        Assert.True(suggestions.Count <= 10);
        Assert.True(suggestions.IndexOf("고양이") < suggestions.IndexOf("괴물"));
    }

    [Fact]
    public void GetSuggestions_includes_consonant_extensions_only_for_last_syllable()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("가나무");
        dict.RecordWord("가낙다");
        dict.RecordWord("가난다");
        dict.RecordWord("고나다");

        var suggestions = dict.GetSuggestions("가나", 10);

        Assert.Contains("가나무", suggestions);
        Assert.Contains("가낙다", suggestions);
        Assert.Contains("가난다", suggestions);
        Assert.DoesNotContain("고나다", suggestions);
    }

    [Fact]
    public void GetSuggestions_with_simple_coda_includes_vowel_moved_coda_continuations()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("부분이");
        dict.RecordWord("부자");

        var suggestions = dict.GetSuggestions("붑", 10);

        Assert.Contains("부분이", suggestions);
        Assert.DoesNotContain("부자", suggestions);
    }

    [Fact]
    public void GetSuggestions_with_compound_final_does_not_suggest_unrelated_final_consonants()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("업무");
        dict.RecordWord("얼굴");
        dict.RecordWord("언제");

        var suggestions = dict.GetSuggestions("얹", 10);

        Assert.Contains("언제", suggestions);
        Assert.DoesNotContain("업무", suggestions);
        Assert.DoesNotContain("얼굴", suggestions);
    }

    [Fact]
    public void GetSuggestions_matches_a_choseong_after_completed_syllables()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("깃허브");
        dict.RecordWord("깃가나다");
        dict.RecordWord("빗허브");

        var suggestions = dict.GetSuggestions("깃ㅎ", 10);

        Assert.Contains("깃허브", suggestions);
        Assert.DoesNotContain("깃한글", suggestions);
        Assert.DoesNotContain("빗허브", suggestions);
    }

    [Fact]
    public void GetSuggestions_sorts_exact_and_expanded_user_words_by_frequency_with_exact_tie_break()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordWord("가나다");
        dict.RecordWord("가나다");
        dict.RecordWord("각별히");
        dict.RecordWord("각별히");
        dict.RecordWord("각별히");
        dict.RecordWord("각별히");
        dict.RecordWord("간다");
        dict.RecordWord("간다");

        var suggestions = dict.GetSuggestions("가", 20).ToList();

        Assert.True(suggestions.IndexOf("각별히") < suggestions.IndexOf("가나다"));
        Assert.True(suggestions.IndexOf("가나다") < suggestions.IndexOf("간다"));
    }

    [Fact]
    public void GetSuggestions_with_zero_count_returns_no_candidates()
    {
        var dict = new KoreanDictionaryTestable();

        Assert.Empty(dict.GetSuggestions("고", 0));
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
    public void RecordBigram_rejects_jamo_only_or_raw_jamo_sides()
    {
        var dict = new KoreanDictionaryTestable();
        // "ㄱ"는 음절이 없고, "ㄱ가"는 독립 자모가 섞였으므로 둘 다 거부.
        dict.RecordBigram("ㄱ", "안녕");
        dict.RecordBigram("안녕", "ㄱ가");
        Assert.Equal(0, dict.BigramStore.Count);
    }

    [Fact]
    public void RecordBigram_accepts_single_syllable_pair()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordBigram("가", "하세요");
        Assert.True(dict.BigramStore.Contains("가", "하세요"));
    }

    [Fact]
    public void RecordBigram_accepts_two_syllable_pair()
    {
        var dict = new KoreanDictionaryTestable();
        dict.RecordBigram("안녕", "하세요");
        Assert.True(dict.BigramStore.Contains("안녕", "하세요"));
    }
}
