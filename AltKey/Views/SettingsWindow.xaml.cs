using System.Windows;
using AltKey.Services;
using AltKey.ViewModels;

using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AltKey.Views;

/// <summary>
/// [역할] AltKey의 환경 설정 창을 제어하는 클래스입니다.
/// [기능] 설정 값(투명도, 사운드, 테마 등)을 변경할 수 있는 UI를 제공하고 뷰모델(ViewModel)과 연결합니다.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        
        // 포커스 추적기에 현재 창을 등록합니다. (키 입력이 키보드 창으로 잘 가도록 돕는 기능)
        FocusTracker.Register(this);
    }

    /// <summary>
    /// 창 닫기 버튼을 눌렀을 때의 동작입니다.
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 키보드의 ESC 키를 눌렀을 때 창을 닫도록 처리합니다.
    /// </summary>
    protected override void OnKeyDown(WpfKeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) { Close(); return; }
        base.OnKeyDown(e);
    }

    /// <summary>
    /// 설정 창이 닫힐 때 뷰모델에 알림을 주어 필요한 뒷정리 작업을 수행합니다.
    /// </summary>
    protected override void OnClosed(System.EventArgs e)
    {
        _vm.OnSettingsWindowClosed();
        base.OnClosed(e);
    }
}