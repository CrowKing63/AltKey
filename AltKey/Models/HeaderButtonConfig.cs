using System.Text.Json.Serialization;

namespace AltKey.Models;

public enum HeaderButtonKind
{
    BuiltIn,
    Custom
}

public enum HeaderButtonDisplayMode
{
    IconOnly
}

/// <summary>
/// [역할] 상단바(헤더)에 표시되는 버튼 하나의 설정을 담습니다.
/// [기능] 기본 버튼과 사용자 커스텀 버튼을 같은 리스트에서 관리해 표시 여부, 순서, 좌우 위치를 함께 저장합니다.
/// [참고] 접기/닫기 버튼은 이 설정과 무관하게 항상 우측 끝에 고정됩니다.
/// </summary>
public class HeaderButtonConfig
{
    /// <summary>
    /// 사용자가 만들 수 있는 커스텀 상단바 단축키의 최대 개수입니다.
    /// 너무 많은 항목이 쌓이면 설정 탐색이 어려워지고, 상단바 배치 안전성도 떨어지므로 상한을 둡니다.
    /// </summary>
    public const int MaxCustomButtonCount = 10;

    /// <summary>
    /// 좌측 상단바에 동시에 표시할 수 있는 최대 버튼 수입니다.
    /// 좌우 기준을 같게 유지해 사용자가 한도를 직관적으로 이해할 수 있게 합니다.
    /// </summary>
    public const int MaxVisibleButtonsLeft = 8;

    /// <summary>
    /// 우측 상단바에 동시에 표시할 수 있는 최대 버튼 수입니다.
    /// 좌우 기준을 같게 유지해 사용자가 한도를 직관적으로 이해할 수 있게 합니다.
    /// </summary>
    public const int MaxVisibleButtonsRight = 8;

    /// <summary>
    /// 각 버튼을 식별하는 고유 ID입니다.
    /// 기본 버튼은 아래 상수를 사용하고, 커스텀 버튼은 "custom-..." 형태의 ID를 사용합니다.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// 기본 버튼인지, 사용자 커스텀 버튼인지 구분합니다.
    /// 과거 설정 파일에는 이 값이 없을 수 있으므로, 누락 시 BuiltIn으로 간주합니다.
    /// </summary>
    public HeaderButtonKind Kind { get; set; } = HeaderButtonKind.BuiltIn;

    /// <summary>
    /// 상단바에 이 버튼을 표시할지 여부입니다.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 버튼의 좌/우 배치 위치입니다. "Left" 또는 "Right"만 사용합니다.
    /// </summary>
    public string Position { get; set; } = "Right";

    /// <summary>
    /// 현재는 아이콘만 표시하는 방식만 허용합니다.
    /// 향후 표시 방식을 확장해도 기존 설정 파일을 그대로 읽을 수 있게 값을 유지합니다.
    /// </summary>
    public HeaderButtonDisplayMode DisplayMode { get; set; } = HeaderButtonDisplayMode.IconOnly;

    /// <summary>
    /// 커스텀 버튼이 상단바에 보일 짧은 아이콘형 텍스트입니다.
    /// 예: "번역", "요약", "📌"
    /// </summary>
    public string IconText { get; set; } = "";

    /// <summary>
    /// 시각 툴팁에 표시할 문구입니다.
    /// </summary>
    public string Tooltip { get; set; } = "";

    /// <summary>
    /// 스크린리더가 읽을 이름입니다.
    /// </summary>
    public string AccessibleName { get; set; } = "";

    /// <summary>
    /// 커스텀 버튼이 실행할 실제 액션입니다.
    /// 기본 버튼은 이 값을 사용하지 않습니다.
    /// </summary>
    public KeyAction? CustomAction { get; set; }

    /// <summary>
    /// 설정 창과 편집기에서 보여줄 사람이 읽는 이름입니다.
    /// </summary>
    [JsonIgnore]
    public string DisplayName => Kind == HeaderButtonKind.BuiltIn
        ? GetDisplayName(Id)
        : (string.IsNullOrWhiteSpace(Tooltip) ? "커스텀 단축키" : Tooltip);

