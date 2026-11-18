using System.Windows;

namespace AltKey.Services;

/// <summary>
/// [역할] 키보드 창에 앵커되는 패널(빠른 메모·클립보드 등)이 떠야 할 좌표를 계산하는 순수 함수입니다. WPF 상태가 없습니다.
/// [기능] 단일 패널: 키보드 상/하 반 기준 반대편에 수평 중앙 배치 (기존 스크래치패드 규칙).
///        복수 패널: 한 줄로 나란히, 총 너비 기준으로 키보드 중심선에 정렬 (순서 고정).
/// [참고] 위치는 매 열림·재계산마다 새로 계산하며 저장하지 않습니다 — 같은 키보드 위치면 항상 같은 자리에 뜹니다.
/// </summary>
internal static class AnchoredPanelsLayout
{
    // 패널과 키보드 창 사이 간격(px) — 너무 넓으면 두 창이 떨어져 보입니다.
    internal const double Gap = 4.0;

    /// <summary>배치 대상 패널의 식별자·표시 순서(왼쪽→오른쪽)·크기.</summary>
    public readonly record struct PanelSpec(string Id, int Order, double Width, double Height);

    /// <summary>
    /// 키보드 창 직사각형과 작업 영역을 기준으로 각 패널의 (left, top)를 계산합니다.
    /// - 키보드가 화면 상반부에 있으면 패널은 아래로, 하반부면 위로 배치됩니다.
    ///   (경계: 키보드 상단이 화면 중심선 위에 정확히 있으면 "상반부"로 봅니다.)
    /// - 복수 패널은 Order 순서로 한 줄에 나란히 두고, 총 너비(간격 포함) 기준으로 키보드 중심선에 정렬합니다.
    ///   아래 배치면 모든 패널의 상단을 같은 선에, 위 배치면 하단을 같은 선에 맞춥니다 (키보드에 닿는 가장자리가 항상 공통).
    /// - 총 너비가 작업 영역을 넘으면 폴백: 1번째 패널은 rowSide, 나머지 패널은 반대편에서 개별 수평 중앙 배치 (기존 규칙).
    /// - 결과가 작업 영역을 벗어나면 가장 가까운 경계로 붙입니다.
    /// </summary>
    internal static IReadOnlyDictionary<string, (double left, double top)> PlacePanels(
        Rect keyboardRect,
        Rect workArea,
        IEnumerable<PanelSpec> panels)
    {
        var list = panels.OrderBy(p => p.Order).ToList();
        if (list.Count == 0) return new Dictionary<string, (double, double)>();

        // 화면 수평 중심선 위에 키보드 상단이 있으면 "상반부"로 봅니다.
        bool below = keyboardRect.Top < workArea.Top + workArea.Height / 2;

        var result = new Dictionary<string, (double left, double top)>(list.Count);

        // 단일 패널: 기존 규칙 그대로 — 키보드 기준 수평 중앙.
        if (list.Count == 1)
        {
            var p = list[0];
            double left = keyboardRect.Left + (keyboardRect.Width - p.Width) / 2;
            double top = below ? keyboardRect.Bottom + Gap : keyboardRect.Top - p.Height - Gap;
            result[p.Id] = ClampPosition(left, top, p.Width, p.Height, workArea);
            return result;
        }

        // 복수 패널: 총 너비(간격 포함)가 작업 영역에 맞으면 한 줄 나란히. 키보드 중심선 기준 중앙 정렬.
        double totalWidth = list.Sum(p => p.Width) + Gap * (list.Count - 1);
        if (totalWidth <= workArea.Width)
        {
            double left0 = Math.Clamp(
                keyboardRect.Left + (keyboardRect.Width - totalWidth) / 2,
                workArea.Left, Math.Max(workArea.Left, workArea.Right - totalWidth));

            // 위 배치 시에는 가장 큰 패널 높이를 기준으로 줄의 상단선을 정한 뒤 각 패널을 하단 정렬합니다.
            double tallest = list.Max(p => p.Height);
            double rowTop = below ? keyboardRect.Bottom + Gap : keyboardRect.Top - tallest - Gap;

            double cursorX = left0;
            foreach (var p in list)
            {
                result[p.Id] = (cursorX, below ? rowTop : rowTop + tallest - p.Height);
                cursorX += p.Width + Gap;
            }
            return result;
        }

        // 폴백: 총 너비가 작업 영역보다 넓으면 — 1번째 패널은 rowSide, 나머지 패널은 반대편 개별 중앙 배치.
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            bool useRowSide = i == 0;
            double left = keyboardRect.Left + (keyboardRect.Width - p.Width) / 2;
            // rowSide는 "키보드 반대편": below면 아래, 위면 위로. 나머지 패널은 그 반대.
            bool placeBelow = (below && useRowSide) || (!below && !useRowSide);
            double top = placeBelow ? keyboardRect.Bottom + Gap : keyboardRect.Top - p.Height - Gap;
            result[p.Id] = ClampPosition(left, top, p.Width, p.Height, workArea);
        }

        return result;
    }

    /// <summary>작업 영역 경계로 붙입니다. 패널이 작업 영역보다 크면(극소 작업 영역) 경계값 자체가 뒤집히므로 Max로 방어합니다.</summary>
    private static (double left, double top) ClampPosition(double left, double top, double width, double height, Rect workArea)
    {
        left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        return (left, top);
    }
}
