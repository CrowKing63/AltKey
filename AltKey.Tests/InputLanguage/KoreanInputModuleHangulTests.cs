using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.Linq;

namespace AltKey.Tests.InputLanguage;

public class KoreanInputModuleHangulTests : KoreanInputModuleTestBase
{
    [Fact]
    public void Feed_해_해_separator_records_해()
    {
        var module = CreateModule(out var input);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        Assert.Equal("해", module.CurrentWord);

        module.OnSeparator();
        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void 해_plus_Shift_ㅆ_feeds_ssang_siot_not_T()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctxShiftOnly);

        Assert.DoesNotContain("T", module.CurrentWord);
        Assert.True(module.CurrentWord is "했" or "해ㅆ");
    }

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
    public void Feed_ㄷ_ㅏ_ㄹ_ㄱ_composes_닭()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);
        module.HandleKey(ㄱ_slot, ctxNoModifiers);

        Assert.Equal("닭", module.CurrentWord);
    }

    [Fact]
    public void Feed_ㄱ_ㅏ_ㅇ_ㅣ_composes_가_then_이()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);

        Assert.Equal("가이", module.CurrentWord);
    }

    [Fact]
    public void Feed_ㅇ_ㅏ_ㄴ_ㄴ_ㅕ_ㅇ_composes_안녕()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅕ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);

        Assert.Equal("안녕", module.CurrentWord);
    }

    [Fact]
    public void SsangConsonants_via_ShiftLabel_compose_correctly()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅃ_slot, ctxShiftOnly);
        Assert.Contains("ㅃ", module.CurrentWord);

        module.HandleKey(ㅉ_slot, ctxShiftOnly);
        Assert.Contains("ㅉ", module.CurrentWord);

        module.HandleKey(ㄸ_slot, ctxShiftOnly);
        Assert.Contains("ㄸ", module.CurrentWord);

        module.HandleKey(ㄲ_slot, ctxShiftOnly);
        Assert.Contains("ㄲ", module.CurrentWord);

        module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctxShiftOnly);
        Assert.Contains("ㅆ", module.CurrentWord);
    }

    [Fact]
    public void SsangVowels_via_ShiftLabel_compose_correctly()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅒ_slot, ctxShiftOnly);
        Assert.Contains("ㅒ", module.CurrentWord);

        module.HandleKey(ㅖ_slot, ctxShiftOnly);
        Assert.Contains("ㅖ", module.CurrentWord);
    }

    [Fact]
    public void Reset_clears_all_state()
    {
        var module = CreateModule(out _);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        module.Reset();

        Assert.Equal("", module.CurrentWord);
        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);
    }

    [Fact]
    public void ToggleSubmode_back_to_HangulJamo_resets_correctly()
    {
        var module = CreateModule(out _);
        module.ToggleSubmode();
        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);

        module.ToggleSubmode();
        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);
        Assert.Equal("가", module.ComposeStateLabel);
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
    public void AcceptSuggestion_With_AutoCompleteDisabled_Does_Not_Record()
    {
        var (module, _, _) = TestSlotFactory.CreateModuleWithInput(autoCompleteEnabled: false);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var (bs, word) = module.AcceptSuggestion("해달");

        Assert.Equal(1, bs);
        Assert.Equal("해달", word);
        Assert.Equal("", module.CurrentWord);
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
    public void LastCommittedWord_resets_after_ToggleSubmode()
    {
        var (module, _, koDict, enDict) = TestSlotFactory.CreateModuleWithBothDicts();

        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);
        module.HandleKey(ㅕ_slot, ctxNoModifiers);
        module.HandleKey(ㅇ_slot, ctxNoModifiers);
        module.OnSeparator();

        module.ToggleSubmode();

        var ctxEnglish = new KeyContext(false, false, false, InputMode.Unicode, 0);
        TestSlotFactory.FeedEnglish(module, "hello", ctxEnglish);
        module.OnSeparator();

        Assert.Equal(0, enDict.BigramStore.Count);
    }

    [Fact]
    public void Bigram_not_recorded_when_autoComplete_disabled()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts(autoCompleteEnabled: false);

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

        Assert.Equal(0, koDict.BigramStore.Count);
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
    public void ToggleSubmode_clears_context_for_next_suggestions()
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

        module.ToggleSubmode();
        module.ToggleSubmode();

        IReadOnlyList<string>? captured = null;
        module.SuggestionsChanged += list => captured = list;

        module.HandleKey(ㅎ_slot, ctxNoModifiers);

        Assert.NotNull(captured);
    }

    [Fact]
    public void CancelComposition_resets_composer_and_tracked_length()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅅ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);
        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);

        Assert.Equal("시간", module.CurrentWord);

        module.CancelComposition();

        Assert.Equal("", module.CurrentWord);
        Assert.Equal(0, input.TrackedOnScreenLength);
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

    [Fact]
    public void DEBUG_AcceptSuggestion_then_type_new_word()
    {
        var (module, input, koDict) = TestSlotFactory.CreateModuleWithInput();

        // 1. "시간" 입력
        module.HandleKey(ㅅ_slot, ctxNoModifiers);
        module.HandleKey(ㅣ_slot, ctxNoModifiers);
        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄴ_slot, ctxNoModifiers);

        Assert.Equal("시간", module.CurrentWord);

        // 2. AcceptSuggestion("시간")
        var (bs, word) = module.AcceptSuggestion("시간");
        input.SendAtomicReplace(bs, word);
        input.ResetTrackedLength();

        // 3. "이" 입력
        module.HandleKey(TestSlotFactory.Jamo("ㅇ", null, VirtualKeyCode.VK_D), ctxNoModifiers);
        module.HandleKey(TestSlotFactory.Jamo("ㅣ", null, VirtualKeyCode.VK_I), ctxNoModifiers);

        // 이 시점 CurrentWord가 "이"여야 함
        Assert.Equal("이", module.CurrentWord);

        // 4. 스페이스
        module.OnSeparator();

        // "시간이"는 학습되지 않아야 함
        Assert.DoesNotContain("시간이", koDict.GetSuggestions("시간", 5));
    }

    [Fact]
    public void CommitCurrentWord_resets_composer_and_tracked_length()
    {
        var (module, input, koDict) = TestSlotFactory.CreateModuleWithInput();

        // "친척" 입력 — 매 키마다 TrackedOnScreenLength 반영
        module.HandleKey(ㅊ_slot, CtxFromInput(input));
        module.HandleKey(ㅣ_slot, CtxFromInput(input));
        module.HandleKey(ㄴ_slot, CtxFromInput(input));
        module.HandleKey(ㅊ_slot, CtxFromInput(input));
        module.HandleKey(ㅓ_slot, CtxFromInput(input));
        module.HandleKey(ㄱ_slot, CtxFromInput(input));

        Assert.Equal("친척", module.CurrentWord);

        // CommitCurrentWord (첫 번째 슬롯 클릭)
        module.CommitCurrentWord();

        // composer가 리셋되었는지 확인
        Assert.Equal("", module.CurrentWord);
        Assert.Equal(0, input.TrackedOnScreenLength);

        // "ㅇ" 입력 — 이전 단어가 남아있지 않아야 함
        module.HandleKey(ㅇ_slot, CtxFromInput(input));
        Assert.Equal("ㅇ", module.CurrentWord);

        // "에게" 완성
        module.HandleKey(ㅔ_slot, CtxFromInput(input));
        module.HandleKey(ㄱ_slot, CtxFromInput(input));
        module.HandleKey(ㅔ_slot, CtxFromInput(input));
        Assert.Equal("에게", module.CurrentWord);

        // 스페이스
        module.OnSeparator();

        Assert.DoesNotContain("친척에게", koDict.GetSuggestions("친척", 10));
    }

    [Fact]
    public void Feed_exclamation_mark_records_word()
    {
        var (module, _, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        
        var exclamationSlot = TestSlotFactory.Symbol("1", "!", VirtualKeyCode.VK_1);
        module.HandleKey(exclamationSlot, ctxShiftOnly);

        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void Feed_number_one_without_shift_does_not_record_word()
    {
        var (module, _, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var exclamationSlot = TestSlotFactory.Symbol("1", "!", VirtualKeyCode.VK_1);
        module.HandleKey(exclamationSlot, ctxNoModifiers);

        Assert.Equal("해", module.CurrentWord);
    }

    [Fact]
    public void Feed_quotes_records_word()
    {
        var (module, _, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        var quoteSlot = TestSlotFactory.Symbol("'", "\"", VirtualKeyCode.VK_OEM_7);
        module.HandleKey(quoteSlot, ctxNoModifiers);

        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void Feed_new_punctuation_records_word()
    {
        var keysToTest = new[]
        {
            (vk: VirtualKeyCode.VK_OEM_4, isShifted: false), // [
            (vk: VirtualKeyCode.VK_OEM_4, isShifted: true),  // {
            (vk: VirtualKeyCode.VK_OEM_6, isShifted: false), // ]
            (vk: VirtualKeyCode.VK_OEM_6, isShifted: true),  // }
            (vk: VirtualKeyCode.VK_OEM_1, isShifted: false), // ;
            (vk: VirtualKeyCode.VK_OEM_1, isShifted: true),  // :
            (vk: VirtualKeyCode.VK_9, isShifted: true),      // (
            (vk: VirtualKeyCode.VK_0, isShifted: true),      // )
            (vk: VirtualKeyCode.VK_ESCAPE, isShifted: false), // ESC
            (vk: VirtualKeyCode.VK_DELETE, isShifted: false)  // Delete
        };

        foreach (var (vk, isShifted) in keysToTest)
        {
            var (module, _, _) = TestSlotFactory.CreateModuleWithInput();
            module.HandleKey(ㅎ_slot, ctxNoModifiers);
            module.HandleKey(ㅐ_slot, ctxNoModifiers);
            
            var slot = TestSlotFactory.Symbol("test", "test", vk);
            module.HandleKey(slot, isShifted ? ctxShiftOnly : ctxNoModifiers);
            
            Assert.Equal("", module.CurrentWord);
        }
    }
}