    /// <summary>
    /// 실제 상단바에 표시할 아이콘형 텍스트입니다.
    /// </summary>
    [JsonIgnore]
    public string EffectiveIconText => Kind == HeaderButtonKind.BuiltIn
        ? GetBuiltInIconText(Id)
        : (string.IsNullOrWhiteSpace(IconText) ? "새" : IconText);

    /// <summary>
    /// 시각 툴팁의 실제 표시값입니다.
    /// </summary>
    [JsonIgnore]
    public string EffectiveTooltip => Kind == HeaderButtonKind.BuiltIn
        ? GetBuiltInTooltip(Id)
        : (string.IsNullOrWhiteSpace(Tooltip) ? "커스텀 상단바 단축키" : Tooltip);

    /// <summary>
    /// 스크린리더가 실제로 읽을 이름입니다.
    /// </summary>
    [JsonIgnore]
    public string EffectiveAccessibleName => Kind == HeaderButtonKind.BuiltIn
        ? GetBuiltInAccessibleName(Id)
        : (string.IsNullOrWhiteSpace(AccessibleName) ? EffectiveTooltip : AccessibleName);

    // ── 기본 버튼 ID 상수 ─────────────────────────────────────────────────
    public const string IdClipboard = "Clipboard";
    public const string IdEmoji = "Emoji";
    public const string IdAutoComplete = "AutoComplete";
    public const string IdOsIme = "OsIme";
    public const string IdOsk = "Osk";
    public const string IdSettings = "Settings";
    public const string IdAi = "Ai";

    /// <summary>
    /// 기본 버튼 구성 목록을 생성합니다. HeaderButtons가 비어 있을 때(최초 실행 또는 마이그레이션)에 사용됩니다.
    /// 리스트 순서가 곧 상단바 표시 순서입니다.
    /// </summary>
    public static List<HeaderButtonConfig> CreateDefaults() =>
    [
        CreateBuiltIn(IdClipboard, visible: true, position: "Right"),
        CreateBuiltIn(IdEmoji, visible: true, position: "Right"),
        CreateBuiltIn(IdAutoComplete, visible: true, position: "Right"),
        CreateBuiltIn(IdOsIme, visible: true, position: "Right"),
        CreateBuiltIn(IdOsk, visible: true, position: "Right"),
        CreateBuiltIn(IdSettings, visible: true, position: "Right"),
        CreateBuiltIn(IdAi, visible: false, position: "Right"),
    ];

    public static HeaderButtonConfig CreateBuiltIn(string id, bool visible = true, string position = "Right") => new()
    {
        Id = id,
        Kind = HeaderButtonKind.BuiltIn,
        Visible = visible,
        Position = NormalizePosition(position),
        DisplayMode = HeaderButtonDisplayMode.IconOnly
    };

    /// <summary>
    /// 새 커스텀 버튼의 시작값을 만듭니다.
    /// 저장 전에도 식별 가능한 기본 문구를 넣어 설정 창과 편집기에서 헷갈리지 않게 합니다.
    /// </summary>
    public static HeaderButtonConfig CreateCustomDefault() => new()
    {
        Id = $"custom-{Guid.NewGuid():N}",
        Kind = HeaderButtonKind.Custom,
        Visible = true,
        Position = "Right",
        DisplayMode = HeaderButtonDisplayMode.IconOnly,
        IconText = "새",
        Tooltip = "새 상단바 단축키",
        AccessibleName = "새 상단바 단축키",
        CustomAction = new SendKeyAction("VK_A")
    };

    public static bool IsBuiltInId(string id) => id is
        IdClipboard or IdEmoji or IdAutoComplete or IdOsIme or IdOsk or IdSettings or IdAi;

    public static string GetDisplayName(string id) => id switch
    {
        IdClipboard => "클립보드",
        IdEmoji => "이모지",
        IdAutoComplete => "자동완성",
        IdOsIme => "OS IME 한영",
        IdOsk => "화면 키보드",
        IdSettings => "설정",
        IdAi => "AI",
        _ => "커스텀 단축키"
    };

    public static string GetBuiltInIconText(string id) => id switch
    {
        IdClipboard => "📋",
        IdEmoji => "😊",
        IdAutoComplete => "제안",
        IdOsIme => "한/영",
        IdOsk => "⌨",
        IdSettings => "⚙",
        IdAi => "✨",
        _ => "?"
    };

