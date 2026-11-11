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

    private static SettingsViewModel CreateSettingsViewModel(ConfigService configService)
    {
        var localBackend = new LocalGgufBackend(new SidecarProcessManager(), new ModelStore(), configService);
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
            new AiService(configService, localBackend),
            new ModelStore(),
            localBackend);
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
