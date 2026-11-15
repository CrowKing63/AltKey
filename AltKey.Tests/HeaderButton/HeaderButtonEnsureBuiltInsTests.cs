using AltKey.Models;
using Xunit;

namespace AltKey.Tests.HeaderButton;

// EnsureAllBuiltIns가 "새 기능" 기본 버튼(빠른 메모 → AI)을 상단바 맨 앞(1번)으로 정렬하고,
// 누락된 기본 버튼을 보정하며 커스텀 버튼은 건드리지 않는지 확인하는 테스트입니다.
public class HeaderButtonEnsureBuiltInsTests
{
    private static HeaderButtonConfig BuiltIn(string id) => new()
    {
        Id = id,
        Kind = HeaderButtonKind.BuiltIn
    };

    private static HeaderButtonConfig Custom(string id) => new()
    {
        Id = id,
        Kind = HeaderButtonKind.Custom,
        Position = "Right"
    };

    [Fact]
    // 설정(⚙) 버튼이 없으면 새 기능 기본 버튼이 맨 앞에, 나머지는 기본 순서로 채워지는지 확인합니다.
    public void missing_builtins_go_front_and_rest_fill_in_when_no_settings()
    {
        var buttons = new List<HeaderButtonConfig>
        {
            BuiltIn(HeaderButtonConfig.IdClipboard)
        };

        var result = HeaderButtonConfig.EnsureAllBuiltIns(buttons);

        Assert.Equal(HeaderButtonConfig.CreateDefaults().Count, result.Count);
        // 새 기능이 맨 앞(빠른 메모 → AI), 기존 클립보드가 그다음, 설정은 마지막입니다.
        Assert.Equal(
            [HeaderButtonConfig.IdScratchPad, HeaderButtonConfig.IdAi, HeaderButtonConfig.IdClipboard],
            [result[0].Id, result[1].Id, result[2].Id]);
        Assert.Equal(HeaderButtonConfig.IdSettings, result[^1].Id);
    }

    [Fact]
    // 오래된 설정에서 AI ✨가 설정 오른쪽에 있고 스크래치패드가 누락돼 있으면,
    // 빠른 메모 → AI 순서로 맨 앞으로 정렬되는지 확인합니다.
    public void builtins_after_settings_are_moved_to_front()
    {
        var buttons = new List<HeaderButtonConfig>
        {
            BuiltIn(HeaderButtonConfig.IdClipboard),
            BuiltIn(HeaderButtonConfig.IdSettings),
            BuiltIn(HeaderButtonConfig.IdAi),
        };

        var result = HeaderButtonConfig.EnsureAllBuiltIns(buttons);

        Assert.Equal([HeaderButtonConfig.IdScratchPad, HeaderButtonConfig.IdAi], [result[0].Id, result[1].Id]);
        Assert.True(result.FindIndex(b => b.Id == HeaderButtonConfig.IdSettings) > 1, "설정은 새 기능 다음");
    }

    [Fact]
    // 기존 설치에서 스크래치패드가 AI 뒤에 있는 순서(.., Osk, AI, 빠른 메모, 설정)가
    // 맨 앞에 "빠른 메모 → AI"로 다시 정렬되는지 확인합니다.
    public void old_order_with_scratchpad_after_ai_is_rearranged()
    {
        var buttons = new List<HeaderButtonConfig>
        {
            BuiltIn(HeaderButtonConfig.IdClipboard),
            BuiltIn(HeaderButtonConfig.IdOsk),
            BuiltIn(HeaderButtonConfig.IdAi),
            BuiltIn(HeaderButtonConfig.IdScratchPad),
            BuiltIn(HeaderButtonConfig.IdSettings),
        };

        var result = HeaderButtonConfig.EnsureAllBuiltIns(buttons);

        // 누락된 기본 버튼(이모지·자동완성)은 설정 바로 앞에 채워지고, 새 기능은 맨 앞입니다.
        Assert.Equal(
            [HeaderButtonConfig.IdScratchPad, HeaderButtonConfig.IdAi,
             HeaderButtonConfig.IdClipboard, HeaderButtonConfig.IdOsk,
             HeaderButtonConfig.IdEmoji, HeaderButtonConfig.IdAutoComplete,
             HeaderButtonConfig.IdOsIme, HeaderButtonConfig.IdSettings],
            result.Select(b => b.Id).ToArray());
    }

