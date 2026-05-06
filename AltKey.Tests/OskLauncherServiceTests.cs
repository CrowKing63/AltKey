using AltKey.Services;

namespace AltKey.Tests;

public class OskLauncherServiceTests
{
    private sealed class TrackingOskLauncherService : OskLauncherService
    {
        private readonly Func<string, bool> _launchResult;

        public TrackingOskLauncherService(Func<string, bool> launchResult)
        {
            _launchResult = launchResult;
        }

        public List<string> AttemptedCandidates { get; } = [];

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
}
