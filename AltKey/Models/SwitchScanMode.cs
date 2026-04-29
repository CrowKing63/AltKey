namespace AltKey.Models;

/// <summary>
/// [접근성][L3] 스위치 스캔이 이동할 순서를 결정하는 모드입니다.
/// </summary>
public enum SwitchScanMode
{
    // 모든 대상을 순서대로 훑는 기본 모드입니다.
    Linear,
    // 먼저 행을 선택한 뒤 해당 행의 키를 선택하는 2단계 모드입니다.
    RowColumn,
    // 자동 타이머 없이 "다음/이전" 입력으로만 이동하는 모드입니다.
    Manual
}
