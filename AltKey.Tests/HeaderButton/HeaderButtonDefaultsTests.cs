using AltKey.Models;
using AltKey.Services;
using System.IO;
using System.Text.Json;

namespace AltKey.Tests.HeaderButton;

// 상단바 버튼 기본값 채우기가 최초 실행과 마이그레이션에서 모두 일어나는지 확인하는 테스트입니다.
public class HeaderButtonDefaultsTests
{
    [Fact]
    // 설정 파일이 없는 최초 실행에서도 기본 버튼 목록이 바로 채워지고 파일에 저장되는지 확인합니다.
    public void Load_fills_default_header_buttons_on_first_run()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"AltKey-HeaderButtonDefaults-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            PathResolver.OverrideDataDir(tempDir);

            var service = new ConfigService();

            Assert.Equal(HeaderButtonConfig.CreateDefaults().Count, service.Current.HeaderButtons.Count);
            Assert.All(service.Current.HeaderButtons, button => Assert.Equal(HeaderButtonKind.BuiltIn, button.Kind));

            // 기본 목록이 설정 파일에도 저장되어 이후 실행에서 그대로 읽히는지 확인합니다.
            var savedJson = File.ReadAllText(PathResolver.ConfigPath);
            var saved = JsonSerializer.Deserialize<AppConfig>(savedJson, JsonOptions.Default);
            Assert.Equal(HeaderButtonConfig.CreateDefaults().Count, saved?.HeaderButtons.Count);
        }
        finally
        {
            PathResolver.OverrideDataDir(null);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    // 설정 파일은 있는데 상단바 버튼 목록이 비어 있으면 기본 버튼 목록으로 채워지는지 확인합니다.
    public void Load_fills_default_header_buttons_when_file_has_empty_list()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"AltKey-HeaderButtonDefaults-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            PathResolver.OverrideDataDir(tempDir);

            var config = new AppConfig
            {
                HeaderButtons = []
            };
            File.WriteAllText(
                PathResolver.ConfigPath,
                JsonSerializer.Serialize(config, JsonOptions.Default));

            var service = new ConfigService();

            Assert.Equal(HeaderButtonConfig.CreateDefaults().Count, service.Current.HeaderButtons.Count);
        }
        finally
        {
            PathResolver.OverrideDataDir(null);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
