using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// [역할] 앱의 설정(config.json)을 로드하고 저장하며, 변경 사항을 관리하는 서비스입니다.
/// [기능] 파일 읽기/쓰기, 설정값 업데이트 알림, 이전 버전 설정 파일의 호환성 유지(Migration)를 처리합니다.
/// </summary>
public class ConfigService
{
    private const string LegacyDefaultLayoutName = "Bagic";
    private const string LegacyDefaultLayoutPlusName = "Bagic Plus";
    private const string CurrentDefaultLayoutName = "Basic";
    private const string CurrentDefaultLayoutPlusName = "Basic Plus";

    /// 현재 앱에 적용된 모든 설정 데이터입니다.
    public AppConfig Current { get; private set; } = new();

    /// <summary>설정 값이 변경되었을 때 발생하는 이벤트입니다.</summary>
    /// <param name="propertyName">변경된 속성의 이름입니다. 전체가 바뀌었으면 null입니다.</param>
    public event Action<string?>? ConfigChanged;

    public ConfigService()
    {
        // 설정 파일이 저장될 폴더가 없다면 생성합니다.
        Directory.CreateDirectory(Path.GetDirectoryName(PathResolver.ConfigPath)!);
        Load();
    }

    /// <summary>
    /// config.json 파일에서 설정을 불러옵니다. 파일이 없으면 기본 설정으로 파일을 생성합니다.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(PathResolver.ConfigPath)) { Save(); return; }
        try
        {
            var json = File.ReadAllText(PathResolver.ConfigPath);
            Current = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default) ?? new();
            MigrateWindowConfig(json); // 예전 버전의 설정 형식을 최신 형식으로 변환합니다.
            MigrateLegacyLayoutNames();

            // 상단바 버튼 목록이 비어 있으면 기본 구성을 넣고 저장합니다.
            // (이전에는 MainViewModel 생성 후에만 기본값이 생겨, 첫 실행에서 설정 창 목록이 비어 보이는 문제가 있었습니다.)
            Current.HeaderButtons ??= [];
            if (Current.HeaderButtons.Count == 0)
            {
                Current.HeaderButtons = HeaderButtonConfig.CreateDefaults();
                Save();
            }
            else
            {
                NormalizeHeaderButtons();
            }
        }
        catch
        {
            Current = new AppConfig();
        }
    }

    /// <summary>
    /// 외부 프로세스가 설정 파일을 저장한 뒤, 메모리 설정을 파일 기준으로 다시 읽고 변경 알림까지 보냅니다.
    /// propertyName을 null로 주면 "어느 한 항목이 아니라 설정 전반이 바뀌었다"는 의미로 해석할 수 있습니다.
    /// </summary>
    public void ReloadFromDiskAndNotify(string? propertyName = null)
    {
        Load();
        ConfigChanged?.Invoke(propertyName);
    }

    /// <summary>
    /// 예전 버전(Width/Height 기반)의 설정을 최신 버전(배율 Scale 기반)으로 변환해주는 도우미 함수입니다.
    /// </summary>
    private void MigrateWindowConfig(string json)
    {
        try
        {
            var node = JsonNode.Parse(json)?["Window"]?.AsObject();
            if (node == null) return;
            if (node.ContainsKey("Scale")) return;

            if (node.TryGetPropertyValue("Width", out var widthNode) && widthNode != null)
            {
                var width = (double)widthNode;
                var scale = (int)Math.Round(width / 900.0 * 100);
                Current.Window.Scale = Math.Clamp(scale, 60, 200);
            }
        }
        catch { }
    }

    /// <summary>
    /// 예전 기본 제공 레이아웃 오타(Bagic)를 현재 이름(Basic)으로 맞춰
    /// 기존 사용자 설정과 앱별 프로필 매핑이 배포 후에도 그대로 이어지도록 정리합니다.
    /// </summary>
    private void MigrateLegacyLayoutNames()
    {
        Current.DefaultLayout = MigrateLegacyLayoutName(Current.DefaultLayout);

        if (Current.Profiles.Count == 0)
            return;

        var migratedProfiles = new Dictionary<string, string>(Current.Profiles.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in Current.Profiles)
            migratedProfiles[pair.Key] = MigrateLegacyLayoutName(pair.Value);

        Current.Profiles = migratedProfiles;
    }

    /// <summary>
    /// 기본 제공 레이아웃 이름만 현재 표기로 교정하고, 사용자가 만든 다른 이름은 유지합니다.
    /// </summary>
    private static string MigrateLegacyLayoutName(string? layoutName)
    {
        return layoutName switch
        {
            LegacyDefaultLayoutName => CurrentDefaultLayoutName,
            LegacyDefaultLayoutPlusName => CurrentDefaultLayoutPlusName,
            _ => layoutName ?? CurrentDefaultLayoutName
        };
    }

    /// <summary>
    /// 과거 버전 설정 파일에도 새 상단바 속성을 안전하게 채워 넣습니다.
    /// 기존 기본 버튼은 Kind가 비어 있어도 BuiltIn으로 해석하고, 커스텀 버튼은 최소 식별 정보를 보정합니다.
    /// </summary>
    private void NormalizeHeaderButtons()
    {
        foreach (var button in Current.HeaderButtons)
        {
            button.Position = HeaderButtonConfig.NormalizePosition(button.Position);
            button.DisplayMode = HeaderButtonDisplayMode.IconOnly;

            if (button.Kind == HeaderButtonKind.BuiltIn && !HeaderButtonConfig.IsBuiltInId(button.Id))
            {
                button.Kind = HeaderButtonKind.Custom;
            }

            if (button.Kind == HeaderButtonKind.Custom)
            {
                if (string.IsNullOrWhiteSpace(button.Id))
                    button.Id = HeaderButtonConfig.CreateCustomDefault().Id;

                button.IconText = string.IsNullOrWhiteSpace(button.IconText) ? "새" : button.IconText.Trim();
                button.Tooltip = string.IsNullOrWhiteSpace(button.Tooltip) ? "커스텀 상단바 단축키" : button.Tooltip.Trim();
                button.AccessibleName = string.IsNullOrWhiteSpace(button.AccessibleName) ? button.Tooltip : button.AccessibleName.Trim();
                button.CustomAction ??= new SendKeyAction("VK_A");
            }
        }
    }

    /// <summary>
    /// 현재 설정 내용을 config.json 파일에 물리적으로 저장합니다.
    /// 다른 프로세스가 파일을 사용 중일 수 있으므로 실패 시 몇 번 재시도합니다.
    /// </summary>
    public void Save()
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                File.WriteAllText(PathResolver.ConfigPath,
                    JsonSerializer.Serialize(Current, JsonOptions.Default));
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(300); // 잠시 기다린 후 재시도
            }
        }
    }

    /// <summary>
    /// 설정을 안전하게 변경하고 즉시 파일에 저장하며, 변경되었음을 다른 컴포넌트들에 알립니다.
    /// </summary>
    /// <param name="updater">설정 값을 수정하는 함수</param>
    /// <param name="propertyName">수정한 속성 이름</param>
    public void Update(Action<AppConfig> updater, string? propertyName = null)
    {
        updater(Current);
        Save();
        ConfigChanged?.Invoke(propertyName);
    }
}
