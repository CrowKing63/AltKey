using System.IO;
using AltKey.Models;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey.Tests;

public class WindowOpacityProfileTests
{
    [Fact]
    public void WindowOpacityProfile_returns_opaque_when_both_features_are_disabled()
    {
        var config = new AppConfig
        {
            ActiveOpacityEnabled = false,
            OpacityActive = 0.8,
            IdleOpacityEnabled = false,
            OpacityIdle = 0.2
        };

        Assert.Equal(1.0, WindowOpacityProfile.GetBaseOpacity(config));
        Assert.Equal(1.0, WindowOpacityProfile.GetIdleOpacity(config));
        Assert.False(WindowOpacityProfile.ShouldStartIdleTimer(config));
    }

    [Fact]
    public void WindowOpacityProfile_returns_active_opacity_when_only_active_feature_is_enabled()
    {
        var config = new AppConfig
        {
            ActiveOpacityEnabled = true,
            OpacityActive = 0.8,
            IdleOpacityEnabled = false,
            OpacityIdle = 0.2
        };

        Assert.Equal(0.8, WindowOpacityProfile.GetBaseOpacity(config));
        Assert.Equal(0.8, WindowOpacityProfile.GetIdleOpacity(config));
        Assert.False(WindowOpacityProfile.ShouldStartIdleTimer(config));
    }

    [Fact]
    public void WindowOpacityProfile_returns_idle_opacity_when_both_features_are_enabled()
    {
        var config = new AppConfig
        {
            ActiveOpacityEnabled = true,
            OpacityActive = 0.8,
            IdleOpacityEnabled = true,
            OpacityIdle = 0.2
        };

        Assert.Equal(0.8, WindowOpacityProfile.GetBaseOpacity(config));
        Assert.Equal(0.2, WindowOpacityProfile.GetIdleOpacity(config));
        Assert.True(WindowOpacityProfile.ShouldStartIdleTimer(config));
    }

    [Fact]
    public void WindowOpacityProfile_caps_idle_opacity_to_active_opacity()
    {
        var config = new AppConfig
        {
            ActiveOpacityEnabled = true,
            OpacityActive = 0.8,
            IdleOpacityEnabled = true,
            OpacityIdle = 1.0
        };

        Assert.Equal(0.8, WindowOpacityProfile.GetIdleOpacityMaximum(config));
        Assert.Equal(0.8, WindowOpacityProfile.GetIdleOpacity(config));
    }

    [Fact]
    public void ConfigService_uses_new_opacity_defaults_when_fields_are_missing()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            File.WriteAllText(
                PathResolver.ConfigPath,
                """
                {
                  "Theme": "system",
                  "AlwaysOnTop": true
                }
                """);

            var configService = new ConfigService();

            Assert.False(configService.Current.ActiveOpacityEnabled);
            Assert.Equal(1.0, configService.Current.OpacityActive);
            Assert.False(configService.Current.IdleOpacityEnabled);
            Assert.Equal(1.0, configService.Current.OpacityIdle);
            Assert.Equal(5000, configService.Current.FadeDelayMs);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    public void SettingsViewModel_loads_new_opacity_fields_from_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            configService.Update(config =>
            {
                config.ActiveOpacityEnabled = true;
                config.OpacityActive = 0.8;
                config.IdleOpacityEnabled = true;
                config.OpacityIdle = 0.2;
                config.FadeDelayMs = 3000;
            });

            var viewModel = CreateSettingsViewModel(configService);

            Assert.True(viewModel.ActiveOpacityEnabled);
            Assert.Equal(0.8, viewModel.OpacityActive);
            Assert.True(viewModel.IdleOpacityEnabled);
            Assert.Equal(0.2, viewModel.OpacityIdle);
            Assert.Equal(3, viewModel.FadeDelaySec);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    public void SettingsViewModel_saves_new_opacity_fields_to_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            var viewModel = CreateSettingsViewModel(configService);

            viewModel.ActiveOpacityEnabled = true;
            viewModel.OpacityActive = 0.8;
            viewModel.IdleOpacityEnabled = true;
            viewModel.OpacityIdle = 0.2;
            viewModel.FadeDelaySec = 3;

            Assert.True(configService.Current.ActiveOpacityEnabled);
            Assert.Equal(0.8, configService.Current.OpacityActive);
            Assert.True(configService.Current.IdleOpacityEnabled);
            Assert.Equal(0.2, configService.Current.OpacityIdle);
            Assert.Equal(3000, configService.Current.FadeDelayMs);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    public void SettingsViewModel_clamps_idle_opacity_to_active_opacity()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            var viewModel = CreateSettingsViewModel(configService);

            viewModel.ActiveOpacityEnabled = true;
            viewModel.OpacityActive = 0.8;
            viewModel.IdleOpacityEnabled = true;
            viewModel.OpacityIdle = 1.0;

            Assert.Equal(0.8, viewModel.IdleOpacityMaximum);
            Assert.Equal(0.8, viewModel.OpacityIdle);
            Assert.Equal(0.8, configService.Current.OpacityIdle);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    public void SettingsViewModel_turns_off_idle_opacity_when_active_opacity_is_disabled()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            var viewModel = CreateSettingsViewModel(configService);

            viewModel.ActiveOpacityEnabled = true;
            viewModel.IdleOpacityEnabled = true;

            viewModel.ActiveOpacityEnabled = false;

            Assert.False(viewModel.CanUseIdleOpacity);
            Assert.False(viewModel.CanEditIdleOpacity);
            Assert.False(viewModel.IdleOpacityEnabled);
            Assert.False(configService.Current.IdleOpacityEnabled);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    private static SettingsViewModel CreateSettingsViewModel(ConfigService configService)
    {
        return new SettingsViewModel(
            configService,
            new ThemeService(configService),
            new LayoutService(),
            new HotkeyService(),
            new StartupService(),
            new SoundService(),
            new UpdateService(),
            new DownloadService(),
            new InstallerService(),
            new AiService(configService));
    }

    private static string CreateTempDataDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AltKeyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void ResetDataDir(string tempDir)
    {
        PathResolver.OverrideDataDir(null);

        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
