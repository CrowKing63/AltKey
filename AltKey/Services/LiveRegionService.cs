namespace AltKey.Services;

public sealed class LiveRegionService
{
    public event Action<string>? Announced;

    public void Announce(string message) => Announced?.Invoke(message);
}
