namespace AltKey.Models;

public class WindowConfig
{
    public double Left   { get; set; } = -1;   // -1 = 미설정, RestoreWindowPosition에서 화면 중앙 하단으로 계산
    public double Top    { get; set; } = -1;
    public double Width  { get; set; } = 900;
    public double Height { get; set; } = 320;
}

public class AppConfig
{
    public string Version           { get; set; } = "1.0.0";
    public string DefaultLayout     { get; set; } = "qwerty-ko";
    public bool   AlwaysOnTop       { get; set; } = true;
    public double OpacityIdle       { get; set; } = 0.4;
    public double OpacityActive     { get; set; } = 1.0;
    public int    FadeDelayMs       { get; set; } = 5000;
    public bool   DwellEnabled      { get; set; } = false;
    public int    DwellTimeMs       { get; set; } = 800;
    public bool   StickyKeysEnabled { get; set; } = true;
    public string Theme             { get; set; } = "system";
    public string GlobalHotkey      { get; set; } = "Ctrl+Alt+K";
    public bool   AutoProfileSwitch { get; set; } = true;
    public Dictionary<string, string> Profiles { get; set; } = [];
    public WindowConfig Window      { get; set; } = new();

    // T-8.1: 윈도우 시작 시 자동 실행
    public bool RunOnStartup        { get; set; } = false;

    // T-8.2: 키 클릭 사운드 (기본 ON — Assets/Sounds/ 폴더의 WAV 파일 자동 사용)
    public bool SoundEnabled        { get; set; } = true;
    public string? SoundFilePath    { get; set; } = null;

    // T-8.4: 클립보드 히스토리 패널
    public bool ClipboardPanelEnabled { get; set; } = false;

    // T-9.3: 자동 완성 (영문/한글 공용, 레이아웃에 따라 자동 선택)
    public bool AutoCompleteEnabled   { get; set; } = false;

    // T-10: 키 반복 입력 (접근성 - 물리적 키보드처럼 홀드 시 반복)
    public bool KeyRepeatEnabled      { get; set; } = true;
    public int  KeyRepeatDelayMs      { get; set; } = 300;
    public int  KeyRepeatIntervalMs   { get; set; } = 50;
}
