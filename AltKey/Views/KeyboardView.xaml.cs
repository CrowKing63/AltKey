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

/// <summary>
/// [역할] 메인 키보드 창의 화면 표시와 사용자 인터페이스(UI) 동작을 제어하는 클래스입니다.
/// [기능] 창 크기 배율 적용, 드래그 이동, 창 접기/펼치기, 화면 가장자리 이동 등을 처리합니다.
/// </summary>
public partial class KeyboardView : System.Windows.Controls.UserControl
{
    private bool _isCollapsed = false;

    public bool IsCollapsed => _isCollapsed;
    private bool _autoCompleteBarAdded = false;

    private ConfigService? _configService;
    
    // 키보드를 접었을 때의 최소 높이입니다.
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

    // 창 크기가 수동으로 변경될 때 키 크기(KeyUnit)를 다시 계산합니다.
    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateKeyUnit(e.NewSize.Width);

    // 키보드 빈 공간을 클릭하면 열려있던 패널(이모지, 클립보드)을 닫습니다.
    private void KeyboardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.Emoji.IsVisible     = false;
            vm.Clipboard.IsVisible = false;
        }
    }

    private void KeyboardBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateKeyUnit(Window.GetWindow(this)?.Width ?? ActualWidth);

    // ── UI 레이아웃 상수 (화면 배치 및 크기 계산용) ──────────────────────────

    // 키보드 버튼의 최소/최대 크기(픽셀)입니다. 너무 작아져서 터치하기 어려워지는 것을 방지합니다.
    private const double MinKeyUnit = 28.0;
    private const double MaxKeyUnit = 80.0;

    // 키보드 전체 테두리의 안쪽 여백(Padding)입니다.
    private const double KbHorizontalPad = 12.0; // 좌우 합계
    private const double KbVerticalPad   = 8.0;  // 상하 합계

    // 키 버튼 사이의 기본 마진입니다.
    private const double KeyMargin       = 4.0;

    // 크기 계산의 기준이 되는 기본 키 크기입니다.
    private const double BaseKeyUnit  = 50.0;

    // 상단 드래그 핸들이나 버튼들이 위치한 헤더의 높이입니다.
    private const double HeaderHeight = 28.0;

    // 사용자가 설정할 수 있는 창 크기 배율의 최소/최대 범위(%)입니다.
    private const int    MinScale     = 60;
    private const int    MaxScale     = 200;

    // 어떤 상황에서도 유지해야 할 창의 최소 가로/세로 크기입니다.
    private const double AbsMinWindowWidth  = 400.0;
    private const double AbsMinWindowHeight = 180.0;

    /// <summary>
    /// 현재 윈도우 너비에 맞춰 버튼 하나하나의 크기(KeyUnit)를 조절합니다.
    /// 창을 늘리거나 줄일 때 버튼들이 그에 맞춰 자연스럽게 커지거나 작아지게 합니다.
    /// </summary>
    private void UpdateKeyUnit(double windowWidth)
    {
        if (DataContext is not MainViewModel vm) return;

        double units  = Math.Max(1, vm.Keyboard.MaxRowUnits);
        double wKeys  = Math.Max(1, vm.Keyboard.MaxRowCount);
        double rows   = Math.Max(1, vm.Keyboard.RowCount);

        bool barVisible = AutoCompleteBar?.Visibility == Visibility.Visible;
        double barH = barVisible ? AutoCompleteBar!.ActualHeight : 0;

        double availW = windowWidth - KbHorizontalPad;
        double totalBudget = KeyboardBorder.ActualHeight + barH;
        double availH = totalBudget - KbVerticalPad - (barVisible ? 4.0 : 0);

        if (availH < 1) return;

        double rowsDiv = rows + (barVisible ? 1 : 0);
        double kW = (availW - wKeys * KeyMargin) / units;
        double kH = (availH - rows  * KeyMargin) / rowsDiv;

        vm.Keyboard.KeyUnit = Math.Max(MinKeyUnit, Math.Min(MaxKeyUnit, Math.Min(kW, kH)));
    }

    /// <summary>
    /// 레이아웃 데이터(행, 열 개수 등)를 바탕으로 키보드 창의 기본 너비와 높이를 계산합니다.
    /// </summary>
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

    /// <summary>
    /// 설정된 배율(Scale)을 바탕으로 실제 윈도우 창의 가로, 세로 크기를 계산하여 적용합니다.
    /// </summary>
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

    /// <summary>
    /// 상단 핸들을 마우스로 잡고 끌었을 때 키보드 창이 따라 움직이게 합니다.
    /// </summary>
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
        var inputService = App.Services.GetRequiredService<InputService>();
        ModifierSafety.PrepareForWindowHide(inputService, "KeyboardView.CloseButton");
        Window.GetWindow(this)?.Hide();
    }

    /// <summary>
    /// 접기(▼)/펼치기(▲) 버튼을 눌렀을 때 실행됩니다.
    /// 창을 작게 줄여서 화면을 덜 차지하게 하거나 다시 원래 크기로 돌립니다.
    /// </summary>
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

    /// <summary>
    /// 창의 높이를 부드럽게 변화시키는 애니메이션 효과를 줍니다.
    /// L2: 애니메이션 최소화 모드 ON이거나 OS 설정(ClientAreaAnimation)이 꺼져 있으면 즉시 변경합니다.
    /// </summary>
    private void AnimateWindowHeight(Window window, double targetHeight)
    {
        // OS 설정 + 앱 설정 중 하나라도 애니메이션 최소화를 요구하면 즉시 적용
        bool reduceMotion = !SystemParameters.ClientAreaAnimation
            || (_configService?.Current.ReducedMotionEnabled == true);

        if (reduceMotion)
        {
            CaptureAndClearHeightAnimation(window);
            window.Height = targetHeight;
            return;
        }

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

    // ── 화면 가장자리 이동 기능 ─────────────────────────────────────────────
    // 버튼 클릭 시 키보드를 화면의 왼쪽, 오른쪽, 위, 아래 끝으로 즉시 이동시킵니다.
    private void EdgeLeftBtn_Click(object sender, RoutedEventArgs e)  => MoveToScreenEdge("Left");
    private void EdgeRightBtn_Click(object sender, RoutedEventArgs e) => MoveToScreenEdge("Right");
    private void EdgeUpBtn_Click(object sender, RoutedEventArgs e)    => MoveToScreenEdge("Up");
    private void EdgeDownBtn_Click(object sender, RoutedEventArgs e)  => MoveToScreenEdge("Down");

    /// <summary>
    /// 창을 지정 방향 화면 끝으로 이동한다 (반대축 위치는 유지).
    /// </summary>
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

    // 상단 드래그 핸들에 마우스를 올렸을 때 강조 효과를 줍니다.
    // L2: 애니메이션 최소화 모드 ON이면 즉시 Opacity를 변경합니다.
    private void DragHandle_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DragPill is not WpfRect pill) return;
        bool reduceMotion = !SystemParameters.ClientAreaAnimation
            || (_configService?.Current.ReducedMotionEnabled == true);
        if (reduceMotion)
        {
            pill.BeginAnimation(UIElement.OpacityProperty, null);
            pill.Opacity = 0.55;
            return;
        }
        pill.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.55, TimeSpan.FromMilliseconds(120)));
    }

    private void DragHandle_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DragPill is not WpfRect pill) return;
        bool reduceMotion = !SystemParameters.ClientAreaAnimation
            || (_configService?.Current.ReducedMotionEnabled == true);
        if (reduceMotion)
        {
            pill.BeginAnimation(UIElement.OpacityProperty, null);
            pill.Opacity = 0.25;
            return;
        }
        pill.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0.25, TimeSpan.FromMilliseconds(150)));
    }

    /// <summary>
    /// [접근성] 시각 장애인을 위한 화면 읽기 기능(스크린 리더)에 현재 상태를 알립니다.
    /// </summary>
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
