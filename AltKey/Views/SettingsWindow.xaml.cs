using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AltKey.Services;
using AltKey.ViewModels;

using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AltKey.Views;

/// <summary>
/// 설정 창의 UI 수명주기와 접근성 보조 동작(탭 전환 시 포커스)을 관리한다.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // 접근성: 설정 창이 열릴 때 현재 탭의 첫 컨트롤로 포커스를 이동한다.
        Loaded += (_, _) => FocusFirstControlInSelectedTab();

        // 포커스 추적기에 현재 창을 등록해 보조 기능 모듈이 활성 창을 알 수 있게 한다.
        FocusTracker.Register(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); return; }
        base.OnKeyDown(e);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _vm.OnSettingsWindowClosed();
        base.OnClosed(e);
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    /// <summary>
    /// 탭이 바뀌면 해당 탭의 첫 입력 요소로 이동시켜 키보드 사용자 탐색 부담을 줄인다.
    /// </summary>
    private void SettingsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, SettingsTabControl))
        {
            return;
        }

        if (e.Source is not System.Windows.Controls.TabControl)
        {
            return;
        }

        FocusFirstControlInSelectedTab();
    }

    private void FocusFirstControlInSelectedTab()
    {
        if (SettingsTabControl?.SelectedItem is not TabItem tab)
        {
            return;
        }

        // 각 탭의 대표 첫 컨트롤 이름에 우선 포커싱하고, 실패하면 탭 본문에서 첫 포커스 가능한 요소를 탐색한다.
        FrameworkElement? primary = tab.Header?.ToString() switch
        {
            "외형" => AppearanceFirstFocusable,
            "동작" => BehaviorFirstFocusable,
            "보조기능" => A11yFirstFocusable,
            "고급" => AdvancedFirstFocusable,
            _ => null
        };

        if (primary is { IsVisible: true, Focusable: true } && primary.Focus())
        {
            return;
        }

        if (tab.Content is DependencyObject root && root is UIElement element)
        {
            var request = new TraversalRequest(FocusNavigationDirection.First);
            element.MoveFocus(request);
        }
    }
}
