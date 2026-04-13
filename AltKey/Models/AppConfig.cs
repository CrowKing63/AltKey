namespace AltKey.Models;

public class WindowConfig
{
    public double Left   { get; set; } = 100;
    public double Top    { get; set; } = 700;
    public double Width  { get; set; } = 900;
    public double Height { get; set; } = 320;
}

public class AppConfig
{
    public string Version           { get; set; } = "1.0.0";
    public string Language          { get; set; } = "ko";
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
}
