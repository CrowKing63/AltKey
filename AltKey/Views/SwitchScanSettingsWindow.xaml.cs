using System.Windows;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey.Views;

/// <summary>
/// [접근성] 스위치 스캔의 세부 옵션을 전담하는 설정 창입니다.
/// 본 설정 창의 복잡도를 낮추기 위해 상세 항목을 분리했습니다.
/// </summary>
public partial class SwitchScanSettingsWindow : Window
{
    public SwitchScanSettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        FocusTracker.Register(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
