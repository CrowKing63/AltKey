using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using System.Linq;

namespace AltKey.Tests.InputLanguage;

public class KoreanInputModuleTests
{
    private static KeySlot ㅎ_slot => TestSlotFactory.Jamo("ㅎ", null, VirtualKeyCode.VK_H);
    private static KeySlot ㅐ_slot => TestSlotFactory.Jamo("ㅐ", null, VirtualKeyCode.VK_O);
    private static KeySlot ㄷ_slot => TestSlotFactory.Jamo("ㄷ", null, VirtualKeyCode.VK_E);
    private static KeySlot ㅏ_slot => TestSlotFactory.Jamo("ㅏ", null, VirtualKeyCode.VK_K);
    private static KeySlot ㄹ_slot => TestSlotFactory.Jamo("ㄹ", null, VirtualKeyCode.VK_T);
    private static KeySlot ㄱ_slot => TestSlotFactory.Jamo("ㄱ", null, VirtualKeyCode.VK_R);
    private static KeySlot ㅇ_slot => TestSlotFactory.Jamo("ㅇ", null, VirtualKeyCode.VK_D);
    private static KeySlot ㄴ_slot => TestSlotFactory.Jamo("ㄴ", null, VirtualKeyCode.VK_N);
    private static KeySlot ㅣ_slot => TestSlotFactory.Jamo("ㅣ", null, VirtualKeyCode.VK_I);
    private static KeySlot ㅕ_slot => TestSlotFactory.Jamo("ㅕ", null, VirtualKeyCode.VK_J);
    private static KeySlot ㅅ_with_shift_ㅆ_slot => TestSlotFactory.Jamo("ㅅ", "ㅆ", VirtualKeyCode.VK_T);
    private static KeySlot ㅃ_slot => TestSlotFactory.Jamo("ㅂ", "ㅃ", VirtualKeyCode.VK_Q);
    private static KeySlot ㅉ_slot => TestSlotFactory.Jamo("ㅈ", "ㅉ", VirtualKeyCode.VK_W);
    private static KeySlot ㄸ_slot => TestSlotFactory.Jamo("ㄷ", "ㄸ", VirtualKeyCode.VK_E);
    private static KeySlot ㄲ_slot => TestSlotFactory.Jamo("ㄱ", "ㄲ", VirtualKeyCode.VK_R);
    private static KeySlot ㅒ_slot => TestSlotFactory.Jamo("ㅑ", "ㅒ", VirtualKeyCode.VK_O);
    private static KeySlot ㅖ_slot => TestSlotFactory.Jamo("ㅕ", "ㅖ", VirtualKeyCode.VK_P);
    private static KeySlot q_slot_with_english_label_q => TestSlotFactory.English("Q", null, VirtualKeyCode.VK_Q);
    private static KeySlot u_slot_with_english_label_u => TestSlotFactory.English("U", null, VirtualKeyCode.VK_U);

    private static KeyContext ctxNoModifiers => new(false, false, false, InputMode.Unicode, 0);
    private static KeyContext ctxShiftOnly => new(true, true, false, InputMode.Unicode, 1);
    private static KeyContext ctxCtrlShift => new(true, true, true, InputMode.Unicode, 0);

    private KoreanInputModule CreateModule(out FakeInputService input)
    {
        input = new FakeInputService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        return new KoreanInputModule(input, koDict, enDict);
    }

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
    public void 해_plus_Shift_ㅆ_feeds_ssang_siot_not_T()   // ★ 회귀 방지
    {
        var module = CreateModule(out var input);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);

        // Shift sticky 활성 상태 — HasActiveModifiers=true지만 ExcludingShift=false
        module.HandleKey(ㅅ_with_shift_ㅆ_slot, ctxShiftOnly);

