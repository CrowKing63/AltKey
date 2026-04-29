namespace AltKey.Models;

/// <summary>
/// [역할] 키보드 창의 위치와 크기 비율 등 화면 표시와 관련된 설정을 담는 클래스입니다.
/// </summary>
public class WindowConfig
{
    // 창의 가로 위치 (픽셀). -1이면 화면 중앙 하단에 자동으로 배치됩니다.
    public double Left   { get; set; } = -1;
    
    // 창의 세로 위치 (픽셀). -1이면 화면 중앙 하단에 자동으로 배치됩니다.
    public double Top    { get; set; } = -1;

    // 창의 크기 배율 (퍼센트). 100이 기본이며, 60~200 사이의 숫자로 조절 가능합니다.
    public int Scale { get; set; } = 100;
}

/// <summary>
/// [역할] AltKey 애플리케이션의 전반적인 환경 설정을 관리하는 클래스입니다.
/// [참고] 투명도, 사운드, 자동 완성 등 주요 기능의 ON/OFF 및 세부 수치를 여기서 정의합니다.
/// </summary>
public class AppConfig
{
    // 앱의 현재 버전 정보입니다.
    public string Version           { get; set; } = "1.0.0";
    
    // 앱 시작 시 기본으로 불러올 레이아웃 파일의 이름입니다.
    public string DefaultLayout     { get; set; } = "Bagic";
    
    // 키보드 창을 항상 다른 창들보다 위에 띄울지 여부입니다. (true: 항상 위)
    public bool   AlwaysOnTop       { get; set; } = true;
    
    // 키보드를 사용하지 않을 때의 투명도 (0.0: 투명 ~ 1.0: 불투명).
    public double OpacityIdle       { get; set; } = 0.4;
    
    // 키보드 위에 마우스를 올리거나 사용할 때의 투명도 (1.0 권장).
    public double OpacityActive     { get; set; } = 1.0;
    
    // 키보드 사용을 멈춘 후 '사용하지 않는 상태(Idle)'로 전환될 때까지의 대기 시간 (단위: 밀리초, 1000 = 1초).
    public int    FadeDelayMs       { get; set; } = 5000;
    
    // [접근성] 키 위에 마우스를 가만히 올리고 있으면 클릭되는 기능 활성화 여부입니다.
    public bool   DwellEnabled      { get; set; } = false;
    
    // [접근성] Dwell 기능 사용 시, 클릭으로 인식될 때까지 머물러야 하는 시간 (단위: 밀리초).
    public int    DwellTimeMs       { get; set; } = 800;
    
    // [접근성] Shift, Ctrl 등의 키를 한 번만 눌러도 고정되게 하는 기능 활성화 여부입니다.
    public bool   StickyKeysEnabled { get; set; } = true;
    
    // 앱의 테마 설정 ("light", "dark", "system" 중 선택).
    public string Theme             { get; set; } = "system";
    
    // 키보드를 띄우거나 숨길 때 사용하는 전체 시스템 단축키입니다.
    public string GlobalHotkey      { get; set; } = "Ctrl+Alt+K";
    
    // 실행 중인 프로그램에 따라 키보드 레이아웃을 자동으로 바꿀지 여부입니다.
    public bool   AutoProfileSwitch { get; set; } = true;
    
    // 프로그램별 전용 레이아웃 설정 데이터입니다.
    public Dictionary<string, string> Profiles { get; set; } = [];
    
    // 창 위치 및 크기 설정 객체입니다.
    public WindowConfig Window      { get; set; } = new();

    // 윈도우 시작 시 자동으로 AltKey를 실행할지 여부입니다.
    public bool RunOnStartup        { get; set; } = false;

    // 키를 누를 때 소리를 낼지 여부입니다.
    public bool SoundEnabled        { get; set; } = true;
    
    // 기본 소리 외에 사용하고 싶은 효과음 파일(.wav)의 경로입니다. 비어있으면 기본음이 나옵니다.
    public string? SoundFilePath    { get; set; } = null;

    // 이전에 복사했던 텍스트 목록(클립보드 히스토리) 창을 보여줄지 여부입니다.
    public bool ClipboardPanelEnabled { get; set; } = false;

    // 단어 입력 시 다음 단어를 추천해주는 자동 완성 기능 활성화 여부입니다.
    public bool AutoCompleteEnabled   { get; set; } = false;

    // [접근성] 키를 꾹 누르고 있을 때 연속으로 입력되게 할지 여부입니다.
    public bool KeyRepeatEnabled      { get; set; } = false;
    
    // 연속 입력이 시작되기 전까지 기다리는 시간 (단위: 밀리초).
    public int  KeyRepeatDelayMs      { get; set; } = 300;
    
    // 연속 입력이 진행되는 간격 (단위: 밀리초). 작을수록 입력 속도가 빨라집니다.
    public int  KeyRepeatIntervalMs   { get; set; } = 50;

    // [접근성] 키보드 버튼 안에 적힌 글자(라벨)의 크기 비율 (단위: 퍼센트). 80~220 사이 권장.
    public int  KeyFontScalePercent   { get; set; } = 100;

    // [접근성] 방향키나 탭 키를 이용해 키보드 버튼 사이를 이동하며 조작할 수 있는 기능입니다.
    public bool KeyboardA11yNavigationEnabled { get; set; } = false;
    
