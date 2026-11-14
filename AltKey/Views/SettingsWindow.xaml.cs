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

        // 로컬 AI 파일은 사용자가 수동으로 배치할 수도 있어, 창이 활성화될 때마다
        // 설치 상태를 디스크에서 다시 확인해 다운로드 버튼 활성/비활성을 갱신한다.
        Activated += (_, _) => _vm.UpdateLocalAiReady();

        // 포커스 추적기에 현재 창을 등록해 보조 기능 모듈이 활성 창을 알 수 있게 한다.
        FocusTracker.Register(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// AI 백엔드 라디오 버튼 클릭 시 IsLocalAi를 명시적으로 설정합니다.
    /// 바인딩이 OneWay라 라디오 버튼 상태가 소스로 write-back되는 경로(UpdateSource)가 끊어져,
    /// 예전마다 주기가 겹겹이 쌓여 WinForms 트레이 메뉴 재빌드·네이티브 텍스트 측정으로
    /// 스택 오버플로까지 이어지던 피드백 루프가 더 이상 만들어지지 않습니다.
    /// 창 생성 시 초기 Checked 이벤트도 같은 값으로 설정하므로 속성 변경이 발생하지 않아 안전합니다.
    /// </summary>
    private void OnApiBackendRadioChecked(object sender, RoutedEventArgs e)
        => _vm.IsLocalAi = false;

    private void OnLocalBackendRadioChecked(object sender, RoutedEventArgs e)
        => _vm.IsLocalAi = true;

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
        // [상단바] 대표 컨트롤을 지정하지 않는다 — "새 커스텀 단축키 추가" 버튼은 본문 중간(좌·우 목록 아래)에 있어
        // 포커스가 그쪽으로 가면서 ScrollViewer가 자동으로 중간까지 스크롤되는 문제를 막기 위해,
        // 폴백 탐색이 본문 맨 위(첫 행의 체크박스)로 포커스를 보내도록 한다.
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
