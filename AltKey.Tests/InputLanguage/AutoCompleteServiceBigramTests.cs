using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

public class AutoCompleteServiceBigramTests
{
    private static KeyContext ctxNoModifiers => new(false, false, false, InputMode.Unicode, 0);
    private static KeyContext ctxEnglish => new(false, false, false, InputMode.Unicode, 0);

    private (AutoCompleteService service, KoreanInputModule module, KoreanDictionaryTestable koDict, EnglishDictionaryTestable enDict) Build(bool enabled = true)
    {
        var input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = enabled;
        var module = new KoreanInputModule(input, koDict, enDict, config);
        var service = new AutoCompleteService(module, koDict, enDict, config);
        return (service, module, koDict, enDict);
    }

    /// 자동완성 수락 후 공백 없이 즉시 바이그램 추천이 노출되는지 확인합니다.
    [Fact]
    public void AcceptSuggestion_shows_bigram_immediately()
    {
        var (service, module, koDict, _) = Build();

        // 바이그램 데이터 선행 학습
        koDict.BigramStore.Record("테스트", "추천");
        koDict.BigramStore.Record("테스트", "추가");

        // 조합 중인 단어 준비
        TestSlotFactory.FeedSyllables(module, "테스트", ctxNoModifiers);

        // 이벤트 캡처
        (IReadOnlyList<string> list, SuggestionMode mode)? captured = null;
        service.SuggestionsChanged += (list, mode) => captured = (list, mode);

        // 자동완성 수락
        service.AcceptSuggestion("테스트");

        Assert.NotNull(captured);
        Assert.Equal(SuggestionMode.Bigram, captured!.Value.mode);
        Assert.Contains("추천", captured.Value.list);
        Assert.Contains("추가", captured.Value.list);
    }

    /// 바이그램 수락 시 쌍이 Record되고 _lastCommittedWord가 갱신되는지 확인합니다.
    [Fact]
    public void AcceptBigramSuggestion_records_pair_and_updates_context()
    {
        var (service, module, koDict, _) = Build();

        // 선행 단어 수락으로 _lastCommittedWord 설정
        TestSlotFactory.FeedSyllables(module, "테스트", ctxNoModifiers);
        service.AcceptSuggestion("테스트");

        // 바이그램 데이터 선행 학습 (연속 추천용)
        koDict.BigramStore.Record("테스트", "확인");

        // 바이그램 수락 → ("테스트", "확인") Record 확인
        var result = service.AcceptBigramSuggestion("확인");

        Assert.Equal(" 확인", result);
        Assert.True(koDict.BigramStore.Contains("테스트", "확인"));
    }

    /// 바이그램 수락 후 수동 공백(OnSeparator)에서 이중 Record가 발생하지 않는지 확인합니다.
    [Fact]
    public void OnSeparator_after_bigram_accept_does_not_duplicate_record()
    {
        var (service, module, koDict, _) = Build();

        // 선행 단어 수락
        TestSlotFactory.FeedSyllables(module, "테스트", ctxNoModifiers);
        service.AcceptSuggestion("테스트");

        // 바이그램 수락 → ("테스트", "확인") Record
        service.AcceptBigramSuggestion("확인");

        // 바이그램 수락 후 CurrentWord는 비어 있어야 함
        // 수동 공백이 OnSeparator를 호출해도 Record는 한 쌍만 유지
        service.OnSeparator();

        Assert.Equal(1, koDict.BigramStore.NextCountFor("테스트"));
        Assert.Equal(1, koDict.BigramStore.Count);
    }

    /// 두 단어 연속 입력 시 bigram 빈도가 정확히 1인지를 확인합니다.
    /// (과거 AutoCompleteService.OnSeparator의 직접 Record 경로가 빈도를 2배로 세던 버그 방지)
    [Fact]
    public void OnSeparator_records_bigram_frequency_exactly_once()
    {
        var (service, module, koDict, _) = Build();

        TestSlotFactory.FeedSyllables(module, "안녕", ctxNoModifiers);
        service.OnSeparator(); // "안녕" 확정 — prev 없음 → 쌍 기록 없음

        TestSlotFactory.FeedSyllables(module, "하세요", ctxNoModifiers);
        service.OnSeparator(); // "하세요" 확정 — prev "안녕" → 정확히 1회 Record

        var nexts = koDict.BigramStore.GetNexts("안녕", "");
        Assert.Single(nexts);
        Assert.Equal(1, nexts[0].Count);
    }

    /// 바이그램 수락 시 단어 빈도와 bigram 쌍이 사전 경유로 기록되는지 확인합니다.
    [Fact]
    public void AcceptBigramSuggestion_records_word_and_pair()
    {
        var (service, module, koDict, _) = Build();

        TestSlotFactory.FeedSyllables(module, "테스트", ctxNoModifiers);
        service.AcceptSuggestion("테스트");

        service.AcceptBigramSuggestion("확인");

        Assert.True(koDict.UserStore.Contains("확인")); // 수락 자체가 단어 빈도 기록
        var nexts = koDict.BigramStore.GetNexts("테스트", "");
        Assert.Single(nexts);
        Assert.Equal(1, nexts[0].Count);
    }

    /// 자동완성 OFF 상태에서는 바이그램 수락이 아무것도 기록하지 않는지 확인합니다.
    [Fact]
    public void AcceptBigramSuggestion_with_learning_off_records_nothing()
    {
        var (service, module, koDict, _) = Build(enabled: false);

        TestSlotFactory.FeedSyllables(module, "테스트", ctxNoModifiers);
        service.AcceptSuggestion("테스트"); // 설정 OFF → 단어 미기록
        koDict.BigramStore.Record("테스트", "확인"); // 직접 선행 학습 (빈도 1)

        service.AcceptBigramSuggestion("확인");

        Assert.Equal(0, koDict.UserWordCount);
        var nexts = koDict.BigramStore.GetNexts("테스트", "");
        Assert.Single(nexts);
        Assert.Equal(1, nexts[0].Count); // 수락으로 추가 기록이 없어야 함
    }
}
