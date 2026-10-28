using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.Linq;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// KoreanInputModule 제안/학습 테스트: AcceptSuggestion, bigram 문맥, 사용자 사전 학습.
/// </summary>
public class KoreanInputModuleSuggestionTests : KoreanInputModuleTestBase
{
    [Fact]
    public void AcceptSuggestion_in_HangulJamo_Unicode_returns_onscreen_char_count()
    {
        var module = CreateModule(out _);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var (bs, word) = module.AcceptSuggestion("해달");

        Assert.Equal(1, bs);
        Assert.Equal("해달", word);
    }

    [Fact]
    public void AcceptSuggestion_in_HangulJamo_VirtualKey_returns_jamo_depth()
    {
        var module = CreateModule(out var input);
        Assert.True(input.TrySetMode(InputMode.VirtualKey));

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var (bs, word) = module.AcceptSuggestion("해달");

        Assert.Equal(2, bs);
        Assert.Equal("해달", word);
    }

    [Fact]
    public void AcceptSuggestion_then_new_jamo_preserves_accepted_word_on_screen()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var (bs, word) = module.AcceptSuggestion("해달");
        input.SendAtomicReplace(bs, word);
        input.ResetTrackedLength();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);

        var last = input.AtomicReplaces.Last();
        Assert.Equal(0, last.prevLen);
        Assert.Equal("ㅎ", last.next);
    }

    [Fact]
    public void ToggleSubmode_during_composition_learns_word()
    {
        var (module, _, dict) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);

        module.ToggleSubmode();

        Assert.Contains("해달", dict.GetSuggestions("해", 10));
    }

    [Fact]
    public void OnSeparator_with_empty_composition_does_not_record()
    {
        var (module, _, dict) = TestSlotFactory.CreateModuleWithInput();

        module.OnSeparator();

        Assert.Empty(dict.GetSuggestions("", 10));
    }

    [Fact]
    public void OnSeparator_with_multi_syllable_records_user_dictionary()
    {
        var (module, _, dict) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);
        module.OnSeparator();

        Assert.Contains("해달", dict.GetSuggestions("해", 5));
    }

    [Fact]
    public void FinalizeComposition_With_AutoCompleteDisabled_Does_Not_Record_Word()
    {
        var (module, _, _) = TestSlotFactory.CreateModuleWithInput(autoCompleteEnabled: false);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);
        module.OnSeparator();

        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void FinalizeComposition_With_AutoCompleteEnabled_Records_Word()
    {
        var (module, _, dict) = TestSlotFactory.CreateModuleWithInput(autoCompleteEnabled: true);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);
        module.OnSeparator();

        Assert.Contains("해달", dict.GetSuggestions("해", 5));
    }

    [Fact]
    public void Bigram_recorded_on_separator_after_previous_finalize()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.OnSeparator();

        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.OnSeparator();

        Assert.True(koDict.BigramStore.Contains("가나", "다라"));
    }

    [Fact]
    public void Bigram_recorded_on_AcceptSuggestion()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅕ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.OnSeparator();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.AcceptSuggestion("하세요");

        Assert.True(koDict.BigramStore.Contains("안녕", "하세요"));
    }

    [Fact]
    public void Context_is_used_in_GetSuggestions_call_path()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        for (int i = 0; i < 3; i++) koDict.RecordBigram("안녕", "하세요");
        koDict.RecordWord("하세요");
        koDict.RecordWord("해달");

        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅕ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.OnSeparator();

        IReadOnlyList<string>? captured = null;
        module.SuggestionsChanged += list => captured = list;

        module.HandleKey(ㅎ_slot, ctxNoModifiers);

        Assert.NotNull(captured);
        Assert.Contains("하세요", captured!);
    }

    [Fact]
    public void CancelComposition_resets_lastCommittedWord_and_suggestionContext()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.OnSeparator();

        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.CancelComposition();

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.OnSeparator();

        Assert.False(koDict.BigramStore.Contains("다라", "가나"));
    }

    [Fact]
    public void AcceptSuggestion_then_next_word_no_bigram_context()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        for (int i = 0; i < 3; i++) koDict.RecordBigram("안녕", "하세요");
        koDict.RecordWord("하세요");

        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅕ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.OnSeparator();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.AcceptSuggestion("하세요");

        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.OnSeparator();

        Assert.False(koDict.BigramStore.Contains("하세요", "아앙"));
    }

    [Fact]
    public void CommitCurrentWord_then_next_word_no_bigram_context()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        module.HandleKey(ㅅ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);
        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.CommitCurrentWord();

        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);
        module.OnSeparator();

        Assert.False(koDict.BigramStore.Contains("시간", "이"));
    }

    [Fact]
    public void FinalizeComposition_then_next_word_has_bigram_context()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        module.HandleKey(ㅅ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);
        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.OnSeparator();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);
        module.OnSeparator();

        Assert.True(koDict.BigramStore.Contains("시간", "해달"));
    }

    [Fact]
    public void CancelComposition_does_not_learn_word()
    {
        var (module, _, dict) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅅ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);
        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.CancelComposition();

        Assert.Equal(0, dict.UserWordCount);
    }

    [Fact]
    public void AcceptSuggestion_then_type_new_word_with_realistic_tracked_length()
    {
        var (module, input, koDict) = TestSlotFactory.CreateModuleWithInput();

        // "친척" = ㅊ ㅣ ㄴ ㅊ ㅓ ㄱ — 매 키마다 TrackedOnScreenLength 반영
        module.HandleKey(ㅊ_slot, CtxFromInput(input));
        module.HandleKey(ㅣ_slot, CtxFromInput(input));
        module.HandleKey(ㄴ_slot, CtxFromInput(input));
        module.HandleKey(ㅊ_slot, CtxFromInput(input));
        module.HandleKey(ㅓ_slot, CtxFromInput(input));
        module.HandleKey(ㄱ_slot, CtxFromInput(input));

        Assert.Equal("친척", module.CurrentWord);

        // AcceptSuggestion("친척")
        var (bs, word) = module.AcceptSuggestion("친척");
        input.SendAtomicReplace(bs, word);
        input.ResetTrackedLength();

        Assert.Equal("", module.CurrentWord);

        // "ㅇ" 입력 — TrackedOnScreenLength = 0 (리셋된 상태)
        module.HandleKey(ㅇ_slot, CtxFromInput(input));
        Assert.Equal("ㅇ", module.CurrentWord);

        // "ㅔ" → "에"
        module.HandleKey(ㅔ_slot, CtxFromInput(input));
        Assert.Equal("에", module.CurrentWord);

        // "ㄱ" → 종성 → "엑"
        module.HandleKey(ㄱ_slot, CtxFromInput(input));
        Assert.Equal("엑", module.CurrentWord);

        // "ㅔ" → 종성→초성 전이 → "에게"
        module.HandleKey(ㅔ_slot, CtxFromInput(input));
        Assert.Equal("에게", module.CurrentWord);

        // 스페이스
        module.OnSeparator();

        Assert.DoesNotContain("친척에게", koDict.GetSuggestions("친척", 10));
    }

    /// <summary>
    /// 단어 구분자(공백) 직후에도 bigram 제안이 떠야 한다.
    /// 예: "가나" AcceptSuggestion → 공백 → "다라" bigram 제안 표시
    /// </summary>
    [Fact]
    public void Separator_after_AcceptSuggestion_shows_bigram_suggestions()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        for (int i = 0; i < 3; i++) koDict.RecordBigram("가나", "다라");
        koDict.RecordWord("다라");

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.AcceptSuggestion("가나");

        IReadOnlyList<string>? captured = null;
        module.SuggestionsChanged += list => captured = list;

        module.OnSeparator();

        Assert.NotNull(captured);
        Assert.Contains("다라", captured!);
    }
}