    // [접근성] 탭 탐색 시 순회할 컨트롤 범위입니다. 기본값은 키보드 본체 키만 탐색합니다.
    public KeyboardA11yNavigationScope KeyboardA11yNavigationScope { get; set; } = KeyboardA11yNavigationScope.KeysOnly;
    
    // [접근성] 탭 탐색 모드에서 빠져나올 때 사용할 키입니다. 기본값은 Esc(VK_ESCAPE)입니다.
    public string KeyboardA11yExitKey { get; set; } = "VK_ESCAPE";
    
    // [접근성] 탭 탐색으로 포커스가 이동할 때 현재 위치를 LiveRegion으로 공지할지 여부입니다.
    public bool KeyboardA11yAnnounceFocus { get; set; } = false;

    // ── L2/L3 접근성 설정 ────────────────────────────────────────────────

    // [접근성][L2] 키를 누를 때 키 라벨을 음성으로 읽어주는 TTS 기능 활성화 여부입니다.
    public bool TtsEnabled { get; set; } = false;

    // [접근성][L2] 키 위에 마우스만 올려도 TTS로 라벨을 읽어줄지 여부입니다. (TtsEnabled가 켜져 있을 때 동작)
    public bool TtsOnHover { get; set; } = false;

    // [접근성][L2] TTS 음성 속도 조절 값입니다. -5(느림) ~ 5(빠름), 0이 기본 속도입니다.
    public int TtsRate { get; set; } = 0;

    // [접근성][L2] 키 누름/창 이동 등의 애니메이션을 최소화하는 모드 활성화 여부입니다.
    public bool ReducedMotionEnabled { get; set; } = false;

    // [접근성][L3] 스위치 접근용 자동 스캔 입력 모드 활성화 여부입니다.
    public bool SwitchScanEnabled { get; set; } = false;

    // [접근성][L3] 스위치 스캔 시 다음 키로 이동하는 간격(밀리초)입니다. 작을수록 빠르게 스캔됩니다.
    public int SwitchScanIntervalMs { get; set; } = 800;

    // [접근성][L3] true면 2스위치 모드(다음/선택 분리), false면 1스위치 모드(Enter/Space로 선택)입니다.
    public bool SwitchScanTwoSwitch { get; set; } = false;

    // [접근성][L3] 스위치 스캔 방식입니다. Linear(순차), RowColumn(행→키), Manual(수동) 중 하나를 사용합니다.
    public SwitchScanMode SwitchScanMode { get; set; } = SwitchScanMode.Linear;

    // [접근성][L3] 스캔 시작 후 첫 이동 전 대기 시간(밀리초)입니다.
    public int SwitchScanInitialDelayMs { get; set; } = 800;

    // [접근성][L3] 선택 직후 다음 스캔 재개 전 잠시 멈추는 시간(밀리초)입니다.
    public int SwitchScanSelectPauseMs { get; set; } = 500;

    // [접근성][L3] 스캔이 전체 대상을 몇 바퀴 돈 뒤 멈출지 설정합니다. 0이면 무제한입니다.
    public int SwitchScanCyclesBeforePause { get; set; } = 0;

    // [접근성][L3] 마지막 대상 다음에 다시 처음으로 돌아갈지 여부입니다.
    public bool SwitchScanWrapEnabled { get; set; } = true;

    // [접근성][L3] 외부 스위치 장치가 "다음" 동작에서 실제로 보내는 키 이름입니다. (예: VK_TAB)
    public string SwitchScanNextKey { get; set; } = "VK_TAB";

    // [접근성][L3] 외부 스위치 장치가 "선택" 동작에서 실제로 보내는 키 이름입니다. (예: VK_RETURN)
    public string SwitchScanSelectKey { get; set; } = "VK_RETURN";

    // [접근성][L3] 외부 스위치 장치가 "보조 선택" 동작에서 실제로 보내는 키 이름입니다. (예: VK_SPACE)
    public string SwitchScanSecondarySelectKey { get; set; } = "VK_SPACE";

    // [접근성][L3] 외부 스위치 장치가 "이전" 동작에서 실제로 보내는 키 이름입니다. 비우면 사용하지 않습니다.
    public string SwitchScanPreviousKey { get; set; } = "";

    // [접근성][L3] 외부 스위치 장치가 "일시정지/재개" 동작에서 실제로 보내는 키 이름입니다. 비우면 사용하지 않습니다.
    public string SwitchScanPauseKey { get; set; } = "";

    // [접근성][L3] 스캔 대상에 자동완성 제안/현재 단어 슬롯을 포함할지 여부입니다.
    public bool SwitchScanIncludeSuggestions { get; set; } = true;

    // [접근성][L3] 제안 바 스캔 순서 우선순위입니다. BeforeKeyboard면 제안을 먼저 훑습니다.
    public SwitchScanSuggestionPriority SwitchScanSuggestionPriority { get; set; } = SwitchScanSuggestionPriority.BeforeKeyboard;

    // [접근성][L3] 스위치 스캔 공지 정책입니다. 기본값은 선택 시점만 공지입니다.
    public SwitchScanAnnounceMode SwitchScanAnnounceMode { get; set; } = SwitchScanAnnounceMode.SelectionOnly;
}
