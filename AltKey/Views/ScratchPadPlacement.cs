using System.Windows;

namespace AltKey.Views;

/// <summary>
/// [역할] 스크래치패드 창이 열릴 때(또는 키보드 창이 이동할 때) 떠야 할 좌표를 계산하는 순수 함수입니다.
/// [기능] 키보드 창의 상/하 반만 기준으로 패드를 그 반대편에 수평 중앙으로 배치하고, 작업 영역 밖으로는 못 나가게 클램프합니다.
/// [참고] 위치는 매번 열 때마다 새로 계산하며 저장하지 않습니다(예측 가능성 — 같은 키보드 위치면 항상 같은 자리에 뜸).
/// </summary>
internal static class ScratchPadPlacement
{
    // 패드와 키보드 창 사이 간격(px) — 너무 넓으면 두 창이 떨어져 보입니다.
    internal const double Gap = 4.0;

    /// <summary>
    /// 키보드 창 직사각형과 작업 영역을 기준으로, 스크래치패드의 (Left, Top)를 계산합니다.
    /// - 키보드가 화면 상반부에 있으면 패드는 아래로, 하반부에 있으면 위로 배치됩니다.
    /// - 가로 위치는 키보드 중앙 기준입니다(패드보다 좁은 키보드도 같은 규칙).
    /// - 결과가 작업 영역을 벗어나면 가장 가까운 경계로 붙입니다.
    /// </summary>
    internal static (double left, double top) PlacePad(
        Rect keyboardRect,
        Rect workArea,
        double padWidth,
        double padHeight)
    {
        // 화면 수평 중심선 위에 키보드 상단이 있으면 "상반부"로 봅니다.
        bool below = keyboardRect.Top < workArea.Top + workArea.Height / 2;

        double left = keyboardRect.Left + (keyboardRect.Width - padWidth) / 2;
        double top = below
            ? keyboardRect.Bottom + Gap
            : keyboardRect.Top - padHeight - Gap;

        // 패드가 작업 영역보다 크면(극소 작업 영역) 경계값 자체가 뒤집히므로 Max로 방어합니다.
        left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - padWidth));
        top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - padHeight));

        return (left, top);
    }
}