    public static string GetBuiltInTooltip(string id) => id switch
    {
        IdClipboard => "클립보드 열기",
        IdEmoji => "이모지 열기",
        IdAutoComplete => "자동완성 토글",
        IdOsIme => "OS IME 한영 전환",
        IdOsk => "화면 키보드 열기",
        IdSettings => "설정 열기",
        IdAi => "AI 텍스트 처리",
        _ => GetDisplayName(id)
    };

    public static string GetBuiltInAccessibleName(string id) => id switch
    {
        IdClipboard => "클립보드 버튼",
        IdEmoji => "이모지 버튼",
        IdAutoComplete => "자동완성 토글 버튼",
        IdOsIme => "OS IME 한영 전환 버튼",
        IdOsk => "화면 키보드 버튼",
        IdSettings => "설정 버튼",
        IdAi => "AI 텍스트 처리 버튼",
        _ => GetDisplayName(id)
    };

    public static string NormalizePosition(string? position) =>
        string.Equals(position, "Left", StringComparison.OrdinalIgnoreCase) ? "Left" : "Right";

    /// <summary>
    /// 지정한 위치에 허용되는 상단바 표시 개수를 반환합니다.
    /// Left/Right 외 값은 모두 Right로 보정해 같은 기준을 적용합니다.
    /// </summary>
    public static int GetMaxVisibleButtons(string? position) =>
        NormalizePosition(position) == "Left" ? MaxVisibleButtonsLeft : MaxVisibleButtonsRight;

    /// <summary>
    /// 현재 목록에서 커스텀 단축키가 몇 개인지 셉니다.
    /// 편집 중인 항목은 제외할 수 있어, "새로 추가"와 "기존 수정" 검증을 같은 규칙으로 처리할 수 있습니다.
    /// </summary>
    public static int CountCustomButtons(IEnumerable<HeaderButtonConfig> buttons, string? excludingId = null) =>
        buttons.Count(button =>
            button.Kind == HeaderButtonKind.Custom
            && !string.Equals(button.Id, excludingId, StringComparison.Ordinal));

    /// <summary>
    /// 지정한 위치에 현재 몇 개의 버튼이 표시 중인지 셉니다.
    /// 편집 중인 버튼 하나를 빼고 계산할 수 있어, 같은 항목 저장 시 중복 계산을 막습니다.
    /// </summary>
    public static int CountVisibleButtons(IEnumerable<HeaderButtonConfig> buttons, string? position, string? excludingId = null)
    {
        var normalizedPosition = NormalizePosition(position);
        return buttons.Count(button =>
            button.Visible
            && NormalizePosition(button.Position) == normalizedPosition
            && !string.Equals(button.Id, excludingId, StringComparison.Ordinal));
    }

    // ── 저장 검증 (메인 앱·독립 편집기 공용) ────────────────────────────────
    // 아래 검증 함수는 AltKey(설정 탭)와 AltKey.Tools(상단바 단축키 편집기)가
    // 같은 기준을 쓰도록 HeaderButtonConfig에 둡니다. UI 메시지 표시는 각 앱이 담당합니다.

    /// <summary>
    /// 전체 버튼 목록이 좌우 표시 한도를 넘지 않는지 확인합니다.
    /// 설정 탭에서 체크박스·위치 변경 직후 현재 목록 전체를 검증하는 데 사용합니다.
    /// </summary>
    public static (bool valid, string? sideName) TryValidateLayout(IEnumerable<HeaderButtonConfig> buttons)
    {
        if (CountVisibleButtons(buttons, "Left") > MaxVisibleButtonsLeft)
            return (false, "좌측");
        if (CountVisibleButtons(buttons, "Right") > MaxVisibleButtonsRight)
            return (false, "우측");
        return (true, null);
    }

