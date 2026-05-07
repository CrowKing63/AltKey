using System.Windows;
using System.Windows.Media;
using static AltKey.Platform.Win32;

namespace AltKey.Services;

/// <summary>
/// [역할] 키보드 창의 특수한 윈도우 스타일(포커스 방지, 항상 위 등)을 제어하는 서비스입니다.
/// [기능] 가상 키보드가 다른 프로그램의 포커스를 뺏지 않게 하거나, 화면 가장 위에 떠 있게 만드는 등 시스템 수준의 창 설정을 담당합니다.
/// </summary>
public class WindowService
{
    /// <summary>
    /// T-1.3: WS_EX_NOACTIVATE 적용 — 창이 포커스를 뺏지 않도록 설정.
    /// </summary>
    public void ApplyNoActivate(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            exStyle | WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// T-1.4: 배경을 반투명 단색으로 설정 (Acrylic은 추후 구현).
    /// </summary>
    public void ApplyBackground(Window window)
    {
        // 접근성/미관: 창 자체는 투명으로 두고, 실제 카드 배경은 XAML Border가 그린다.
        // 이렇게 해야 메인 창 바깥 직각 레이어가 보이지 않고 투명도 기능도 그대로 유지된다.
        window.Background = System.Windows.Media.Brushes.Transparent;
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