    [Fact]
    // 설정 오른쪽의 커스텀 버튼은 절대 옮기지 않는지(기본 버튼만 정렬) 확인합니다.
    public void custom_buttons_after_settings_stay_put()
    {
        var buttons = new List<HeaderButtonConfig>
        {
            BuiltIn(HeaderButtonConfig.IdSettings),
            Custom("Custom-1"),
            BuiltIn(HeaderButtonConfig.IdAi),
        };

        var result = HeaderButtonConfig.EnsureAllBuiltIns(buttons);

        int setIdx = result.FindIndex(b => b.Id == HeaderButtonConfig.IdSettings);
        int customIdx = result.FindIndex(b => b.Id == "Custom-1");
        int aiIdx = result.FindIndex(b => b.Id == HeaderButtonConfig.IdAi);

        Assert.True(aiIdx < setIdx, "AI(기본)는 설정 왼쪽으로 이동");
        Assert.True(customIdx > setIdx, "커스텀 버튼은 설정 오른쪽 그대로");
    }

    [Fact]
    // 새 기능 외 기존 버튼의 상대 순서·표시 여부와 커스텀 버튼은 그대로 유지되는지 확인합니다.
    public void existing_order_custom_buttons_and_visibility_are_untouched()
    {
        var clipboard = BuiltIn(HeaderButtonConfig.IdClipboard);
        var custom = Custom("Custom-1");
        var emoji = new HeaderButtonConfig
        {
            Id = HeaderButtonConfig.IdEmoji,
            Kind = HeaderButtonKind.BuiltIn,
            Visible = false
        };

        var buttons = new List<HeaderButtonConfig> { clipboard, custom, emoji };
        var result = HeaderButtonConfig.EnsureAllBuiltIns(buttons);

        // 새 기능 기본 버튼 2개(빠른 메모 → AI)가 맨 앞에 추가되고, 나머지는 상대 순서 그대로입니다.
        Assert.Equal(3 + (HeaderButtonConfig.CreateDefaults().Count - 2), result.Count);
        Assert.Equal([HeaderButtonConfig.IdScratchPad, HeaderButtonConfig.IdAi], [result[0].Id, result[1].Id]);

        int clipIdx = result.FindIndex(b => b.Id == clipboard.Id);
        int customIdx = result.FindIndex(b => b.Id == custom.Id);
        int emojiIdx = result.FindIndex(b => b.Id == emoji.Id);
        Assert.True(clipIdx < customIdx && customIdx < emojiIdx, "기존 상대 순서 유지");

        Assert.True(result[clipIdx].Visible);          // 표시 여부는 그대로
        Assert.False(result[emojiIdx].Visible);        // 숨김 설정도 그대로
        Assert.Equal(HeaderButtonKind.Custom, result[customIdx].Kind);
    }

    [Fact]
    // 두 번 호출해도 중복이 생기지 않는지(멱등) 확인합니다.
    public void calling_twice_does_not_duplicate()
    {
        var buttons = new List<HeaderButtonConfig>
        {
            BuiltIn(HeaderButtonConfig.IdClipboard)
        };

        var once = HeaderButtonConfig.EnsureAllBuiltIns(buttons);
        var twice = HeaderButtonConfig.EnsureAllBuiltIns(once);

        Assert.Equal(once.Count, twice.Count);
        Assert.Single(twice.Where(b => b.Id == HeaderButtonConfig.IdScratchPad));
    }

    [Fact]
    // 스크래치패드 기본 버튼은 AI처럼 OFF(Visible=false)로 시작하는지 확인합니다.
    public void scratchpad_default_is_hidden()
    {
        var defaults = HeaderButtonConfig.CreateDefaults();

        var scratchPad = Assert.Single(defaults, b => b.Id == HeaderButtonConfig.IdScratchPad);
        Assert.False(scratchPad.Visible);
        Assert.True(HeaderButtonConfig.IsBuiltInId(HeaderButtonConfig.IdScratchPad));
    }

    [Fact]
    // 구버전 앱이 같은 ID의 기본 버튼을 Custom으로 강등해 저장한 설정도 BuiltIn으로 자가치유되는지 확인합니다.
    public void custom_entry_with_known_builtin_id_is_repaired_to_builtin()
    {
        var broken = new HeaderButtonConfig
        {
            Id = HeaderButtonConfig.IdScratchPad,
            Kind = HeaderButtonKind.Custom,
            Visible = true,
            Position = "Right",
            IconText = "새",
            Tooltip = "커스텀 상단바 단축키",
            AccessibleName = "커스텀 상단바 단축키",
            CustomAction = new SendKeyAction("VK_A")
        };

        var buttons = new List<HeaderButtonConfig> { broken, BuiltIn(HeaderButtonConfig.IdSettings) };
        var result = HeaderButtonConfig.EnsureAllBuiltIns(buttons);

        var sp = Assert.Single(result, b => b.Id == HeaderButtonConfig.IdScratchPad);
        Assert.Equal(HeaderButtonKind.BuiltIn, sp.Kind);
        Assert.Null(sp.CustomAction);
        Assert.True(sp.Visible); // 표시 여부는 그대로 유지
    }
}
