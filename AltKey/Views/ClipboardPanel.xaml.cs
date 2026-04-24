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
    }
}
