using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Automation.Peers;
using AltKey.Models;
using AltKey.Services;
using AltKey.ViewModels;
using WpfRect = System.Windows.Shapes.Rectangle;

namespace AltKey.Views;

public partial class KeyboardView : System.Windows.Controls.UserControl
{
    private bool _isCollapsed = false;

    public bool IsCollapsed => _isCollapsed;
    private bool _autoCompleteBarAdded = false;



    private ConfigService? _configService;
    private const double CollapsedWindowHeight = 28.0;

    public KeyboardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            _configService = App.Services.GetRequiredService<ConfigService>();
            _configService.ConfigChanged += OnConfigChanged;

            ApplySuggestionBarHeight();
            ApplyScale();

            window.SizeChanged += OnWindowSizeChanged;

            if (DataContext is MainViewModel mainVm)
            {
                mainVm.Keyboard.LiveRegionChanged += AnnounceLiveRegion;

                mainVm.Keyboard.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(KeyboardViewModel.MaxRowUnits)
                                       or nameof(KeyboardViewModel.MaxRowCount)
                                       or nameof(KeyboardViewModel.RowCount))
                    {
                        Dispatcher.InvokeAsync(() => ApplyScale());
                    }
                };
            }
        }
    }

    private void ApplySuggestionBarHeight()
    {
        if (DataContext is not MainViewModel vm) return;

        var wantBar = _configService?.Current.AutoCompleteEnabled == true;
        _autoCompleteBarAdded = wantBar;
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName is null
            or nameof(AppConfig.AutoCompleteEnabled)
            or "Window.Scale")
        {
            Dispatcher.InvokeAsync(() =>
            {
                ApplySuggestionBarHeight();
                ApplyScale();
            });
        }
    }

    // T-4.10: 창 크기 변경 시 KeyUnit 재계산
    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateKeyUnit(e.NewSize.Width);

    // 키보드 여백(키가 없는 영역) 클릭 시 이모지/클립보드 패널 닫기
    private void KeyboardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Emoji.IsVisible     = false;
            vm.Clipboard.IsVisible = false;
        }
    }

    // T-4.10: KeyboardBorder 크기 변경 시 재계산 (이모지/클립보드 패널 토글 포함)
    private void KeyboardBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateKeyUnit(Window.GetWindow(this)?.Width ?? ActualWidth);

    // 키 한 변의 최소/최대 픽셀 (너무 작아 터치 불가해지는 것 방지)
    private const double MinKeyUnit = 28.0;
    private const double MaxKeyUnit = 80.0;

    // 레이아웃 패딩/마진 상수
    private const double KbHorizontalPad = 12.0; // KeyboardBorder Padding 좌+우
    private const double KbVerticalPad   = 8.0;  // KeyboardBorder Padding 상+하
    private const double KeyMargin       = 4.0;  // 키 1개당 마진 총합 (Margin="2")

    private const double BaseKeyUnit  = 50.0;
    private const double HeaderHeight = 28.0;
    private const int    MinScale     = 60;
    private const int    MaxScale     = 200;

    // 엣지케이스 방어용 절대 하한 (공백 레이아웃 등)
    private const double AbsMinWindowWidth  = 400.0;
    private const double AbsMinWindowHeight = 180.0;

    // T-4.10: KeyUnit = min(가로 기준, 세로 기준) — Stretch=Uniform 동작 재현
    // 바가 KeyRowHeight(= KeyUnit + 4) 에 바인딩되어 KeyUnit과 상호 의존하므로,
    // 고정점을 한 번에 계산하도록 폐쇄형 식(rows+1 분모)을 사용한다.
    private void UpdateKeyUnit(double windowWidth)
    {
        if (DataContext is not MainViewModel vm) return;

        double units  = Math.Max(1, vm.Keyboard.MaxRowUnits);
        double wKeys  = Math.Max(1, vm.Keyboard.MaxRowCount);
        double rows   = Math.Max(1, vm.Keyboard.RowCount);

        // DockPanel이 실제로 바에 할당한 공간을 기준으로 삼는다
        // (AutoComplete 꺼짐 + 제안 없음 양쪽 모두 Collapsed가 되어 0 공간)
        bool barVisible = AutoCompleteBar?.Visibility == Visibility.Visible;
        double barH = barVisible ? AutoCompleteBar!.ActualHeight : 0;

        double availW = windowWidth - KbHorizontalPad;
        double totalBudget = KeyboardBorder.ActualHeight + barH;
        double availH = totalBudget - KbVerticalPad - (barVisible ? 4.0 : 0);

        if (availH < 1) return; // 레이아웃 완료 전 무시

        double rowsDiv = rows + (barVisible ? 1 : 0);
        double kW = (availW - wKeys * KeyMargin) / units;
        double kH = (availH - rows  * KeyMargin) / rowsDiv;

        vm.Keyboard.KeyUnit = Math.Max(MinKeyUnit, Math.Min(MaxKeyUnit, Math.Min(kW, kH)));
    }

    private (double Width, double Height) ComputeBaseSize()
    {
        if (DataContext is not MainViewModel vm)
            return (900.0, 320.0);

        double units  = Math.Max(1, vm.Keyboard.MaxRowUnits);
        double wKeys  = Math.Max(1, vm.Keyboard.MaxRowCount);
        double rows   = Math.Max(1, vm.Keyboard.RowCount);

        double baseW = units * BaseKeyUnit
                     + wKeys * KeyMargin
                     + KbHorizontalPad;

        double keyboardH = rows * BaseKeyUnit
                         + rows * KeyMargin
                         + KbVerticalPad;

        double barH = _autoCompleteBarAdded ? (BaseKeyUnit + 4.0) : 0;

        double baseH = HeaderHeight + barH + keyboardH;

        return (
            Math.Max(baseW, AbsMinWindowWidth),
            Math.Max(baseH, AbsMinWindowHeight)
        );
    }

    public void ApplyScale()
    {
        if (Window.GetWindow(this) is not { } window) return;

        var scale = _configService?.Current.Window.Scale ?? 100;
        scale = Math.Clamp(scale, MinScale, MaxScale);

        var (baseW, baseH) = ComputeBaseSize();

        window.Width  = baseW * scale / 100.0;
        window.Height = _isCollapsed
            ? CollapsedWindowHeight
            : baseH * scale / 100.0;
    }

    // T-1.5: 창 드래그 이동
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
            window.WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Hide();
    }

    // T-7.2: 접기/펼치기 버튼
    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window is null) return;

        if (!_isCollapsed)
        {
            AnimateWindowHeight(window, CollapsedWindowHeight);
            if (FindName("CollapseIcon") is TextBlock collapseIcon)
                collapseIcon.Text = "▼";
            _isCollapsed = true;
        }
        else
        {
            _isCollapsed = false;
            ApplyScale();
            if (FindName("CollapseIcon") is TextBlock collapseIcon)
                collapseIcon.Text = "▲";
        }
    }

    private static void CaptureAndClearHeightAnimation(Window window)
    {
        var current = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        window.BeginAnimation(Window.HeightProperty, null);
        window.Height = current;
    }

    private static void AnimateWindowHeight(Window window, double targetHeight)
    {
        CaptureAndClearHeightAnimation(window);

        var from = window.Height;
        var anim = new DoubleAnimation(from, targetHeight, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        anim.Completed += (_, _) =>
        {
            window.BeginAnimation(Window.HeightProperty, null);
            window.Height = targetHeight;
        };
        window.BeginAnimation(Window.HeightProperty, anim);
    }


    // 화면 가장자리 이동 버튼 핸들러
    private void EdgeLeftBtn_Click(object sender, RoutedEventArgs e)  => MoveToScreenEdge("Left");
    private void EdgeRightBtn_Click(object sender, RoutedEventArgs e) => MoveToScreenEdge("Right");
    private void EdgeUpBtn_Click(object sender, RoutedEventArgs e)    => MoveToScreenEdge("Up");
    private void EdgeDownBtn_Click(object sender, RoutedEventArgs e)  => MoveToScreenEdge("Down");

    /// 창을 지정 방향 화면 끝으로 이동한다 (반대축 위치는 유지).
    private void MoveToScreenEdge(string direction)
    {
        var window = Window.GetWindow(this);
        if (window is null) return;

        var screen = System.Windows.SystemParameters.WorkArea;
        const double margin = 8.0;

        switch (direction)
        {
            case "Left":  window.Left = screen.Left + margin; break;
            case "Right": window.Left = screen.Right - window.Width - margin; break;
            case "Up":    window.Top  = screen.Top  + margin; break;
            case "Down":  window.Top  = screen.Bottom - window.Height - margin; break;
        }
    }

    // T-4.8: 드래그 핸들 hover 애니메이션
    private void DragHandle_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DragPill is WpfRect pill)
            pill.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.55, TimeSpan.FromMilliseconds(120)));
    }

    private void DragHandle_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DragPill is WpfRect pill)
            pill.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.25, TimeSpan.FromMilliseconds(150)));
    }

    // 08: LiveRegion 공지
    private void AnnounceLiveRegion()
    {
        Dispatcher.InvokeAsync(() =>
        {
            var peer = FrameworkElementAutomationPeer.FromElement(ModeAnnouncer)
                       ?? new FrameworkElementAutomationPeer(ModeAnnouncer);
            peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }
}
