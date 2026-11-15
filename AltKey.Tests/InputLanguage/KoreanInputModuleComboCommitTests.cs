using AltKey.Models;
using AltKey.Platform;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// [콤보 취소] 조합키(Ctrl+A 등)가 OS에 전달되기 직전(InputService.ComboWillBeSent)
/// 한글 모듈이 조합 상태를 Esc처럼 취소하는 동작 테스트.
/// 대상 앱은 숏컷으로 전체 선택 같은 동작을 수행해 조합 글자의 화면 위치가 바뀔 수
/// 있으므로, 보내기 전에 취소를 해 "화면 꼬리 = 조합 상태" 전제를 유지한다.
/// 확정(FinalizeComposition)과 달리 학습·bigram 문맥에는 참여하지 않는다.
/// </summary>
public class KoreanInputModuleComboCommitTests : KoreanInputModuleTestBase
{
    private static KeySlot SpaceSlot => TestSlotFactory.Jamo(" ", null, VirtualKeyCode.VK_SPACE);

    /// <summary>
    /// 학습 여부를 관찰하려면 사전 참조가 필요해 베이스 팩토리와 달리 사전을 함께 반환한다.
    /// </summary>
    private static (KoreanInputModule Module, FakeInputService Input, KoreanDictionaryTestable Dict) CreateModuleWithDict()
    {
        var input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = true;
        return (new KoreanInputModule(input, koDict, enDict, config), input, koDict);
    }

    [Fact]
    public void ComboWillBeSent_cancels_hangul_composition()
    {
        var (module, input, dict) = CreateModuleWithDict();
        int wordsBefore = dict.UserWordCount;
        TestSlotFactory.FeedSyllables(module, "아", ctxNoModifiers);
        Assert.True(input.TrackedOnScreenLength > 0);

        int replacesBefore = input.AtomicReplaces.Count;
        input.FireComboWillBeSent([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_A]);

        // 조합이 취소되어 상태가 비어 있어야 한다.
        Assert.Equal("", module.CurrentWord);
        Assert.Equal(0, input.TrackedOnScreenLength);

        // 취소는 학습하지 않는다 — 확정(FinalizeComposition)과 다른 점.
        Assert.Equal(wordsBefore, dict.UserWordCount);
        Assert.Null(module.LastCommittedWord);

        // 취소한 뒤 백스페이스는 모듈이 처리하지 않고(raw) OS에 그대로 간다 —
        // 화면 재작성도 없다(선택 영역 삭제는 OS가 한다).
        bool handled = module.HandleKey(TestSlotFactory.Backspace(), ctxNoModifiers);
        Assert.False(handled);
        Assert.Equal(replacesBefore, input.AtomicReplaces.Count);
    }

    [Fact]
    public void ComboWillBeSent_cancels_quiet_english_prefix()
    {
        var module = CreateModule(out var input);
        module.ToggleSubmode();
        TestSlotFactory.FeedEnglish(module, "he", ctxNoModifiers);
        Assert.Equal("he", module.CurrentWord);

        input.FireComboWillBeSent([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_A]);

        Assert.Equal("", module.CurrentWord);
        Assert.Null(module.LastCommittedWord);
    }

    [Fact]
    public void ComboWillBeSent_without_composition_is_noop()
    {
        var module = CreateModule(out var input);

        // 조합이 없으면 아무 일도 일어나지 않는다(예외 없이 통과).
        input.FireComboWillBeSent([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_A]);

        Assert.Equal("", module.CurrentWord);
        Assert.Equal(0, input.TrackedOnScreenLength);
    }

    /// <summary>
    /// 제안 수락으로 pending 상태인 꼬리 공백이 있을 때 콤보가 오면,
    /// 그 공백은 실제 텍스트로 확정된다 — 플래그만 정리하고 환원 백스페이스는
    /// 남기지 않는다(콤보는 화면의 공백을 지우지 않으므로).
    /// </summary>
    [Fact]
    public void ComboWillBeSent_clears_pending_trailing_space_without_retraction()
    {
        var module = CreateModule(out var input);
        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.AcceptSuggestion("해달");
        Assert.True(module.HasPendingTrailingSpace);

        input.FireComboWillBeSent([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_A]);

        Assert.False(module.HasPendingTrailingSpace);
        Assert.Empty(input.BackspaceCounts);

        // 이후 구분자가 와도 오래된 플래그로 환원 백스페이스가 나가지 않아야 한다.
        module.HandleKey(SpaceSlot, ctxNoModifiers);
        Assert.Empty(input.BackspaceCounts);
    }
}
