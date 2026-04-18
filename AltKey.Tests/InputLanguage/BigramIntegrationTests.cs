using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.IO;
using Xunit;

namespace AltKey.Tests.InputLanguage;

public class BigramIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public BigramIntegrationTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("altkey-bigram-integration").FullName;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private (KoreanInputModule module, KoreanDictionary koDict, FakeInputService input) Build(bool enabled = true)
    {
        var koStore = new WordFrequencyStore(_tempDir, "ko");
        var enStore = new WordFrequencyStore(_tempDir, "en");
        var koBigram = new BigramFrequencyStore(_tempDir, "ko");
        var enBigram = new BigramFrequencyStore(_tempDir, "en");
        var koDict = new KoreanDictionary(_ => koStore, _ => koBigram);
        var enDict = new EnglishDictionary(_ => enStore, _ => enBigram);
        var input = new FakeInputService();
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = enabled;
        return (new KoreanInputModule(input, koDict, enDict, config), koDict, input);
    }

    private static KeyContext ctxNoModifiers => new(false, false, false, InputMode.Unicode, 0);
    private static KeyContext ctxEnglish => new(false, false, false, InputMode.Unicode, 0);

    [Fact]
    public void Round_trip_bigram_survives_process_restart()
    {
        var (module, koDict, _) = Build();

        // 한 문장 타이핑 시뮬레이션
        TestSlotFactory.FeedSyllables(module, "안녕", ctxNoModifiers);
        module.OnSeparator();
        TestSlotFactory.FeedSyllables(module, "하세요", ctxNoModifiers);
        module.OnSeparator();

        koDict.Flush();

        // 두번째 인스턴스로 재시작 시뮬레이션
        var koBigram2 = new BigramFrequencyStore(_tempDir, "ko");
        Assert.True(koBigram2.Contains("안녕", "하세요"));
    }

    [Fact]
    public void Suggestion_list_reflects_context_after_finalize()
    {
        var (module, koDict, _) = Build();

        // 학습 데이터 축적
        for (int i = 0; i < 3; i++)
        {
            TestSlotFactory.FeedSyllables(module, "안녕", ctxNoModifiers);
            module.OnSeparator();
            TestSlotFactory.FeedSyllables(module, "하세요", ctxNoModifiers);
            module.OnSeparator();
        }

        // 새 문장: "안녕␣ㅎ"
        TestSlotFactory.FeedSyllables(module, "안녕", ctxNoModifiers);
        module.OnSeparator();

        IReadOnlyList<string>? captured = null;
        module.SuggestionsChanged += list => captured = list;

        var ㅎ_slot = TestSlotFactory.Jamo("ㅎ", null, VirtualKeyCode.VK_H);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);

        Assert.NotNull(captured);
        Assert.Contains("하세요", captured!);
    }

    [Fact]
    public void Toggle_off_then_input_does_not_persist_bigrams()
    {
        var (module, koDict, _) = Build(enabled: false);

        TestSlotFactory.FeedSyllables(module, "안녕", ctxNoModifiers);
        module.OnSeparator();
        TestSlotFactory.FeedSyllables(module, "하세요", ctxNoModifiers);
        module.OnSeparator();

        koDict.Flush();

        Assert.Equal(0, koDict.BigramStore.Count);
    }

    [Fact]
    public void Submode_toggle_prevents_cross_language_bigram()
    {
        var (module, koDict, _) = Build();

        TestSlotFactory.FeedSyllables(module, "안녕", ctxNoModifiers);
        module.OnSeparator();
        module.ToggleSubmode();       // 영어로 전환
        TestSlotFactory.FeedEnglish(module, "hello", ctxEnglish);
        module.OnSeparator();

        Assert.Equal(0, koDict.BigramStore.Count);
    }
}
