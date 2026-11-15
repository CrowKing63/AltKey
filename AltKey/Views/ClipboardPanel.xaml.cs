using System.Windows.Controls;

namespace AltKey.Views;

/// <summary>
/// [역할] 최근에 복사했던 텍스트 목록(클립보드 히스토리)을 보여주고 다시 사용할 수 있게 돕는 패널입니다.
/// </summary>
public partial class ClipboardPanel : System.Windows.Controls.UserControl
{
    public ClipboardPanel()
    {
        InitializeComponent();

        // 수정: 닫았다 다시 열면 ScrollViewer 오프셋이 마지막 위치에서 남아 "최근" 탭이
        // 아래에서 시작하는 문제를 막기 위해, 보이기 시작할 때마다 맨 위(최근 항목)로 되돌립니다.
        IsVisibleChanged += (_, e) =>
        {
            if (!IsVisible) return;

            ItemListScroller.ScrollToVerticalOffset(0);

            // 레이아웃이 끝난 뒤에도 다시 반영해, 높이 재계산으로 오프셋이 복원되는 것을 막습니다.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                ItemListScroller.ScrollToVerticalOffset(0)));
        };
    }
}
