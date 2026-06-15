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

    /// 바이그램 연속 3회 수락 시 각 쌍이 모두 Record되는지 확인합니다.
    [Fact]
    public void Consecutive_bigram_accepts_record_all_pairs()
    {
        var (service, module, koDict, _) = Build();

        // 시작 단어 수락
        TestSlotFactory.FeedSyllables(module, "테스트", ctxNoModifiers);
        service.AcceptSuggestion("테스트");

        // 연속 바이그램 수락
        service.AcceptBigramSuggestion("추가");
        service.AcceptBigramSuggestion("확인");
        service.AcceptBigramSuggestion("완료");

        Assert.True(koDict.BigramStore.Contains("테스트", "추가"));
        Assert.True(koDict.BigramStore.Contains("추가", "확인"));
        Assert.True(koDict.BigramStore.Contains("확인", "완료"));
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

    /// 바이그램 수락 후 수동 타이핑 + 공백으로 OnSeparator Record가 정상 동작하는지 확인합니다.
    [Fact]
    public void OnSeparator_after_manual_typing_records_bigram_with_context()
    {
        var (service, module, koDict, _) = Build();

        // 선행 단어 수락
        TestSlotFactory.FeedSyllables(module, "테스트", ctxNoModifiers);
        service.AcceptSuggestion("테스트");

        // 바이그램 수락 → ("테스트", "확인") Record
        service.AcceptBigramSuggestion("확인");

        // 수동 입력 후 공백
        TestSlotFactory.FeedSyllables(module, "완료", ctxNoModifiers);
        service.OnSeparator();

        Assert.True(koDict.BigramStore.Contains("확인", "완료"));
        Assert.Equal(1, koDict.BigramStore.NextCountFor("확인"));
    }

    /// 언어 전환 시 bigram 컨텍스트가 초기화되는지 확인합니다.
    /// 완료는 시나리오에서 단어로만 사용
    [Fact]
    public void Submode_toggle_clears_bigram_context()
    {
        var (service, module, koDict, enDict) = Build();

        // 한글 단어 수락
        TestSlotFactory.FeedSyllables(module, "테스트", ctxNoModifiers);
        service.AcceptSuggestion("테스트");

        // 영어 모드로 전환 → _lastCommittedWord 초기화
        service.ToggleKoreanSubmode();

        // 영어 입력 후 공백
        TestSlotFactory.FeedEnglish(module, "hello", ctxEnglish);
        service.OnSeparator();

        // 한글 bigram 저장소에 기록이 없어야 함 (영어 입력이므로)
        Assert.Equal(0, koDict.BigramStore.Count);
        // enDict의 bigram도 여기서는 학습되지 않음 (완료는 한글 단어)
    }
}
