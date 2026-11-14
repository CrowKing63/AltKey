using AltKey.Models;

namespace AltKey.Tests.AiLocal;

// 로컬 AI 기능 관련 AppConfig 기본값을 검증하는 테스트입니다.
public class AppConfigAiBackendTests
{
    [Fact]
    // 새 설정의 AI 백엔드 기본값이 "local"(로컬 우선)인지 확인합니다.
    public void NewConfig_defaults_aiBackend_to_local()
    {
        var config = new AppConfig();
        Assert.Equal("local", config.AiBackend);
    }

    [Fact]
    // 로컬 모델 유휴 언로드 기본값이 10분인지 확인합니다.
    public void NewConfig_defaults_localIdleUnload_to_10_minutes()
    {
        var config = new AppConfig();
        Assert.Equal(10, config.AiLocalIdleUnloadMinutes);
    }
}
