using AltKey.Services;
using Xunit;

namespace AltKey.Tests.ScratchPad;

/// <summary>스크래치패드 저장/로드(ScratchPadService)가 디바운스·손상 파일·종료 플러시 규칙을 지킬지 검증합니다.</summary>
public class ScratchPadServiceTests
{
    private static string MakeTempPath() => Path.Combine(Path.GetTempPath(), $"scratchpad-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void 저장_파일_없으면_빈_문자열()
    {
        using var service = new ScratchPadService(MakeTempPath());
        Assert.Equal("", service.Load());
    }

    [Fact]
    public void SaveNow로_저장하면_Load가_동일_내용()
    {
        string path = MakeTempPath();
        using (var service = new ScratchPadService(path))
        {
            service.ScheduleSave("안녕, 스크래치패드");
            service.SaveNow();
        }

        using var reloaded = new ScratchPadService(path);
        Assert.Equal("안녕, 스크래치패드", reloaded.Load());
    }

    [Fact]
    public void 손상된_JSON이면_빈_상태()
    {
        string path = MakeTempPath();
        File.WriteAllText(path, "{ \"text\": 끊김");

        using var service = new ScratchPadService(path);
        Assert.Equal("", service.Load());
    }

    [Fact]
    // 디바운스 시간이 지나면 마지막 값만 저장되는지 확인합니다.
    public void 디바운스_시간이_지나면_마지막_값만_저장()
    {
        string path = MakeTempPath();
        using var service = new ScratchPadService(path, debounceMs: 50);

        service.ScheduleSave("첫 번째");
        service.ScheduleSave("두 번째"); // 디바운스 창 안에서 다시 호출 — 타이핑 연속 상황
        Thread.Sleep(300);

        // 저장 파일은 JSON이므로 공개 API(Load)로 다시 읽어 비교합니다.
        using var reloaded = new ScratchPadService(path);
        Assert.Equal("두 번째", reloaded.Load());
    }

    [Fact]
    public void 변경_없으면_SaveNow가_파일을_만들지_않음()
    {
        string path = MakeTempPath();
        using var service = new ScratchPadService(path);

        service.SaveNow();

        Assert.False(File.Exists(path));
    }

    [Fact]
    // Dispose(앱 종료) 시 대기 중이던 내용이 즉시 저장되는지 확인합니다.
    public void Dispose는_대기_중인_내용을_즉시_저장()
    {
        string path = MakeTempPath();
        var service = new ScratchPadService(path, debounceMs: 60_000); // 디바운스가 도래하지 않는 긴 시간

        service.ScheduleSave("종료 직전 타이핑");
        service.Dispose();

        using var reloaded = new ScratchPadService(path);
        Assert.Equal("종료 직전 타이핑", reloaded.Load());
    }
}
