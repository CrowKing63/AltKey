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
    public void AppConfig_SwitchScan_defaults_match_accessibility_spec()
    {
        var config = new AppConfig();

        Assert.Equal("VK_TAB", config.SwitchScanNextKey);
        Assert.Equal("VK_RETURN", config.SwitchScanSelectKey);
        Assert.Equal("VK_SPACE", config.SwitchScanSecondarySelectKey);
        Assert.Equal("", config.SwitchScanPreviousKey);
        Assert.Equal("", config.SwitchScanPauseKey);
        Assert.Equal(SwitchScanMode.Linear, config.SwitchScanMode);
        Assert.True(config.SwitchScanWrapEnabled);
        Assert.True(config.SwitchScanIncludeSuggestions);
    }

    [Fact]
    public void AppConfig_SwitchScan_settings_survive_json_round_trip()
    {
        var original = new AppConfig
        {
            SwitchScanMode = SwitchScanMode.Manual,
            SwitchScanInitialDelayMs = 1200,
            SwitchScanSelectPauseMs = 700,
            SwitchScanCyclesBeforePause = 3,
            SwitchScanWrapEnabled = false,
            SwitchScanNextKey = "VK_F8",
            SwitchScanSelectKey = "VK_F9",
            SwitchScanSecondarySelectKey = "VK_F10",
            SwitchScanPreviousKey = "VK_F7",
            SwitchScanPauseKey = "VK_F6",
            SwitchScanIncludeSuggestions = false,
            SwitchScanSuggestionPriority = SwitchScanSuggestionPriority.AfterKeyboard
        };

        var json = JsonSerializer.Serialize(original, JsonOptions.Default);
        var restored = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default);

        Assert.NotNull(restored);
        Assert.Equal(SwitchScanMode.Manual, restored!.SwitchScanMode);
        Assert.Equal(1200, restored.SwitchScanInitialDelayMs);
        Assert.Equal(700, restored.SwitchScanSelectPauseMs);
        Assert.Equal(3, restored.SwitchScanCyclesBeforePause);
        Assert.False(restored.SwitchScanWrapEnabled);
        Assert.Equal("VK_F8", restored.SwitchScanNextKey);
        Assert.Equal("VK_F9", restored.SwitchScanSelectKey);
        Assert.Equal("VK_F10", restored.SwitchScanSecondarySelectKey);
        Assert.Equal("VK_F7", restored.SwitchScanPreviousKey);
        Assert.Equal("VK_F6", restored.SwitchScanPauseKey);
        Assert.False(restored.SwitchScanIncludeSuggestions);
        Assert.Equal(SwitchScanSuggestionPriority.AfterKeyboard, restored.SwitchScanSuggestionPriority);
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
        var allKeys = layout!.Columns!
            .SelectMany(c => c.Rows!.SelectMany(r => r.Keys));

        var winKey = allKeys.First(k => k.Label == "Win");

        var action = Assert.IsType<ToggleStickyAction>(winKey.Action);
        Assert.Equal("VK_LWIN", action.Vk);
    }
}
