using AltKey.Services;
using System.IO;

namespace AltKey.Tests.Services;

// AI 백엔드 마이그레이션: AiBackend 키가 없는 구버전 config.json을 읽을 때
// 기존 API 사용자의 동작이 "api"로 보존되는지 확인하는 테스트입니다.
public class AiBackendMigrationTests
{
    [Fact]
    // ai_backend 키가 없으면서 엔드포인트가 채워져 있으면 "api"로 유지되는지 확인합니다.
    public void Load_legacy_config_with_endpoint_keeps_api_backend()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"AltKey-AiBackendMigration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            PathResolver.OverrideDataDir(tempDir);

            // 구버전 파일 흉내: ai_backend 키 없이 엔드포인트만 저장된 상태.
            File.WriteAllText(
                PathResolver.ConfigPath,
                """
                {
                  "ai_endpoint": "http://localhost:11434"
                }
                """);

            var service = new ConfigService();

            Assert.Equal("api", service.Current.AiBackend);
        }
        finally
        {
            PathResolver.OverrideDataDir(null);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    // ai_backend 키가 없고 엔드포인트도 비어 있으면 새 기본값 "local"이 적용되는지 확인합니다.
    public void Load_legacy_config_without_endpoint_uses_local_default()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"AltKey-AiBackendMigration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            PathResolver.OverrideDataDir(tempDir);

            File.WriteAllText(PathResolver.ConfigPath, "{}");

            var service = new ConfigService();

            Assert.Equal("local", service.Current.AiBackend);
        }
        finally
        {
            PathResolver.OverrideDataDir(null);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    // ai_backend 키가 명시적으로 저장되어 있으면(어떤 값이든) 그대로 유지되는지 확인합니다.
    public void Load_explicit_ai_backend_is_untouched()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"AltKey-AiBackendMigration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            PathResolver.OverrideDataDir(tempDir);

            // 엔드포인트가 있어도 키가 명시되어 있으면 마이그레이션이 개입하지 않아야 합니다.
            File.WriteAllText(
                PathResolver.ConfigPath,
                """
                {
                  "ai_backend": "api",
                  "ai_endpoint": "http://localhost:11434"
                }
                """);

            var service = new ConfigService();

            Assert.Equal("api", service.Current.AiBackend);
        }
        finally
        {
            PathResolver.OverrideDataDir(null);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
