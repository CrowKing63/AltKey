namespace AltKey.Models;

public class WindowConfig
{
    public int Left { get; set; } = 100;
    public int Top { get; set; } = 100;
    public int Width { get; set; } = 900;
    public int Height { get; set; } = 320;
}

public class AppConfig
{
    public WindowConfig Window { get; set; } = new();
    public double OpacityIdle { get; set; } = 0.4;
    public int FadeDelaySeconds { get; set; } = 5;
    public bool IsTopmost { get; set; } = true;
}
