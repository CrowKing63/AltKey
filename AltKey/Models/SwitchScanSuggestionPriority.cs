namespace AltKey.Models;

/// <summary>
/// [접근성][L3] 제안 바를 스캔 순서의 앞/뒤 어디에 배치할지 지정합니다.
/// </summary>
public enum SwitchScanSuggestionPriority
{
    // 제안 바를 먼저 훑은 뒤 키보드를 훑습니다.
    BeforeKeyboard,
    // 키보드를 먼저 훑은 뒤 제안 바를 훑습니다.
    AfterKeyboard
}
