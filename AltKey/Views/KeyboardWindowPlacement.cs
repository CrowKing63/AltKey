using System.Windows;

namespace AltKey.Views;

/// <summary>
/// [역할] 키보드 창의 세로 기준점(상단/하단)과 높이 변경 후 새 Top 좌표를 계산합니다.
/// [기능] 접기/펼치기, 추천 바 표시 변경처럼 창 높이가 달라질 때 사용자가 마지막에 맞춰 둔 화면 가장자리 기준을 유지하도록 돕습니다.
/// </summary>
internal static class KeyboardWindowPlacement
{
    // 화면 끝에 거의 붙었을 때만 "도킹" 상태로 간주합니다.
    // 이 값을 너무 크게 잡으면 화면 중간에 둔 창도 하단/상단 고정처럼 움직여 어색해집니다.
    internal const double DockTolerance = 24.0;

    internal enum VerticalAnchor
    {
        Top,
        Freeform,
        Bottom
    }

    /// <summary>
    /// 현재 창이 화면 상단에 더 가까운지, 하단에 더 가까운지 계산합니다.
    /// </summary>
    internal static VerticalAnchor DetectVerticalAnchor(double top, double height, Rect workArea)
    {
        double topGap = top - workArea.Top;
        double bottomGap = workArea.Bottom - (top + height);

        if (bottomGap <= DockTolerance)
            return VerticalAnchor.Bottom;

        if (topGap <= DockTolerance)
            return VerticalAnchor.Top;

        return VerticalAnchor.Freeform;
    }

    /// <summary>
    /// 높이 변경 전 창 위치와 작업 영역을 기준으로, 같은 가장자리 간격을 유지하는 새 Top 좌표를 계산합니다.
    /// </summary>
    internal static double ComputeAnchoredTop(
        double currentTop,
        double currentHeight,
        double newHeight,
        Rect workArea,
        VerticalAnchor anchor,
        double? anchorGapOverride = null)
    {
        double topGap = currentTop - workArea.Top;
        double bottomGap = workArea.Bottom - (currentTop + currentHeight);

        double nextTop = anchor switch
        {
            VerticalAnchor.Bottom => workArea.Bottom - (anchorGapOverride ?? bottomGap) - newHeight,
            VerticalAnchor.Top => workArea.Top + (anchorGapOverride ?? topGap),
            _ => currentTop
        };

        double maxTop = Math.Max(workArea.Top, workArea.Bottom - newHeight);
        return Math.Clamp(nextTop, workArea.Top, maxTop);
    }
}
