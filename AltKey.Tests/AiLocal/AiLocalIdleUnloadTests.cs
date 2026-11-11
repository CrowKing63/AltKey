using System.IO;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey.Tests.AiLocal;

// 로컬 AI idle 언로드 시간(AiLocalIdleUnloadMinutes) 설정의 저장/불러오기 동작을 검증합니다.
public class AiLocalIdleUnloadTests
{
    [Fact]
    // ViewModel에서 idle 언로드 시간을 바꿨을 때 설정에 저장되는지 확인합니다.
    public void SettingsViewModel_saves_idle_unload_minutes_to_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();

            var viewModel = CreateSettingsViewModel(configService);
            viewModel.AiLocalIdleUnloadMinutes = 15;

            Assert.Equal(15, configService.Current.AiLocalIdleUnloadMinutes);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // 설정 파일의 idle 언로드 시간이 ViewModel에 그대로 반영되는지 확인합니다.
    public void SettingsViewModel_loads_idle_unload_minutes_from_config()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            configService.Update(config => config.AiLocalIdleUnloadMinutes = 25);

            var viewModel = CreateSettingsViewModel(configService);

            Assert.Equal(25, viewModel.AiLocalIdleUnloadMinutes);
        }
        finally
        {
            ResetDataDir(tempDir);
        }
    }

    [Fact]
    // idle 언로드 시간을 0(즉시 언로드)으로 저장해도 재불러오기 때 10으로复位되지 않고 그대로 유지되는지 확인합니다.
    public void SettingsViewModel_keeps_zero_idle_unload_minutes_on_load()
    {
        var tempDir = CreateTempDataDir();

        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var configService = new ConfigService();
            configService.Update(config => config.AiLocalIdleUnloadMinutes = 0);

            var viewModel = CreateSettingsViewModel(configService);

            Assert.Equal(0, viewModel.AiLocalIdleUnloadMinutes);
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
