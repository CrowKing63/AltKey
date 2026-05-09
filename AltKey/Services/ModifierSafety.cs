namespace AltKey.Services;

internal static class ModifierSafety
{
    internal static void PrepareForWindowHide(InputService inputService, string source)
    {
        inputService.ReleaseAllHeldKeys($"{source}:hide-held");
        inputService.ReleaseHighRiskModifiers($"{source}:hide");
    }

    internal static void PrepareForAppExit(InputService inputService, string source)
    {
        inputService.ReleaseAllHeldKeys($"{source}:exit-held");
        inputService.ReleaseAllModifiers($"{source}:exit");
    }
}
