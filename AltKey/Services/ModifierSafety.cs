namespace AltKey.Services;

internal static class ModifierSafety
{
    internal static void PrepareForWindowHide(InputService inputService, string source)
    {
        inputService.ReleaseHighRiskModifiers($"{source}:hide");
    }

    internal static void PrepareForAppExit(InputService inputService, string source)
    {
        inputService.ReleaseAllModifiers($"{source}:exit");
    }
}
