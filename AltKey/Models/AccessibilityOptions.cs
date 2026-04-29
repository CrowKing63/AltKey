namespace AltKey.Models;

/// <summary>
/// [접근성] 탭 탐색 시 어떤 컨트롤까지 순회할지 정의합니다.
/// </summary>
public enum KeyboardA11yNavigationScope
{
    // 키보드 본체 키만 탐색합니다. (기본값)
    KeysOnly,
    // 키보드 키 + 자동완성 제안까지 탐색합니다.
    KeysAndSuggestions,
    // 헤더 버튼/설정/닫기 등 모든 컨트롤을 탐색 대상으로 확장합니다.
    AllControls
}

/// <summary>
/// [접근성] 스위치 스캔 공지 빈도를 제어합니다.
/// </summary>
public enum SwitchScanAnnounceMode
{
    // 스캔 이동/선택 모두 공지하지 않습니다.
    Off,
    // 선택 시점만 공지합니다. (기본값)
    SelectionOnly,
    // 스캔이 이동할 때마다 공지합니다.
    EveryMove
}

/// <summary>
/// [접근성] 현재 접근성 포커스(하이라이트) 소유자를 명시합니다.
/// </summary>
public enum A11yFocusOwner
{
    None,
    KeyboardNavigation,
    SwitchScan
}
