using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
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
    private const double SuggestionBarHeight = 28.0;
    private bool _autoCompleteBarAdded = false;

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
        }
    }

    private void ApplySuggestionBarHeight()
    {
        if (Window.GetWindow(this) is not { } window) return;

        var wantBar = _configService?.Current.AutoCompleteEnabled == true;

        if (wantBar && !_autoCompleteBarAdded)
        {
            window.Height += SuggestionBarHeight;
            _autoCompleteBarAdded = true;
        }
        else if (!wantBar && _autoCompleteBarAdded)
        {
            window.Height -= SuggestionBarHeight;
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

    // T-4.10: KeyUnit = min(가로 기준, 세로 기준) — Stretch=Uniform 동작 재현
    // 가장 넓은 행(Row1/2): 14개 키, 15단위 → 가로 = 15K + 14×4
    // 행 수: 5행, 각 1단위 높이 → 세로 = 5K + 5×4
    // KeyboardBorder.ActualHeight 를 직접 사용하므로 패널 토글 시에도 자동으로 재조정됨
    private void UpdateKeyUnit(double windowWidth)
    {
        if (DataContext is not MainViewModel vm) return;

        const double hPad  = 6 + 6;   // KeyboardBorder Padding 좌+우
        const double kPad  = 4 + 4;   // KeyboardBorder Padding 상+하
        const double units = 15.0;    // 가장 넓은 행의 단위 합계
        const double wKeys = 14.0;    // 가장 넓은 행의 키 개수
        const double rows  = 5.0;     // 행 수
        const double mKey  = 4.0;     // 키 한 개당 마진 총합 (Margin="2")

        double availW = windowWidth - hPad;
        double availH = KeyboardBorder.ActualHeight - kPad;

        if (availH < 1) return; // 레이아웃 완료 전 무시

        double kW = (availW - wKeys * mKey) / units;  // 가로 기준 KeyUnit
        double kH = (availH - rows  * mKey) / rows;   // 세로 기준 KeyUnit

        vm.Keyboard.KeyUnit = Math.Max(18, Math.Min(80, Math.Min(kW, kH)));
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
    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            // 가로·세로 변화량을 대각선으로 합산해 비율 유지
            double diagChange = (e.HorizontalChange + e.VerticalChange * _resizeAspectRatio) / 2.0;

            // T-7.4: 최소 너비 432px (minUnit 28 × 15단위 + 12px 패딩)
            double newWidth  = Math.Max(432, window.Width  + diagChange);
            double newHeight = newWidth / _resizeAspectRatio;

            // 최소 높이 250px 하한 적용 시 너비도 비율에 맞춰 재조정
            if (newHeight < 250)
            {
                newHeight = 250;
                newWidth  = Math.Max(432, newHeight * _resizeAspectRatio);
            }

            window.Width  = newWidth;
            window.Height = newHeight;
        }
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
}
