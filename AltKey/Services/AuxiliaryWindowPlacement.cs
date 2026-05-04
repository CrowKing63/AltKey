using System.Windows;

namespace AltKey.Services;

/// <summary>
/// 설정·편집기 등 보조 창을 띄울 때 위치만 잡아 줍니다.
/// <see cref="Window.Owner"/>를 메인(항상 위) 창에 두면 소유 창이 항상 소유자 위에 붙어
/// 다른 프로그램이나 가상 키보드와 겹쳐 입력하기 어려워지므로, Owner 없이 수동 배치합니다.
/// </summary>
public static class AuxiliaryWindowPlacement
{
    /// <summary>
    /// <paramref name="reference"/>가 보이는 일반 상태면 그 창을 기준으로 가운데에 맞추고,
    /// 숨김·최소화 등이면 화면 중앙에 둡니다.
    /// </summary>
    public static void CenterNear(Window window, Window? reference)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        if (reference is null
            || reference.WindowState == WindowState.Minimized
            || !reference.IsVisible)
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        double refW = reference.IsLoaded && reference.ActualWidth > 0
            ? reference.ActualWidth
            : reference.Width;
        double refH = reference.IsLoaded && reference.ActualHeight > 0
            ? reference.ActualHeight
            : reference.Height;

        if (double.IsNaN(refW) || refW <= 0) refW = 900;
        if (double.IsNaN(refH) || refH <= 0) refH = 350;

        double w = window.Width;
        double h = window.Height;
        if (double.IsNaN(w) || w <= 0) w = window.MinWidth > 0 ? window.MinWidth : 600;
        if (double.IsNaN(h) || h <= 0) h = window.MinHeight > 0 ? window.MinHeight : 400;

        double left = reference.Left + (refW - w) / 2;
        double top = reference.Top + (refH - h) / 2;

        var wa = SystemParameters.WorkArea;
        left = Math.Clamp(left, wa.Left, Math.Max(wa.Left, wa.Right - w));
        top = Math.Clamp(top, wa.Top, Math.Max(wa.Top, wa.Bottom - h));

        window.Left = left;
        window.Top = top;
    }
}
