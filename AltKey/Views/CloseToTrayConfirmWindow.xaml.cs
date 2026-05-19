using System.Windows;

namespace AltKey.Views;

/// <summary>
/// [역할] 키보드 창을 트레이로 숨기기 전에 한 번 더 확인하는 접근성 보조 창입니다.
/// [기능] 사용자가 숨김 또는 취소를 명확히 고르고, 필요하면 다음부터 묻지 않도록 기억합니다.
/// </summary>
public partial class CloseToTrayConfirmWindow : Window
{
    public CloseToTrayConfirmWindow()
    {
        InitializeComponent();

        // 접근성: 창이 열리면 가장 안전한 기본 동작인 취소 버튼에 바로 포커스를 둡니다.
        Loaded += (_, _) => CancelButtonElement.Focus();
    }

    /// <summary>
    /// 사용자가 "앞으로 다시 묻지 않기"를 체크했는지 여부입니다.
    /// 숨기기를 실제로 확정한 경우에만 호출 쪽에서 저장합니다.
    /// </summary>
    public bool DontAskAgain => DontAskAgainCheckBox.IsChecked == true;

    private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
