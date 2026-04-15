using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace AltKey.Services;

/// <summary>T-6.4: GitHub Release 자동 업데이트 체크</summary>
public class UpdateService
{
    private const string ApiUrl =
        "https://api.github.com/repos/CrowKing63/altkey/releases/latest";

    public async Task<(bool HasUpdate, string Version, string Url, string InstallerUrl)> CheckAsync()
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

            // T-9.5: 설치형 앱을 위한 인스톨러 URL 추출
            var installerUrl = ExtractInstallerUrl(doc.RootElement);

            return (remote > current, tag, url, installerUrl);
        }
        catch
        {
            return (false, string.Empty, string.Empty, string.Empty);
        }
    }

    /// <summary>GitHub 릴리즈 assets에서 인스톨러(.exe) URL 추출</summary>
    private static string ExtractInstallerUrl(JsonElement root)
    {
        try
        {
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.StartsWith("AltKey-Setup-") && name.EndsWith(".exe"))
                    {
                        return asset.GetProperty("browser_download_url").GetString()!;
                    }
                }
            }
        }
        catch
        {
            // 파싱 실패 시 빈 문자열 반환
        }
        return string.Empty;
    }
}
