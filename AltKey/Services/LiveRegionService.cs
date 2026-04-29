namespace AltKey.Services;

public sealed class LiveRegionService
{
    public event Action<string>? Announced;
    
    private string _lastMessage = "";
    private DateTime _lastAnnouncedAtUtc = DateTime.MinValue;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// [접근성] 같은 메시지가 짧은 시간(기본 500ms) 안에 반복될 때 공지 스팸을 줄입니다.
    /// </summary>
    public void Announce(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var now = DateTime.UtcNow;
        if (message == _lastMessage && (now - _lastAnnouncedAtUtc) < DuplicateWindow)
            return;

        _lastMessage = message;
        _lastAnnouncedAtUtc = now;
        Announced?.Invoke(message);
    }
}
