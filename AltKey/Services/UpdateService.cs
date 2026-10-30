using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace AltKey.Services;

/// <summary>
/// [역할] 인터넷(GitHub)에서 AltKey의 최신 버전을 확인하고 업데이트 정보를 가져오는 서비스입니다.
/// </summary>
public class UpdateService
{
    private const string ApiUrl =
        "https://api.github.com/repos/CrowKing63/altkey/releases/latest"; // 최신 출시 정보를 가져올 GitHub API 주소입니다.

    public async Task<(bool HasUpdate, string Version, string Url, string InstallerUrl)> CheckAsync()
    {
        // [업데이트] 스토어(MSIX) 설치판은 Microsoft Store가 자동으로 업데이트하므로 GitHub 확인을 하지 않는다.
        // 이 단일 쉐크포인트에서 막아 두므로, 어떤 호출 경로(시작·트레이·설정)에서도 스토어판이 GitHub를 조회하지 못한다.
        if (PathResolver.IsMsix)
            return (false, string.Empty, string.Empty, string.Empty);

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
