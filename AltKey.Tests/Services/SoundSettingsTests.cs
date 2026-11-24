using System.IO;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey.Tests.Services;

// 키 사운드 크기(SoundVolumePercent) 설정의 저장/불러오기 동작을 검증하는 테스트입니다.
public class SoundSettingsTests
{
    [Fact]
    // 설정 파일에 sound_volume_percent가 없으면 기본값 100이 적용되는지 확인합니다.
    public void ConfigService_uses_default_sound_volume_when_field_is_missing()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            File.WriteAllText(
                PathResolver.ConfigPath,
                """
                {
                  "SoundEnabled": true
                }
                """);

            var configService = new ConfigService();

            Assert.Equal(100, configService.Current.SoundVolumePercent);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // 설정 파일의 sound_volume_percent가 ViewModel에 그대로 반영되는지 확인합니다.
    public void SettingsViewModel_loads_sound_volume_from_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            configService.Update(config => config.SoundVolumePercent = 40);

            var viewModel = CreateSettingsViewModel(configService);

            Assert.Equal(40, viewModel.SoundVolume);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // ViewModel에서 크기를 바꿨을 때 설정에 저장되는지 확인합니다.
    public void SettingsViewModel_saves_sound_volume_to_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();

            var viewModel = CreateSettingsViewModel(configService);
            viewModel.SoundVolume = 60;

            Assert.Equal(60, configService.Current.SoundVolumePercent);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // 설정 파일에 sound_preset가 없으면 기본값 1(조용)이 적용되는지 확인합니다.
    public void ConfigService_uses_default_sound_preset_when_field_is_missing()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            File.WriteAllText(
                PathResolver.ConfigPath,
                """
                {
                  "SoundEnabled": true
                }
                """);

            var configService = new ConfigService();

            Assert.Equal(1, configService.Current.SoundPreset);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // 설정 파일의 sound_preset이 ViewModel 라디오 상태에 반영되는지 확인합니다.
    public void SettingsViewModel_loads_sound_preset_from_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            configService.Update(config => config.SoundPreset = 1);

            var viewModel = CreateSettingsViewModel(configService);

            Assert.True(viewModel.SoundIsOne);
            Assert.False(viewModel.SoundIsThree);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // ViewModel에서 스타일을 바꿨을 때 설정에 저장되는지 확인합니다.
    public void SettingsViewModel_saves_sound_preset_to_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();

            var viewModel = CreateSettingsViewModel(configService);
            viewModel.SoundPreset = 2;

            Assert.Equal(2, configService.Current.SoundPreset);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // 기존 설정에 사용자 지정 파일만 있으면 "custom" 상태(라디오 미선택)로 표시되는지 확인합니다.
    public void SettingsViewModel_treats_legacy_custom_file_as_custom_state()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            var customWav = Path.Combine(tempDir, "my.wav");
            File.WriteAllText(customWav, "RIFF");
            configService.Update(config => config.SoundFilePath = customWav);

            var viewModel = CreateSettingsViewModel(configService);

            Assert.Equal(0, viewModel.SoundPreset);
            Assert.False(viewModel.SoundIsOne);
            Assert.False(viewModel.SoundIsTwo);
            Assert.False(viewModel.SoundIsThree);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // 프리셋 이름이 번들된 WAV로, 없는 파일은 기본음으로 폴백되는지 확인합니다.
    public void SoundService_resolves_preset_paths_with_fallback()
    {
        var baseDir = AppContext.BaseDirectory;
        var soundsDir = Path.Combine(baseDir, "Assets", "Sounds");

        // 프리셋 5종은 번들 파일로 해석된다.
        foreach (var preset in new[] { 1, 2, 3, 4, 5 })
        {
            var path = SoundService.ResolvePath(preset, null);
            Assert.NotNull(path);
            Assert.True(File.Exists(path!));
            Assert.EndsWith($"Assets/Sounds/{preset}.wav".Replace('/', Path.DirectorySeparatorChar), path!);
        }

        // 사용자 지정 파일이 있으면 프리셋보다 우선한다.
        var customWav = Path.Combine(soundsDir, "1.wav");
        Assert.Equal(customWav, SoundService.ResolvePath(2, customWav));

        // custom(0)인데 지정 파일이 없거나 프리셋 값이 알 수 없으면 소리가 안 나도록 null을 반환한다.
        Assert.Null(SoundService.ResolvePath(0, null));
        Assert.Null(SoundService.ResolvePath(99, null));
    }

    private static SettingsViewModel CreateSettingsViewModel(ConfigService configService)
    {
        var localBackend = new LocalGgufBackend(new SidecarProcessManager(), new ModelStore(), configService);
        return new SettingsViewModel(
            configService,
            new ThemeService(configService),
            new LayoutService(),
            new StartupService(),
            new SoundService(),
            new UpdateService(),
            new DownloadService(),
            new InstallerService(),
            new AiService(configService, localBackend),
            new ModelStore(),
            localBackend,
            // 사용자 설정 백업·복원·초기화 관련 의존성 — 이 테스트들은 해당 기능을 쓰지 않아 기본 인스턴스만 넘깁니다.
            new SettingsBackupService(),
            new ClipboardService(),
            new ScratchPadService(),
            new KoreanDictionary(lang => new WordFrequencyStore(lang), lang => new BigramFrequencyStore(lang)),
            new EnglishDictionary(lang => new WordFrequencyStore(lang), lang => new BigramFrequencyStore(lang)));
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
