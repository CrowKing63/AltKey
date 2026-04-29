using AltKey.Services;

namespace AltKey.Tests.Services;

public class LiveRegionServiceTests
{
    [Fact]
    public void Announce_blocks_same_message_within_500ms()
    {
        var service = new LiveRegionService();
        var received = new List<string>();
        service.Announced += msg => received.Add(msg);

        service.Announce("현재 포커스 A");
        service.Announce("현재 포커스 A");

        Assert.Single(received);
        Assert.Equal("현재 포커스 A", received[0]);
    }

    [Fact]
    public void Announce_allows_different_message_even_within_500ms()
    {
        var service = new LiveRegionService();
        var received = new List<string>();
        service.Announced += msg => received.Add(msg);

        service.Announce("현재 포커스 A");
        service.Announce("현재 포커스 B");

        Assert.Equal(2, received.Count);
    }
}
