using System.Windows;

namespace AltKey.Services;

/// <summary>
/// 설정/편집기 같은 보조 창의 시작 위치를 접근성 관점에서 일관되게 배치하기 위한 유틸리티입니다.
/// </summary>
public static class AuxiliaryWindowPlacement
{
    /// <summary>
    /// 설정 창처럼 입력 창과 겹치면 불편한 보조 창을 화면 정중앙에 띄웁니다.
    /// </summary>
    public static void CenterOnScreen(Window window)
    {
        // 작업 영역 기준 중앙 배치를 사용해 창 시작 위치를 예측 가능하게 고정합니다.
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    /// <summary>
    /// 기준 창이 보이면 그 창 중심 근처에 배치하고, 기준 창이 없거나 최소화 상태면 화면 중앙에 띄웁니다.
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
