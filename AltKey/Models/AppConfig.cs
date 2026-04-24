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
}
