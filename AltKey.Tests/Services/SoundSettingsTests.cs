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
    // 설정 파일에 sound_preset가 없으면 기본값 1(또렷)이 적용되는지 확인합니다.
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

        // 프리셋 7종은 번들 파일로 해석된다.
        foreach (var preset in new[] { 1, 2, 3, 4, 5, 6, 7 })
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

    // 4~7번 프리셋이 저장 값 그대로 라디오에 반영되는지 확인합니다. (4~5번이 기본값 1으로 덮어쓰이던 회귀 방지)
    [Fact]
    public void SettingsViewModel_loads_preset_four_to_seven_from_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            configService.Update(config => config.SoundPreset = 7);

            var viewModel = CreateSettingsViewModel(configService);

            Assert.True(viewModel.SoundIsSeven);
            Assert.False(viewModel.SoundIsSix);
            Assert.False(viewModel.SoundIsOne);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    // 구 번호 체계(1=조용,2=균형,3=또렷,4=밝은 클릭,5=빠른 스냅,6=타자기,7=타자기(깊은 타건),8=옛날 레트로,9=맥 레트로)의 설정 파일을
    // 새 번호(1=또렷,2=균형,3=빠른 스냅,4=타자기,5=타자기(깊은 타건),6=옛날 레트로,7=맥 레트로)로 변환해 불러오는지,
    // 그리고 변환이 한 번만 적용되는지 확인합니다.
    [Fact]
    public void ConfigService_migrates_legacy_sound_preset_numbers_once()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            File.WriteAllText(PathResolver.ConfigPath, """{"sound_preset": 8, "sound_volume_percent": 80}""");

            var configService = new ConfigService();

            // 구 번호 8(옛날 레트로) → 새 번호 6
            Assert.Equal(6, configService.Current.SoundPreset);
            Assert.True(configService.Current.SoundPresetMigratedV2);
            // 무관한 설정은 그대로 유지된다.
            Assert.Equal(80, configService.Current.SoundVolumePercent);

            // 마이그레이션 결과가 파일에 저장돼 다시 읽어도 다시 변환되지 않는다(구 번호 8은 새 체계에서 존재하지 않으므로 6 유지).
            var reloaded = new ConfigService();
            Assert.Equal(6, reloaded.Current.SoundPreset);
            Assert.True(reloaded.Current.SoundPresetMigratedV2);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
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
