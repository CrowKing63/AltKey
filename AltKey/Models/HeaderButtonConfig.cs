namespace AltKey.Models;

/// <summary>
/// [역할] 상단바(헤더)에 표시되는 구성 가능한 버튼 하나의 설정을 담는 클래스입니다.
/// [참고] 접기/최소화/닫기 버튼은 이 설정과 무관하게 항상 우측 끝에 고정됩니다.
/// </summary>
public class HeaderButtonConfig
{
    /// 버튼 식별자. 아래 상수 중 하나를 사용합니다.
    /// Clipboard, Emoji, AutoComplete, OsIme, Osk, Settings, Ai
    public string Id { get; set; } = "";

    /// 상단바에 이 버튼을 표시할지 여부입니다.
    public bool Visible { get; set; } = true;

    /// 버튼의 좌/우 배치 위치입니다. "Left" 또는 "Right".
    public string Position { get; set; } = "Right";

    // ── 사전 정의된 버튼 ID 상수 ──────────────────────────────────────────
    public const string IdClipboard    = "Clipboard";
    public const string IdEmoji        = "Emoji";
    public const string IdAutoComplete = "AutoComplete";
    public const string IdOsIme        = "OsIme";
    public const string IdOsk          = "Osk";
    public const string IdSettings     = "Settings";
    public const string IdAi           = "Ai";

    /// <summary>
    /// 기본 버튼 구성 목록을 생성합니다. HeaderButtons가 비어 있을 때(최초 실행 또는 마이그레이션)에 사용됩니다.
    /// 리스트 순서가 곧 상단바 표시 순서입니다.
    /// </summary>
    public static List<HeaderButtonConfig> CreateDefaults() =>
    [
        new() { Id = IdClipboard,    Visible = true,  Position = "Right" },
        new() { Id = IdEmoji,        Visible = true,  Position = "Right" },
        new() { Id = IdAutoComplete, Visible = true,  Position = "Right" },
        new() { Id = IdOsIme,        Visible = true,  Position = "Right" },
        new() { Id = IdOsk,          Visible = true,  Position = "Right" },
        new() { Id = IdSettings,     Visible = true,  Position = "Right" },
        new() { Id = IdAi,           Visible = false, Position = "Right" },
    ];
}
