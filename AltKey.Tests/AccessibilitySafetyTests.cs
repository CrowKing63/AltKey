using System.Text.Json;
using System.Threading;
using AltKey.Controls;
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
    public void AppConfig_KeyboardA11yNavigationEnabled_default_is_off()
    {
        var config = new AppConfig();
        Assert.False(config.KeyboardA11yNavigationEnabled);
    }

    [Fact]
    public void AppConfig_KeyboardA11yNavigationEnabled_survives_json_round_trip()
    {
        var original = new AppConfig
        {
            KeyboardA11yNavigationEnabled = true
        };

        var json = JsonSerializer.Serialize(original, JsonOptions.Default);
        var restored = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default);

        Assert.NotNull(restored);
        Assert.True(restored!.KeyboardA11yNavigationEnabled);
    }

    [Fact]
    public void KeyButton_KeyboardA11yNavigationEnabled_toggles_tab_navigation_flags()
    {
        Exception? captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                var button = new KeyButton();

                button.KeyboardA11yNavigationEnabled = true;

                Assert.True(button.Focusable);
                Assert.True(button.IsTabStop);

                button.KeyboardA11yNavigationEnabled = false;

                Assert.False(button.Focusable);
                Assert.False(button.IsTabStop);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
            throw captured;
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
