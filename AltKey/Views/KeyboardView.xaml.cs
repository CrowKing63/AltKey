using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private double _expandedHeight = 0;
    private bool _isCollapsed = false;

    public bool IsCollapsed => _isCollapsed;
    public double ExpandedHeight => _expandedHeight;

    /// AutoComplete가 켜져 있는 동안 창 높이에 반영된 바 높이(= 당시 KeyRowHeight).
    /// 런타임에 바를 끌 때 정확히 같은 픽셀만큼 창을 줄이기 위해 추적한다.
    private double _appliedBarHeight = 0;
    private bool _autoCompleteBarAdded = false;
    private bool _initialized = false;

    // 비율 고정 리사이즈: 드래그 시작 시 캡처한 가로/세로 비율
    private double _resizeAspectRatio = 900.0 / 350.0;

    private ConfigService? _configService;

    public KeyboardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            UpdateKeyUnit(window.Width);
            window.SizeChanged += OnWindowSizeChanged;

            _configService = App.Services.GetRequiredService<ConfigService>();
            _configService.ConfigChanged += OnConfigChanged;

            ApplySuggestionBarHeight();

            if (DataContext is MainViewModel mainVm)
            {
                mainVm.Keyboard.LiveRegionChanged += AnnounceLiveRegion;

                // 레이아웃 교체/편집기 저장으로 메트릭이 바뀌면 KeyUnit 재계산
                mainVm.Keyboard.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(KeyboardViewModel.MaxRowUnits)
                                       or nameof(KeyboardViewModel.MaxRowCount)
                                       or nameof(KeyboardViewModel.RowCount))
                    {
                        Dispatcher.InvokeAsync(() =>
                            UpdateKeyUnit(Window.GetWindow(this)?.Width ?? ActualWidth));
                    }
                    else if (e.PropertyName is nameof(KeyboardViewModel.KeyUnit))
                    {
                        // 바가 켜져 있으면, 현재 KeyRowHeight로 applied 값을 갱신.
                        // (창 높이는 건드리지 않음 — 사용자의 리사이즈 동작 방해 방지)
                        if (_autoCompleteBarAdded)
                            _appliedBarHeight = mainVm.Keyboard.KeyRowHeight;
                    }
                };
            }
        }
    }

    private void ApplySuggestionBarHeight()
    {
        if (Window.GetWindow(this) is not { } window) return;
        if (DataContext is not MainViewModel vm) return;

        var wantBar = _configService?.Current.AutoCompleteEnabled == true;

        if (!_initialized)
        {
            // 최초 Loaded: 저장된 창 높이를 그대로 사용.
            // UpdateKeyUnit의 폐쇄형 계산이 바 유무에 따라 KeyUnit을 자동 조정한다.
            _autoCompleteBarAdded = wantBar;
            _appliedBarHeight = wantBar ? vm.Keyboard.KeyRowHeight : 0;
            _initialized = true;
            return;
        }

        // 런타임 토글: 키보드 면적 유지를 위해 창 높이를 ±KeyRowHeight 만큼 조정.
        if (wantBar && !_autoCompleteBarAdded)
        {
            var h = vm.Keyboard.KeyRowHeight;
            window.Height += h;
            _appliedBarHeight = h;
            _autoCompleteBarAdded = true;
        }
        else if (!wantBar && _autoCompleteBarAdded)
        {
            window.Height -= _appliedBarHeight;
            _appliedBarHeight = 0;
            _autoCompleteBarAdded = false;
        }
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName is null or nameof(AppConfig.AutoCompleteEnabled))
        {
            Dispatcher.InvokeAsync(() => ApplySuggestionBarHeight());
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

    /// 현재 레이아웃 × MinKeyUnit 기준 최소 창 크기 계산
    private (double W, double H) ComputeMinWindowSize()
    {
        if (DataContext is not MainViewModel vm || Window.GetWindow(this) is not { } window)
            return (AbsMinWindowWidth, AbsMinWindowHeight);

        double units = Math.Max(1, vm.Keyboard.MaxRowUnits);
        double wKeys = Math.Max(1, vm.Keyboard.MaxRowCount);
        double rows  = Math.Max(1, vm.Keyboard.RowCount);

        double minW = units * MinKeyUnit + wKeys * KeyMargin + KbHorizontalPad;

        // 키보드 바깥(헤더·경고 배너·자동완성 바 등)이 차지하는 높이
        double nonKeyboardH = window.ActualHeight - KeyboardBorder.ActualHeight;
        if (nonKeyboardH < 1) nonKeyboardH = vm.Keyboard.KeyRowHeight;

        double minH = rows * MinKeyUnit + rows * KeyMargin + KbVerticalPad + nonKeyboardH;

        return (Math.Max(minW, AbsMinWindowWidth), Math.Max(minH, AbsMinWindowHeight));
    }

    // T-1.5: 창 드래그 이동
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.DragMove();
        }
    }

    // T-1.9: 창 리사이즈 핸들 — 드래그 시작 시 현재 비율 캡처
    private void ResizeGrip_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window && window.Height > 0)
            _resizeAspectRatio = window.Width / window.Height;
    }

    // T-1.9: 비율 고정(대각선) 리사이즈
    // 가로 변화량을 주 축으로 삼아 가로/세로 비율을 유지한 채 크기 조절
    // 최소 크기는 현재 레이아웃 × MinKeyUnit 기준으로 동적 계산
    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Window.GetWindow(this) is not { } window) return;

        var (minWidth, minHeight) = ComputeMinWindowSize();

        // 가로·세로 변화량을 대각선으로 합산해 비율 유지
        double diagChange = (e.HorizontalChange + e.VerticalChange * _resizeAspectRatio) / 2.0;

        double newWidth  = Math.Max(minWidth, window.Width + diagChange);
        double newHeight = newWidth / _resizeAspectRatio;

        if (newHeight < minHeight)
        {
            newHeight = minHeight;
            newWidth  = Math.Max(minWidth, newHeight * _resizeAspectRatio);
        }

        window.Width  = newWidth;
        window.Height = newHeight;
    }

    // T-7.2: 닫기 버튼 → 트레이로 숨기기
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
            _expandedHeight = window.Height;
            var anim = new DoubleAnimation(28, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            window.BeginAnimation(Window.HeightProperty, anim);
            if (FindName("CollapseIcon") is TextBlock collapseIcon)
                collapseIcon.Text = "▼";
            _isCollapsed = true;
        }
        else
        {
            var anim = new DoubleAnimation(_expandedHeight, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            window.BeginAnimation(Window.HeightProperty, anim);
            if (FindName("CollapseIcon") is TextBlock collapseIcon)
                collapseIcon.Text = "▲";
            _isCollapsed = false;
        }
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
