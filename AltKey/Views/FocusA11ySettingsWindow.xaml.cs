using System.Windows;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey.Views;

/// <summary>
/// [접근성] 포커스 가시화(탭 탐색) 세부 옵션을 설정하는 전용 창입니다.
/// </summary>
public partial class FocusA11ySettingsWindow : Window
{
    public FocusA11ySettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        FocusTracker.Register(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
