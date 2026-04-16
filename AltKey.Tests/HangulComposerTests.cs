using AltKey.Services;

namespace AltKey.Tests;

public class HangulComposerTests
{
    [Fact]
    public void Feed_SingleChoseong_ReturnsChoseong()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄱ");
        Assert.Equal("ㄱ", composer.Current);
    }

    [Fact]
    public void Feed_ChoseongJungseong_ComposesSyllable()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄱ");
        composer.Feed("ㅏ");
        Assert.Equal("가", composer.Current);
    }

    [Fact]
    public void Feed_FullSyllable_WithJongseong()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄱ");
        composer.Feed("ㅏ");
        composer.Feed("ㄴ");
        Assert.Equal("간", composer.Current);
    }

    [Fact]
    public void Feed_JongseongBecomesChoseong_WhenVowelFollows()
    {
        // 가나: ㄱ+ㅏ → 가, ㄴ → 간(종성), ㅏ → ㄴ이 초성으로 이동 → 가나
        var composer = new HangulComposer();
        composer.Feed("ㄱ"); composer.Feed("ㅏ"); // 가
        composer.Feed("ㄴ"); // 간
        composer.Feed("ㅏ"); // 가나
        Assert.Equal("가나", composer.Current);
    }

    [Fact]
    public void Feed_Ssangieung_DoubleConsonant()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄲ");
        composer.Feed("ㅏ");
        Assert.Equal("까", composer.Current);
    }

    [Fact]
    public void Feed_CompoundJungseong_OA()
    {
        // 화: ㅎ+ㅗ+ㅏ = ㅎ + ㅘ
        var composer = new HangulComposer();
        composer.Feed("ㅎ");
        composer.Feed("ㅗ");
        composer.Feed("ㅏ");
        Assert.Equal("화", composer.Current);
    }

    [Fact]
    public void Feed_CompoundJungseong_U_EO()
    {
        // 눠: ㄴ+ㅜ+ㅓ = ㄴ + ㅝ (compound jungseong)
        var composer = new HangulComposer();
        composer.Feed("ㄴ");
        composer.Feed("ㅜ");
        composer.Feed("ㅓ");
        Assert.Equal("눠", composer.Current);
    }

    [Fact]
    public void Feed_CompoundJungseong_EUI()
    {
        var composer = new HangulComposer();
        composer.Feed("ㅇ");
        composer.Feed("ㅡ");
        composer.Feed("ㅣ");
        Assert.Equal("의", composer.Current);
    }

    [Fact]
    public void Backspace_RemoveJongseong()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄱ"); composer.Feed("ㅏ"); composer.Feed("ㄴ");
        Assert.Equal("간", composer.Current);
        composer.Backspace();
        Assert.Equal("가", composer.Current);
    }

    [Fact]
    public void Backspace_RemoveJungseong()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄱ"); composer.Feed("ㅏ");
        Assert.Equal("가", composer.Current);
        composer.Backspace();
        Assert.Equal("ㄱ", composer.Current);
    }

    [Fact]
    public void Backspace_FromChoseong()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄱ");
        composer.Backspace();
        Assert.Equal("", composer.Current);
    }

    [Fact]
    public void Backspace_RemoveJongseong_DuringComposition()
    {
        // "간"에서 백스페이스 → 종성 제거 → "가"
        var composer = new HangulComposer();
        composer.Feed("ㄱ"); composer.Feed("ㅏ"); composer.Feed("ㄴ");
        Assert.Equal("간", composer.Current);
        composer.Backspace();
        Assert.Equal("가", composer.Current);
    }

    [Fact]
    public void Backspace_RemoveCompletedSyllable_WhenNoComposition()
    {
        // "가" 확정 후, 조합 중인 문자 없을 때 백스페이스 → 전체 삭제
        var composer = new HangulComposer();
        composer.Feed("ㄱ"); composer.Feed("ㅏ");
        // ㄸ은 종성이 될 수 없으므로 "가"가 확정되고 "ㄸ"이 새 초성으로 시작
        composer.Feed("ㄸ");
        Assert.Equal("가ㄸ", composer.Current);
        // 백스페이스로 "ㄸ" 제거
        composer.Backspace();
        Assert.Equal("가", composer.Current);
        // 백스페이스로 "가" 전체 삭제 (분해하지 않음)
        composer.Backspace();
        Assert.Equal("", composer.Current);
    }

    [Fact]
    public void Backspace_HwasaScenario_EmptyFieldAfter3Backspaces()
    {
        // "화사"에서 백스페이스 3회 → 빈 필드 (OS IME 동작 일치)
        var composer = new HangulComposer();
        composer.Feed("ㅎ"); composer.Feed("ㅗ"); composer.Feed("ㅏ");
        composer.Feed("ㅅ"); composer.Feed("ㅏ");
        Assert.Equal("화사", composer.Current);
        // 백스페이스 1: "사"의 중성(ㅏ) 제거 → "화ㅅ"
        composer.Backspace();
        Assert.Equal("화ㅅ", composer.Current);
        // 백스페이스 2: "ㅅ"의 초성 제거 → "화"
        composer.Backspace();
        Assert.Equal("화", composer.Current);
        // 백스페이스 3: "화" 전체 삭제 (분해하지 않음)
        composer.Backspace();
        Assert.Equal("", composer.Current);
    }

    [Fact]
    public void Reset_ClearsAll()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄱ"); composer.Feed("ㅏ");
        composer.Reset();
        Assert.Equal("", composer.Current);
    }

    [Fact]
    public void Feed_ConsonantAfterSyllable_Jongseong()
    {
        // 낙: ㄴ+ㅏ+ㄱ
        var composer = new HangulComposer();
        composer.Feed("ㄴ"); composer.Feed("ㅏ"); composer.Feed("ㄱ");
        Assert.Equal("낙", composer.Current);
    }

    [Fact]
    public void Feed_ConsonantAfterJongseong_NewChoseong()
    {
        // 한글: ㅎ+ㅏ+ㄴ → 한, ㄱ → 새 초성, ㅡ → 그, ㄹ → 글
        var composer = new HangulComposer();
        composer.Feed("ㅎ"); composer.Feed("ㅏ"); composer.Feed("ㄴ");
        Assert.Equal("한", composer.Current);
        composer.Feed("ㄱ");
        Assert.Equal("한ㄱ", composer.Current);
        composer.Feed("ㅡ");
        Assert.Equal("한그", composer.Current);
        composer.Feed("ㄹ");
        Assert.Equal("한글", composer.Current);
    }

    [Fact]
    public void Feed_CompoundJongseong_Formation()
    {
        // 닭: ㄷ+ㅏ+ㄹ → 달, then ㄱ → ㄺ 겹받침 → 닭
        var composer = new HangulComposer();
        composer.Feed("ㄷ"); composer.Feed("ㅏ"); composer.Feed("ㄹ");
        Assert.Equal("달", composer.Current);
        composer.Feed("ㄱ");
        Assert.Equal("닭", composer.Current);
    }

    [Fact]
    public void Feed_CompoundJongseong_Split_WithVowel()
    {
        // 닭 + ㅣ → 겹받침 ㄺ 분해: ㄹ은 이전 음절 종성, ㄱ은 다음 초성 → 달기
        var composer = new HangulComposer();
        composer.Feed("ㄷ"); composer.Feed("ㅏ"); composer.Feed("ㄹ"); composer.Feed("ㄱ");
        Assert.Equal("닭", composer.Current);
        composer.Feed("ㅣ");
        Assert.Equal("달기", composer.Current);
    }

    [Fact]
    public void Feed_TwoConsonantsInARow()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄴ"); composer.Feed("ㄱ");
        Assert.Equal("ㄴㄱ", composer.Current);
    }

    [Fact]
    public void Feed_StandaloneVowel()
    {
        var composer = new HangulComposer();
        composer.Feed("ㅏ");
        Assert.Equal("ㅏ", composer.Current);
    }

    [Fact]
    public void Feed_DakisGiyeok_CompoundJongseong()
    {
        // 닭: ㄷ+ㅏ+ㄹ → 달, then ㄱ → 겹받침 ㄺ → 닭
        var composer = new HangulComposer();
        composer.Feed("ㄷ"); composer.Feed("ㅏ"); composer.Feed("ㄹ"); composer.Feed("ㄱ");
        Assert.Equal("닭", composer.Current);
    }

    [Fact]
    public void HasComposition_ReturnsTrue_WhenComposing()
    {
        var composer = new HangulComposer();
        Assert.False(composer.HasComposition);
        composer.Feed("ㄱ");
        Assert.True(composer.HasComposition);
        composer.Feed("ㅏ");
        Assert.True(composer.HasComposition);
    }

    [Fact]
    public void HasComposition_ReturnsFalse_AfterReset()
    {
        var composer = new HangulComposer();
        composer.Feed("ㄱ"); composer.Feed("ㅏ");
        Assert.True(composer.HasComposition);
        composer.Reset();
        Assert.False(composer.HasComposition);
    }

    [Fact]
    public void CompletedLength_TracksCompletedSyllables()
    {
        var composer = new HangulComposer();
        Assert.Equal(0, composer.CompletedLength);
        // "화" 조합 중: ㅎ+ㅗ+ㅏ → 조합 중, CompletedLength는 0
        composer.Feed("ㅎ"); composer.Feed("ㅗ"); composer.Feed("ㅏ");
        Assert.Equal(0, composer.CompletedLength);
        // 모음 입력 → "화" 확정 후 새 모음 조합 중
        composer.Feed("ㅓ");
        Assert.Equal(1, composer.CompletedLength);
    }

    [Fact]
    public void CompletedLength_IncludesNonHangulCharacters()
    {
        var composer = new HangulComposer();
        composer.Feed("a");
        Assert.Equal(1, composer.CompletedLength);
        composer.Feed("b");
        Assert.Equal(2, composer.CompletedLength);
    }
}

public static class HangulComposerTestExtensions
{
    public static void FinalizeCurrentForTest(this HangulComposer composer)
    {
        composer.Reset(); // Approximate - in real use, finalization happens via separators
    }
}