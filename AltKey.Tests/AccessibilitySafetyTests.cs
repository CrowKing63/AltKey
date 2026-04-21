using System.Text.Json;
using AltKey.Models;

namespace AltKey.Tests;

public class AccessibilitySafetyTests
{
    [Fact]
    public void AppConfig_KeyRepeatEnabled_default_is_off()
    {
        var config = new AppConfig();
        Assert.False(config.KeyRepeatEnabled);
    }

    [Fact]
    public void BagicLayout_WinKey_uses_toggle_sticky()
    {
        var layoutPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "AltKey", "layouts", "Bagic.json"));

        var json = File.ReadAllText(layoutPath);
        var layout = JsonSerializer.Deserialize<LayoutConfig>(json, JsonOptions.Default);

        Assert.NotNull(layout);
        var allKeys = layout!.Columns?
            .SelectMany(c => c.Rows!.SelectMany(r => r.Keys))
            ?? layout.Rows!.SelectMany(r => r.Keys);

        var winKey = allKeys.First(k => k.Label == "Win");

        var action = Assert.IsType<ToggleStickyAction>(winKey.Action);
        Assert.Equal("VK_LWIN", action.Vk);
    }
}
