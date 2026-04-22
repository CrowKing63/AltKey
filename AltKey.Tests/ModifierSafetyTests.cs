using AltKey.Services;

namespace AltKey.Tests;

public class ModifierSafetyTests
{
    private sealed class TrackingInputService : InputService
    {
        public List<string> ReleaseAllReasons { get; } = [];
        public List<string> ReleaseHighRiskReasons { get; } = [];

        public override void ReleaseAllModifiers(string reason = "manual")
        {
            ReleaseAllReasons.Add(reason);
        }

        public override void ReleaseHighRiskModifiers(string reason)
        {
            ReleaseHighRiskReasons.Add(reason);
        }
    }

    [Fact]
    public void PrepareForWindowHide_releases_high_risk_modifiers()
    {
        var svc = new TrackingInputService();

        ModifierSafety.PrepareForWindowHide(svc, "TrayService.ToggleVisibility");

        Assert.Contains("TrayService.ToggleVisibility:hide", svc.ReleaseHighRiskReasons);
        Assert.Empty(svc.ReleaseAllReasons);
    }

    [Fact]
    public void PrepareForAppExit_releases_all_modifiers()
    {
        var svc = new TrackingInputService();

        ModifierSafety.PrepareForAppExit(svc, "App.OnExit");

        Assert.Contains("App.OnExit:exit", svc.ReleaseAllReasons);
        Assert.Empty(svc.ReleaseHighRiskReasons);
    }
}
