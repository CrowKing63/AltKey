using AltKey.Services;

namespace AltKey.Tests;

public class OskLauncherServiceTests
{
    private sealed class TrackingOskLauncherService : OskLauncherService
    {
        private readonly Func<string, bool> _launchResult;
        private readonly bool _isRunning;

        public TrackingOskLauncherService(Func<string, bool> launchResult, bool isRunning = false)
        {
            _launchResult = launchResult;
            _isRunning = isRunning;
        }

        public List<string> AttemptedCandidates { get; } = [];

        // 테스트에서 열림/닫힘 상태를 고정해 토글 결정만 검증합니다.
        public override bool IsRunning() => _isRunning;

        protected override IEnumerable<string> EnumerateCandidates()
        {
            yield return @"C:\missing\osk.exe";
            yield return @"C:\Windows\System32\osk.exe";
            yield return "osk.exe";
        }

        protected override bool TryLaunchCandidate(string candidate)
        {
            AttemptedCandidates.Add(candidate);
            return _launchResult(candidate);
        }
    }

    [Fact]
    public void TryLaunch_returns_true_when_first_candidate_succeeds()
    {
        var service = new TrackingOskLauncherService(candidate => candidate == @"C:\missing\osk.exe");

        var launched = service.TryLaunch();

        Assert.True(launched);
        Assert.Equal([@"C:\missing\osk.exe"], service.AttemptedCandidates);
    }

    [Fact]
    public void TryLaunch_stops_after_first_successful_fallback_candidate()
    {
        var service = new TrackingOskLauncherService(candidate => candidate == @"C:\Windows\System32\osk.exe");

        var launched = service.TryLaunch();

        Assert.True(launched);
        Assert.Equal(
            [@"C:\missing\osk.exe", @"C:\Windows\System32\osk.exe"],
            service.AttemptedCandidates);
    }

    [Fact]
    public void TryLaunch_returns_false_when_all_candidates_fail()
    {
        var service = new TrackingOskLauncherService(_ => false);

        var launched = service.TryLaunch();

        Assert.False(launched);
        Assert.Equal(
            [@"C:\missing\osk.exe", @"C:\Windows\System32\osk.exe", "osk.exe"],
            service.AttemptedCandidates);
    }

    [Fact]
    public void Toggle_when_osk_running_sends_hotkey_once_and_skips_launch()
    {
        // 열려 있는 OSK는 단축키로 닫고, 재실행은 시도하지 않는지 검증합니다.
        var service = new TrackingOskLauncherService(_ => true, isRunning: true);
        int hotkeyCalls = 0;

        var result = service.Toggle(() => hotkeyCalls++);

        Assert.Equal(OskToggleResult.Close, result);
        Assert.Equal(1, hotkeyCalls);
        Assert.Empty(service.AttemptedCandidates);
    }

    [Fact]
    public void Toggle_when_osk_closed_launches_without_hotkey()
    {
        // 닫혀 있는 OSK는 직접 실행으로 열고, 단축키는 보내지 않는지 검증합니다.
        var service = new TrackingOskLauncherService(candidate => candidate == @"C:\missing\osk.exe");
        int hotkeyCalls = 0;

        var result = service.Toggle(() => hotkeyCalls++);

        Assert.Equal(OskToggleResult.Open, result);
        Assert.Equal(0, hotkeyCalls);
        Assert.Equal([@"C:\missing\osk.exe"], service.AttemptedCandidates);
    }

    [Fact]
    public void Toggle_when_launch_fails_falls_back_to_hotkey()
    {
        // 직접 실행이 모두 실패하면 단축키 폴백으로 열기를 시도하는지 검증합니다.
        var service = new TrackingOskLauncherService(_ => false);
        int hotkeyCalls = 0;

        var result = service.Toggle(() => hotkeyCalls++);

        Assert.Equal(OskToggleResult.Open, result);
        Assert.Equal(1, hotkeyCalls);
        Assert.Equal(
            [@"C:\missing\osk.exe", @"C:\Windows\System32\osk.exe", "osk.exe"],
            service.AttemptedCandidates);
    }
}
