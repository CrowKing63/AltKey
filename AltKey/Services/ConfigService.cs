using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// [역할] 앱 설정(config.json)을 읽고 저장하며, 변경 알림과 마이그레이션을 담당합니다.
/// [기능] 구버전 설정 보정, 상단바 버튼 정규화, 파일 저장 재시도까지 한곳에서 처리합니다.
/// </summary>
public class ConfigService
{
    private const string LegacyDefaultLayoutName = "Bagic";
    private const string LegacyDefaultLayoutPlusName = "Bagic Plus";
    private const string CurrentDefaultLayoutName = "Basic";
    private const string CurrentDefaultLayoutPlusName = "Basic Plus";

    /// <summary>현재 앱에 적용 중인 설정 데이터입니다.</summary>
    public AppConfig Current { get; private set; } = new();

    /// <summary>설정이 바뀌면 발생하는 알림입니다.</summary>
    /// <param name="propertyName">변경된 속성 이름입니다. 전체 변경이면 null입니다.</param>
    public event Action<string?>? ConfigChanged;

    public ConfigService()
    {
        // 설정 파일 폴더가 없으면 먼저 만들어, 첫 실행에서도 저장이 실패하지 않게 합니다.
        Directory.CreateDirectory(Path.GetDirectoryName(PathResolver.ConfigPath)!);
        Load();
    }

    /// <summary>
    /// config.json을 읽어 현재 설정으로 반영합니다.
    /// 파일이 없으면 기본값을 그대로 저장해 이후 편집 기준 파일을 만듭니다.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(PathResolver.ConfigPath))
        {
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(PathResolver.ConfigPath);
            Current = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default) ?? new();

            MigrateWindowConfig(json);
            MigrateLegacyLayoutNames();

            // 상단바 버튼 목록은 설정 창과 메인 화면 모두 즉시 써야 하므로,
            // 비어 있으면 여기서 바로 기본값을 채우고 저장합니다.
            Current.HeaderButtons ??= [];
            if (Current.HeaderButtons.Count == 0)
            {
                Current.HeaderButtons = HeaderButtonConfig.CreateDefaults();
                Save();
                return;
            }

            if (NormalizeHeaderButtons())
            {
                Save();
            }
        }
        catch
        {
            Current = new AppConfig();
        }
    }

    /// <summary>
    /// 다른 프로세스가 설정 파일을 저장한 뒤, 현재 메모리 값을 파일 기준으로 다시 읽고 변경 알림까지 보냅니다.
    /// </summary>
    public void ReloadFromDiskAndNotify(string? propertyName = null)
    {
        Load();
        ConfigChanged?.Invoke(propertyName);
    }

    /// <summary>
    /// 과거 Width/Height 기반 창 설정을 현재 Scale 기반 설정으로 옮깁니다.
    /// </summary>
    private void MigrateWindowConfig(string json)
    {
        try
        {
            var node = JsonNode.Parse(json)?["Window"]?.AsObject();
            if (node == null || node.ContainsKey("Scale"))
                return;

            if (node.TryGetPropertyValue("Width", out var widthNode) && widthNode != null)
            {
                var width = (double)widthNode;
                var scale = (int)Math.Round(width / 900.0 * 100);
                Current.Window.Scale = Math.Clamp(scale, 60, 200);
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// 과거 기본 레이아웃 이름(Bagic)을 현재 이름(Basic)으로 맞춥니다.
    /// 사용자 커스텀 이름은 건드리지 않고, 기본 이름에만 보정 규칙을 적용합니다.
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
    /// 기본 레이아웃 이름만 현재 표기로 교정하고, 사용자가 만든 다른 이름은 그대로 둡니다.
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
    /// 구버전 설정도 현재 상단바 규칙에 맞게 보정합니다.
    /// 커스텀 버튼 기본값을 채우고, 중앙 이동/드래그 영역을 침범할 수 있는 초과 표시 항목은 자동으로 숨깁니다.
    /// </summary>
    private bool NormalizeHeaderButtons()
    {
        var changed = false;

        foreach (var button in Current.HeaderButtons)
        {
            var normalizedPosition = HeaderButtonConfig.NormalizePosition(button.Position);
            if (button.Position != normalizedPosition)
            {
                button.Position = normalizedPosition;
                changed = true;
            }

            if (button.DisplayMode != HeaderButtonDisplayMode.IconOnly)
            {
                button.DisplayMode = HeaderButtonDisplayMode.IconOnly;
                changed = true;
            }

            if (button.Kind == HeaderButtonKind.BuiltIn && !HeaderButtonConfig.IsBuiltInId(button.Id))
            {
                button.Kind = HeaderButtonKind.Custom;
                changed = true;
            }

            if (button.Kind != HeaderButtonKind.Custom)
                continue;

            if (string.IsNullOrWhiteSpace(button.Id))
            {
                button.Id = HeaderButtonConfig.CreateCustomDefault().Id;
                changed = true;
            }

            var iconText = string.IsNullOrWhiteSpace(button.IconText) ? "새" : button.IconText.Trim();
            if (button.IconText != iconText)
            {
                button.IconText = iconText;
                changed = true;
            }

            var tooltip = string.IsNullOrWhiteSpace(button.Tooltip) ? "커스텀 상단바 단축키" : button.Tooltip.Trim();
            if (button.Tooltip != tooltip)
            {
                button.Tooltip = tooltip;
                changed = true;
            }

            var accessibleName = string.IsNullOrWhiteSpace(button.AccessibleName) ? button.Tooltip : button.AccessibleName.Trim();
            if (button.AccessibleName != accessibleName)
            {
                button.AccessibleName = accessibleName;
                changed = true;
            }

            if (button.CustomAction is null)
            {
                button.CustomAction = new SendKeyAction("VK_A");
                changed = true;
            }
        }

        // 같은 쪽에 버튼이 너무 많으면 중앙 이동/드래그 조작부를 덮을 수 있으므로,
        // 목록 순서상 뒤에 있는 항목부터 숨겨 기존 사용자의 배치 순서를 최대한 보존합니다.
        var visibleLeft = 0;
        var visibleRight = 0;
        foreach (var button in Current.HeaderButtons)
        {
            if (!button.Visible)
                continue;

            if (HeaderButtonConfig.NormalizePosition(button.Position) == "Left")
            {
                if (visibleLeft >= HeaderButtonConfig.MaxVisibleButtonsLeft)
                {
                    button.Visible = false;
                    changed = true;
                    continue;
                }

                visibleLeft++;
                continue;
            }

            if (visibleRight >= HeaderButtonConfig.MaxVisibleButtonsRight)
            {
                button.Visible = false;
                changed = true;
                continue;
            }

            visibleRight++;
        }

        return changed;
    }

    /// <summary>
    /// 현재 설정을 config.json에 저장합니다.
    /// 잠깐 파일 사용 충돌이 나면 몇 번 재시도해, 외부 도구와 함께 써도 저장 성공률을 높입니다.
    /// </summary>
    public void Save()
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                File.WriteAllText(
                    PathResolver.ConfigPath,
                    JsonSerializer.Serialize(Current, JsonOptions.Default));
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(300);
            }
        }
    }

    /// <summary>
    /// 설정을 안전하게 수정하고 즉시 저장한 뒤, 필요한 화면이 다시 읽을 수 있도록 변경 알림을 보냅니다.
    /// </summary>
    public void Update(Action<AppConfig> updater, string? propertyName = null)
    {
        updater(Current);
        Save();
        ConfigChanged?.Invoke(propertyName);
    }
}