    /// <summary>
    /// 저장하려는 버튼 하나가 커스텀 총개수·좌우 표시 한도를 넘지 않는지 확인합니다.
    /// buttons에는 candidate가 이미 포함된 목록을 전달하고, isEditingExisting은
    /// "candidate가 기존 항목인지"를 나타냅니다. 실패하면 sideName에 꽉 찬 쪽이 들어갑니다.
    /// </summary>
    public static (bool valid, string? sideName) TryValidateSave(
        IEnumerable<HeaderButtonConfig> buttons,
        HeaderButtonConfig candidate,
        bool isEditingExisting)
    {
        // 커스텀 총개수: 저장 대상 자신을 제외하고 셉니다.
        if (!isEditingExisting
            && CountCustomButtons(buttons, candidate.Id) >= MaxCustomButtonCount)
        {
            return (false, null);
        }

        // 표시를 꺼두는 경우라면 좌우 한도와 무관합니다.
        if (!candidate.Visible)
            return (true, null);

        var maxVisibleButtons = GetMaxVisibleButtons(candidate.Position);
        var visibleOnSide = CountVisibleButtons(buttons, candidate.Position, candidate.Id);
        if (visibleOnSide >= maxVisibleButtons)
        {
            return (false, NormalizePosition(candidate.Position) == "Left" ? "좌측" : "우측");
        }

        return (true, null);
    }

    // ── 쪽별 표시 순서 이동 (순수 함수) ────────────────────────────────────
    // 상단바 표시 순서는 "쪽별로 필터링한 리스트 순서"이므로, 이동도 같은 쪽 안에서만
    // 의미 있습니다. 반대쪽 버튼을 건너뛴 이동은 표시에 영향을 주지 못합니다.

    /// <summary>
    /// 지정한 버튼을 같은 쪽의 표시 순서 안에서 한 칸 이동(up=true면 앞)한 새 목록을 반환합니다.
    /// 그쪽에 버튼이 하나뿐이면 아무것도 바꾸지 않습니다.
    /// </summary>
    public static List<HeaderButtonConfig> MoveWithinSide(List<HeaderButtonConfig> buttons, string id, bool up)
    {
        var result = new List<HeaderButtonConfig>(buttons);
        var index = result.FindIndex(button => button.Id == id);
        if (index < 0)
            return result;

        var step = up ? -1 : 1;
        for (var next = index + step; next >= 0 && next < result.Count; next += step)
        {
            // 같은 쪽의 바로 인접 버튼을 찾아서만 위치를 바꿉니다.
            if (NormalizePosition(result[next].Position) == NormalizePosition(result[index].Position))
            {
                (result[index], result[next]) = (result[next], result[index]);
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// 지정한 버튼을 반대쪽 표시 순서의 끝으로 옮긴 새 목록을 반환합니다.
    /// 리스트 끝에 붙이면 그쪽 필터링 기준에서도 가장 뒤가 됩니다.
    /// </summary>
    public static List<HeaderButtonConfig> MoveToOtherSideEnd(List<HeaderButtonConfig> buttons, string id)
    {
        var result = new List<HeaderButtonConfig>(buttons);
        var index = result.FindIndex(button => button.Id == id);
        if (index < 0)
            return result;

        var item = result[index];
        result.RemoveAt(index);
        item.Position = NormalizePosition(item.Position) == "Left" ? "Right" : "Left";
        result.Add(item);
        return result;
    }

    // ── 편집기 대상 해석 (추가/편집 분리) ───────────────────────────────────

    /// <summary>
    /// 상단바 단축키 편집기의 편집 대상을 결정합니다.
    /// - createNew=true: 항상 새 버튼으로 시작하며 기존 항목을 참조하지 않습니다.
    /// - createNew=false + ID 지정: 그 커스텀 버튼을 편집합니다. 없으면 error를 반환합니다.
    /// - createNew=false + ID 없음: 실수로 기존 버튼을 덮어쓰는 것을 막기 위해 추가로 처리합니다.
    /// </summary>
    public static (HeaderButtonConfig? target, bool isCreate, string? error) ResolveEditTarget(
        IEnumerable<HeaderButtonConfig> buttons,
        string? requestedId,
        bool createNew)
    {
        if (createNew)
        {
            return (CreateCustomDefault(), true, null);
        }

        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            var target = buttons.FirstOrDefault(button =>
                button.Kind == HeaderButtonKind.Custom && button.Id == requestedId);
            if (target is null)
            {
                return (null, false, $"상단바 단축키를 찾을 수 없습니다. (ID: {requestedId})");
            }

            return (target, false, null);
        }

        // ID 없이 편집 모드로 들어온 경우: 기존 항목을 고르지 않고 추가 대상으로 처리합니다.
        return (CreateCustomDefault(), true, null);
    }
}
