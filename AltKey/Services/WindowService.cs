using System.Windows;
using System.Windows.Media;
using static AltKey.Platform.Win32;

namespace AltKey.Services;

public class WindowService
{
    /// <summary>
    /// T-1.3: WS_EX_NOACTIVATE 적용 — 창이 포커스를 뺏지 않도록 설정.
    /// </summary>
    public void ApplyNoActivate(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            exStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// T-1.4: 배경을 반투명 단색으로 설정 (Acrylic은 추후 구현).
    /// </summary>
    public void ApplyBackground(Window window)
    {
        window.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 26, 26, 26));
    }

    /// <summary>
    /// T-1.8: 항상 위 토글 — WPF Topmost + SetWindowPos 보강.
    /// </summary>
    public void SetTopmost(Window window, bool topmost)
    {
        window.Topmost = topmost;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        SetWindowPos(hwnd,
            topmost ? HWND_TOPMOST : HWND_NOTOPMOST,
            0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
}