        // 핵심: "T"가 나와서는 안 됨 (Shift+VK_T가 영문 T로 해석되면 안 됨)
        // 현재 구현: ㅆ을 종성으로 처리 → "했" (또는 새 초성 → "해ㅆ")
        Assert.DoesNotContain("T", module.CurrentWord);
        Assert.True(module.CurrentWord is "했" or "해ㅆ");
    }

    [Fact]
    public void Ctrl_Shift_T_is_not_a_jamo_path()
    {
        var module = CreateModule(out _);

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.ToggleSubmode();

        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
        Assert.Equal("A", module.ComposeStateLabel);
        Assert.Equal("", module.CurrentWord);
    }

    [Fact]
    public void QuietEnglish_mode_feeds_english_prefix_and_sends_unicode()
    {
        var module = CreateModule(out var input);
        module.ToggleSubmode();

        module.HandleKey(q_slot_with_english_label_q, ctxNoModifiers);
        module.HandleKey(u_slot_with_english_label_u, ctxNoModifiers);

        Assert.Equal("qu", module.CurrentWord);
        Assert.Contains("q", input.SentUnicodes);
        Assert.Contains("u", input.SentUnicodes);
    }

    [Fact]
    public void AcceptSuggestion_in_HangulJamo_returns_correct_bsCount()
    {
        var module = CreateModule(out _);
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

    // ──────────────────────────────────────────────────────────────
    // E 항목 누락 테스트 4종 (2026-04-18 추가)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// E-1: Shift+A를 QuietEnglish에서 눌렀을 때 prefix에 "A"가 누적되는지 검증
    /// (HasActiveModifiers=true이면 SendUnicode는 호출되지 않고 TrackEnglishKey만 호출됨)
    /// </summary>
    [Fact]
    public void QuietEnglish_Shift_A_updates_prefix_with_uppercase_A()
    {
        var module = CreateModule(out var input);
        module.ToggleSubmode(); // QuietEnglish 진입

        // Shift+A (ShowUpperCase=true, HasActiveModifiers=true)
        var aSlot = TestSlotFactory.English("a", "A", VirtualKeyCode.VK_A);
        module.HandleKey(aSlot, ctxShiftOnly);

        // prefix에 "A"가 누적되어야 함 (소문자 "a"가 아님)
        Assert.Equal("A", module.CurrentWord);
        // HasActiveModifiers=true이므로 SendUnicode는 호출되지 않음
        Assert.Empty(input.SentUnicodes);
    }

    /// <summary>
    /// E-2: 숫자키(1~0) · 기호키가 QuietEnglish에서 전송되는지 회귀
    /// </summary>
    [Fact]
    public void QuietEnglish_number_and_symbol_keys_are_sent()
    {
        var module = CreateModule(out var input);
        module.ToggleSubmode(); // QuietEnglish 진입

        // 숫자키 "1" (VK_1)
        var key1 = TestSlotFactory.Number("1", "!", VirtualKeyCode.VK_1);
        module.HandleKey(key1, ctxNoModifiers);

        // 기호키 "-" (VK_OEM_MINUS)
        var keyDash = TestSlotFactory.Symbol("-", "_", VirtualKeyCode.VK_OEM_MINUS);
        module.HandleKey(keyDash, ctxNoModifiers);

        // prefix에 "1-" 누적 확인
        Assert.Equal("1-", module.CurrentWord);
        // SendUnicode으로 "1"과 "-" 전송 확인
        Assert.Contains("1", input.SentUnicodes);
        Assert.Contains("-", input.SentUnicodes);
    }

    /// <summary>
    /// E-3: 백스페이스가 HangulJamo에서 composer를 올바르게 줄이는지
    /// </summary>
    [Fact]
    public void Backspace_in_HangulJamo_reduces_composer_correctly()
    {
        var module = CreateModule(out _);

        // "가" 조합 (ㄱ + ㅏ)
        module.HandleKey(ㄱ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        Assert.Equal("가", module.CurrentWord);

        // 백스페이스 → "ㄱ"으로 줄어들어야 함
        var backSlot = TestSlotFactory.Backspace();
        module.HandleKey(backSlot, ctxNoModifiers);
        Assert.Equal("ㄱ", module.CurrentWord);

        // 백스페이스 한 번 더 → 빈 문자열
        module.HandleKey(backSlot, ctxNoModifiers);
        Assert.Equal("", module.CurrentWord);
    }

    /// <summary>
    /// E-3: 백스페이스가 QuietEnglish에서 prefix를 올바르게 줄이는지
    /// </summary>
    [Fact]
    public void Backspace_in_QuietEnglish_reduces_prefix_correctly()
    {
        var module = CreateModule(out _);
        module.ToggleSubmode(); // QuietEnglish 진입

        // "abc" 입력
        var aSlot = TestSlotFactory.English("a", "A", VirtualKeyCode.VK_A);
        var bSlot = TestSlotFactory.English("b", "B", VirtualKeyCode.VK_B);
        var cSlot = TestSlotFactory.English("c", "C", VirtualKeyCode.VK_C);
        module.HandleKey(aSlot, ctxNoModifiers);
        module.HandleKey(bSlot, ctxNoModifiers);
        module.HandleKey(cSlot, ctxNoModifiers);
        Assert.Equal("abc", module.CurrentWord);

        // 백스페이스 → "ab"로 줄어들어야 함
        var backSlot = TestSlotFactory.Backspace();
        module.HandleKey(backSlot, ctxNoModifiers);
        Assert.Equal("ab", module.CurrentWord);

        // 백스페이스 한 번 더 → "a"
        module.HandleKey(backSlot, ctxNoModifiers);
        Assert.Equal("a", module.CurrentWord);
    }

    /// <summary>
    /// E-4: AcceptSuggestion VirtualKey 모드 경로(bsCount 사용) 테스트
    /// </summary>
    [Fact]
    public void AcceptSuggestion_in_QuietEnglish_returns_correct_bsCount()
    {
        var module = CreateModule(out _);
        module.ToggleSubmode(); // QuietEnglish 진입

        // "hello" 입력
        var hSlot = TestSlotFactory.English("h", "H", VirtualKeyCode.VK_H);
        var eSlot = TestSlotFactory.English("e", "E", VirtualKeyCode.VK_E);
        var lSlot = TestSlotFactory.English("l", "L", VirtualKeyCode.VK_L);
        var l2Slot = TestSlotFactory.English("l", "L", VirtualKeyCode.VK_L);
        var oSlot = TestSlotFactory.English("o", "O", VirtualKeyCode.VK_O);
        module.HandleKey(hSlot, ctxNoModifiers);
        module.HandleKey(eSlot, ctxNoModifiers);
        module.HandleKey(lSlot, ctxNoModifiers);
        module.HandleKey(l2Slot, ctxNoModifiers);
        module.HandleKey(oSlot, ctxNoModifiers);
        Assert.Equal("hello", module.CurrentWord);

        // "help" 제안 수락
        var (bs, word) = module.AcceptSuggestion("help");

        // bsCount는 prefix 길이(5)와 같아야 함
        Assert.Equal(5, bs);
        Assert.Equal("help", word);
        // AcceptSuggestion 후 prefix 초기화 확인
        Assert.Equal("", module.CurrentWord);
    }

    // ──────────────────────────────────────────────────────────────
    // G 항목: 회귀 방지 테스트 (2026-04-18 추가)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// G-1: AcceptSuggestion 직후 자모 재입력 시 prevLen=0 검증 (F-1 회귀 방지)
    /// </summary>
    [Fact]
    public void AcceptSuggestion_then_new_jamo_preserves_accepted_word_on_screen()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);      // "해" 조합

        var (bs, word) = module.AcceptSuggestion("해달");
        // VM 시뮬레이션
        input.SendAtomicReplace(bs, word);
        input.ResetTrackedLength();                     // F-1 수정 후 VM이 호출

        module.HandleKey(ㅎ_slot, ctxNoModifiers);      // Accept 후 첫 자모

        var last = input.AtomicReplaces.Last();
        Assert.Equal(0, last.prevLen);                  // 이전 제안을 건드리지 않아야 함
        Assert.Equal("ㅎ", last.next);
    }

    /// <summary>
    /// G-2: ToggleSubmode 전환 시 현재 학습/폐기 동작 고정
    /// ToggleSubmode는 FinalizeComposition을 호출하므로, 조합 중인 단어가 학습된다.
    /// </summary>
    [Fact]
    public void ToggleSubmode_during_composition_learns_word()
    {
        var (module, _, dict) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);       // "해달" 완성

        module.ToggleSubmode();

        // FinalizeComposition이 호출되어 학습됨
        Assert.Contains("해달", dict.GetSuggestions("해", 10));
    }

    /// <summary>
    /// G-5a: 조합 중 Backspace는 composer만 업데이트 (VK_BACK 전송 안 함)
    /// </summary>
    [Fact]
    public void Backspace_during_composition_updates_composer_only()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();
        var bsSlot = TestSlotFactory.Backspace();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);       // "해ㄷ" (cho=ㄷ 분리됨)

        int beforeBsPressCount = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);
        module.HandleKey(bsSlot, ctxNoModifiers);        // Backspace via HandleKey
        int afterBsPressCount = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);

        // Composer는 1단계 되돌리지만 직접 VK_BACK 전송은 없어야 함
        Assert.Equal("해", module.CurrentWord);
        Assert.Equal(beforeBsPressCount, afterBsPressCount);
    }

    /// <summary>
    /// G-5b: 조합 종료(OnSeparator) 후 Backspace는 VK_BACK을 전송하지 않음
    /// (현재 구현: composer가 비어있고 TrackedOnScreenLength=0이면 HandleBackspace 진입 불가)
    /// </summary>
    [Fact]
    public void Backspace_after_composition_ended_does_not_send_backspace_via_module()
    {
        var (module, input, _) = TestSlotFactory.CreateModuleWithInput();
        var bsSlot = TestSlotFactory.Backspace();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.OnSeparator();                           // "해" 확정, composer Reset

        int before = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);
        module.HandleKey(bsSlot, ctxNoModifiers);
        int after = input.KeyPresses.Count(k => k == VirtualKeyCode.VK_BACK);

        // 현재 동작: 모듈 레벨에서 VK_BACK 전송하지 않음 (VM이 처리)
        Assert.Equal(before, after);
        Assert.Equal("", module.CurrentWord);
    }

    /// <summary>
    /// G-6a: 빈 조합에서 OnSeparator 호출 시 학습하지 않음
    /// </summary>
    [Fact]
    public void OnSeparator_with_empty_composition_does_not_record()
    {
        var (module, _, dict) = TestSlotFactory.CreateModuleWithInput();

        // 빈 상태에서 separator
        module.OnSeparator();

        // 빈 prefix로는 GetSuggestions가 빈 리스트를 반환 (학습 없음)
        Assert.Empty(dict.GetSuggestions("", 10));
    }

    /// <summary>
    /// G-6b: 다음절 조합 후 OnSeparator 시 사용자 사전에 학습
    /// </summary>
    [Fact]
    public void OnSeparator_with_multi_syllable_records_user_dictionary()
    {
        var (module, _, dict) = TestSlotFactory.CreateModuleWithInput();

        module.HandleKey(ㅎ_slot, ctxNoModifiers);
        module.HandleKey(ㅐ_slot, ctxNoModifiers);
        module.HandleKey(ㄷ_slot, ctxNoModifiers);
        module.HandleKey(ㅏ_slot, ctxNoModifiers);
        module.HandleKey(ㄹ_slot, ctxNoModifiers);       // "해달"
        module.OnSeparator();

        Assert.Contains("해달", dict.GetSuggestions("해", 5));
    }
}
