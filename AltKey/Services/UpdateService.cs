using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace AltKey.Services;

/// <summary>T-6.4: GitHub Release 자동 업데이트 체크</summary>
public class UpdateService
{
    private const string ApiUrl =
        "https://api.github.com/repos/CrowKing63/altkey/releases/latest";

    public async Task<(bool HasUpdate, string Version, string Url)> CheckAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AltKey");

            var json = await client.GetStringAsync(ApiUrl);
            var doc  = JsonDocument.Parse(json);
            var tag  = doc.RootElement.GetProperty("tag_name").GetString()!;
            var url  = doc.RootElement.GetProperty("html_url").GetString()!;

            var current = Assembly.GetExecutingAssembly().GetName().Version
                          ?? new Version(0, 1, 0);
            var remote  = Version.Parse(tag.TrimStart('v'));

            return (remote > current, tag, url);
        }
        catch
        {
            return (false, string.Empty, string.Empty);
        }
    }
}
