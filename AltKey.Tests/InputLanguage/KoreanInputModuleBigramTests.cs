using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.Linq;

namespace AltKey.Tests.InputLanguage;

public class KoreanInputModuleBigramBugTests : KoreanInputModuleTestBase
{
    /// <summary>
    /// 버그 재현 테스트:
    /// "그럼에도" [Space] "불구하고" [Space] 입력 시 (그럼에도, 불구하고) 저장.
    /// "그럼에도" [불구하고 수락] [Enter] 입력 후 다시 "그럼에도" [Space] 입력 시
    /// (불구하고, 그럼에도)가 역으로 저장되지 않아야 함.
    /// </summary>
    [Fact]
    public void Enter_after_AcceptSuggestion_clears_bigram_context_for_recording()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        // 1. "그럼에도" [Space] "불구하고" [Space] -> (그럼에도, 불구하고) 저장
        TestSlotFactory.FeedSyllables(module, "그럼에도", ctxNoModifiers);
        module.OnSeparator(); // Space (keepContextForBigram: true)
        TestSlotFactory.FeedSyllables(module, "불구하고", ctxNoModifiers);
        module.OnSeparator(); // Space
        Assert.True(koDict.BigramStore.Contains("그럼에도", "불구하고"));

        // 2. "그럼에도" 입력 -> "불구하고" 추천 수락
        TestSlotFactory.FeedSyllables(module, "그럼에도", ctxNoModifiers);
        module.AcceptSuggestion("불구하고");

        // 3. [Enter] 입력 -> context reset
        // 수정된 로직에서는 Enter가 HandleKey를 통해 FinalizeComposition(false)를 호출해야 함
        var enterSlot = TestSlotFactory.Symbol("enter", "enter", VirtualKeyCode.VK_RETURN);
        module.HandleKey(enterSlot, ctxNoModifiers);

        // 4. "그럼에도" [Space] 입력
        TestSlotFactory.FeedSyllables(module, "그럼에도", ctxNoModifiers);
        module.OnSeparator();

        // 5. (불구하고, 그럼에도)가 저장되지 않아야 함
        Assert.False(koDict.BigramStore.Contains("불구하고", "그럼에도"));
    }

    [Fact]
    public void Tab_after_word_clears_bigram_context()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        TestSlotFactory.FeedSyllables(module, "단어1", ctxNoModifiers);
        
        var tabSlot = TestSlotFactory.Symbol("tab", "tab", VirtualKeyCode.VK_TAB);
        module.HandleKey(tabSlot, ctxNoModifiers);

        TestSlotFactory.FeedSyllables(module, "단어2", ctxNoModifiers);
        module.OnSeparator();

        Assert.False(koDict.BigramStore.Contains("단어1", "단어2"));
    }

    [Fact]
    public void Period_after_word_clears_bigram_context()
    {
        var (module, _, koDict, _) = TestSlotFactory.CreateModuleWithBothDicts();

        TestSlotFactory.FeedSyllables(module, "단어1", ctxNoModifiers);
        
        var periodSlot = TestSlotFactory.Symbol(".", ".", VirtualKeyCode.VK_OEM_PERIOD);
        module.HandleKey(periodSlot, ctxNoModifiers);

        TestSlotFactory.FeedSyllables(module, "단어2", ctxNoModifiers);
        module.OnSeparator();

        Assert.False(koDict.BigramStore.Contains("단어1", "단어2"));
    }
}
