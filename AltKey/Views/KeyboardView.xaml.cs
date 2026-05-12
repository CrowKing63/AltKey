using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Automation.Peers;
using AltKey.Models;
using AltKey.Services;
using AltKey.ViewModels;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Shapes.Rectangle;

namespace AltKey.Views;

/// <summary>
/// [역할] 메인 키보드 창의 화면 표시와 사용자 인터페이스(UI) 동작을 제어하는 클래스입니다.
/// [기능] 창 크기 배율 적용, 드래그 이동, 창 접기/펼치기, 화면 가장자리 이동 등을 처리합니다.
/// </summary>
public partial class KeyboardView : System.Windows.Controls.UserControl
{
    private bool _isCollapsed = false;
    private KeyboardWindowPlacement.VerticalAnchor _verticalAnchor = KeyboardWindowPlacement.VerticalAnchor.Freeform;
    private double _verticalAnchorGap;

    public bool IsCollapsed => _isCollapsed;
    private bool _autoCompleteBarAdded = false;

    private ConfigService? _configService;
    private bool _isDragHandlePressed;
    private WpfPoint _dragHandlePressPoint;
    
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
            RefreshVerticalAnchor(window);

            window.SizeChanged += OnWindowSizeChanged;

            if (DataContext is MainViewModel mainVm)
            {
                mainVm.Keyboard.LiveRegionChanged += AnnounceLiveRegion;

                mainVm.Keyboard.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(KeyboardViewModel.MaxRowUnits)
                                       or nameof(KeyboardViewModel.MaxRowCount)
                                       or nameof(KeyboardViewModel.RowCount)
                                       or nameof(KeyboardViewModel.TotalRowUnits))
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
            or nameof(AppConfig.KeyFontScalePercent)
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
    private const double SuggestionChipHeightRatio = 0.62;
    private const double EdgeDockMargin = 8.0;

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
        double rowUnits = Math.Max(1, vm.Keyboard.TotalRowUnits);

        double availW = windowWidth - KbHorizontalPad;
        double availH = KeyboardBorder.ActualHeight - KbVerticalPad;

        if (availH < 1) return;

        double kW = (availW - wKeys * KeyMargin) / units;
        double kH = (availH - rows * KeyMargin) / rowUnits;

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
        double rowUnits = Math.Max(1, vm.Keyboard.TotalRowUnits);

        double baseW = units * BaseKeyUnit
                     + wKeys * KeyMargin
                     + KbHorizontalPad;

        double keyboardH = rowUnits * BaseKeyUnit
                         + rows * KeyMargin
                         + KbVerticalPad;

        double barH = _autoCompleteBarAdded ? ComputeSuggestionBarHeight(BaseKeyUnit) : 0;

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
        double targetWidth = baseW * scale / 100.0;
        double targetHeight = _isCollapsed
            ? CollapsedWindowHeight
            : baseH * scale / 100.0;

        window.Width = targetWidth;
        ApplyWindowHeight(window, targetHeight);
    }

    /// <summary>
    /// 추천 바 높이는 칩 높이와 상하 여백을 합쳐 계산해, 창 배율과 큰 텍스트 모드 모두에서 같은 비례를 유지합니다.
    /// 축소 배율에서도 칩이 같이 줄어들도록 기본 하한은 본문 계산과 같은 기준으로 맞춥니다.
    /// </summary>
    private double ComputeSuggestionBarHeight(double keyUnit)
    {
        double scaledFontSize = 13.0 * (_configService?.Current.KeyFontScalePercent ?? 100) / 100.0;
        double fontAwareChipHeight = scaledFontSize + 10.0;
        double chipHeight = Math.Max(fontAwareChipHeight, keyUnit * SuggestionChipHeightRatio);
        return chipHeight + 6.0;
    }

    /// <summary>
    /// 상단바의 버튼 없는 빈 칸을 더블 클릭하면 키보드를 접거나 다시 펼칩니다.
    /// 일반적인 윈도우 제목 표시줄처럼 더블 클릭 시간은 운영체제 설정을 그대로 따릅니다.
    /// </summary>
    private void HeaderBlankArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2) return;
        if (IsHeaderInteractiveElement(e.OriginalSource as DependencyObject)) return;

        ToggleCollapsedState();
        e.Handled = true;
    }

    /// <summary>
    /// 상단 핸들은 한 번 누른 뒤 움직이면 창 이동, 같은 자리에서 두 번 누르면 접기/펼치기로 동작을 나눕니다.
    /// </summary>
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement dragHandle) return;

        if (e.ClickCount == 2)
        {
            ToggleCollapsedState();
            dragHandle.ReleaseMouseCapture();
            _isDragHandlePressed = false;
            e.Handled = true;
            return;
        }

        _isDragHandlePressed = true;
        _dragHandlePressPoint = e.GetPosition(this);
        dragHandle.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>
    /// 드래그 핸들을 누른 상태로 일정 거리 이상 움직였을 때만 창 이동을 시작해 더블 클릭과 충돌하지 않게 합니다.
    /// </summary>
    private void DragHandle_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isDragHandlePressed || e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not UIElement dragHandle) return;
        if (Window.GetWindow(this) is not { } window) return;

        var currentPoint = e.GetPosition(this);
        var movedX = Math.Abs(currentPoint.X - _dragHandlePressPoint.X);
        var movedY = Math.Abs(currentPoint.Y - _dragHandlePressPoint.Y);

        if (movedX < SystemParameters.MinimumHorizontalDragDistance
            && movedY < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _isDragHandlePressed = false;
        dragHandle.ReleaseMouseCapture();
        window.DragMove();
        RefreshVerticalAnchor(window);
    }

    /// <summary>
    /// 드래그 핸들에서 마우스를 떼면 대기 중이던 드래그 상태를 정리합니다.
    /// </summary>
    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement dragHandle && dragHandle.IsMouseCaptured)
        {
            dragHandle.ReleaseMouseCapture();
        }

        _isDragHandlePressed = false;
    }

    /// <summary>
    /// 외부 요인으로 마우스 캡처가 해제되더라도 다음 입력에 영향을 주지 않도록 상태를 초기화합니다.
    /// </summary>
    private void DragHandle_LostMouseCapture(object sender, WpfMouseEventArgs e)
    {
        _isDragHandlePressed = false;
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
    /// 접기(▼)/펼치기(▲) 버튼을 눌렀을 때 공용 접기 토글 로직을 실행합니다.
    /// </summary>
    private void CollapseButton_Click(object sender, RoutedEventArgs e)
        => ToggleCollapsedState();

    /// <summary>
    /// 버튼 클릭과 상단바 더블 클릭이 모두 같은 경로를 사용하도록 접힘 상태 변경을 한곳에서 처리합니다.
    /// </summary>
    private void ToggleCollapsedState()
    {
        var window = Window.GetWindow(this);
        if (window is null) return;

        if (!_isCollapsed)
        {
            ApplyWindowHeight(window, CollapsedWindowHeight, animate: true);
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

    /// <summary>
    /// 상단바의 실제 조작 요소(버튼, 토글 버튼 등)에서 발생한 입력인지를 검사합니다.
    /// 빈 영역 더블 클릭만 접기 동작으로 연결해 기존 버튼 기능과 섞이지 않게 합니다.
    /// </summary>
    private static bool IsHeaderInteractiveElement(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfButtonBase)
            {
                return true;
            }

            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    private static void CaptureAndClearHeightAnimation(Window window)
    {
        var current = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
        window.BeginAnimation(Window.HeightProperty, null);
        window.Height = current;
    }

    /// <summary>
    /// 위치 애니메이션을 중단하고 현재 Top 값을 고정합니다.
    /// 높이와 위치를 함께 움직일 때 두 속성이 같은 시작 시점을 보도록 맞춥니다.
    /// </summary>
    private static void CaptureAndClearTopAnimation(Window window)
    {
        var current = window.Top;
        window.BeginAnimation(Window.TopProperty, null);
        window.Top = current;
    }

    /// <summary>
    /// 현재 창이 상단에 더 붙어 있는지, 하단에 더 붙어 있는지 기억합니다.
    /// 높이가 바뀐 뒤에도 같은 가장자리 기준으로 다시 배치하려고 사용합니다.
    /// </summary>
    private void RefreshVerticalAnchor(Window window)
    {
        double currentHeight = GetCurrentWindowHeight(window);
        var workArea = SystemParameters.WorkArea;
        _verticalAnchor = KeyboardWindowPlacement.DetectVerticalAnchor(
            window.Top,
            currentHeight,
            workArea);

        // 화면 끝에 도킹된 상태로 판단됐을 때만 해당 간격을 저장합니다.
        // 이렇게 해야 아래쪽에 붙인 뒤 접기/펼치기나 추천 바 높이 변화가 있어도 같은 여백을 유지합니다.
        _verticalAnchorGap = _verticalAnchor switch
        {
            KeyboardWindowPlacement.VerticalAnchor.Bottom => Math.Max(0, workArea.Bottom - (window.Top + currentHeight)),
            KeyboardWindowPlacement.VerticalAnchor.Top => Math.Max(0, window.Top - workArea.Top),
            _ => 0
        };
    }

    /// <summary>
    /// 애니메이션 직전처럼 ActualHeight가 아직 갱신되지 않은 시점에도 사용할 현재 창 높이를 안전하게 읽습니다.
    /// </summary>
    private static double GetCurrentWindowHeight(Window window)
    {
        return window.ActualHeight > 0
            ? window.ActualHeight
            : window.Height;
    }

    /// <summary>
    /// 창 높이를 바꾸기 전에 현재 세로 기준점(상단/하단)에 맞춰 Top 좌표를 함께 보정합니다.
    /// </summary>
    private void ApplyWindowHeight(Window window, double targetHeight, bool animate = false)
    {
        double currentHeight = GetCurrentWindowHeight(window);
        double targetTop = KeyboardWindowPlacement.ComputeAnchoredTop(
            window.Top,
            currentHeight,
            targetHeight,
            SystemParameters.WorkArea,
            _verticalAnchor,
            GetAnchorGapOverride());

        if (animate)
        {
            AnimateWindowHeight(window, targetTop, targetHeight);
            return;
        }

        CaptureAndClearHeightAnimation(window);
        window.Top = targetTop;
        window.Height = targetHeight;
    }

    /// <summary>
    /// 도킹 상태일 때는 마지막으로 맞춰 둔 여백을 그대로 쓰고, 자유 위치는 현재 좌표를 유지합니다.
    /// </summary>
    private double? GetAnchorGapOverride()
    {
        return _verticalAnchor switch
        {
            KeyboardWindowPlacement.VerticalAnchor.Top or KeyboardWindowPlacement.VerticalAnchor.Bottom => _verticalAnchorGap,
            _ => null
        };
    }

    /// <summary>
    /// 창의 높이를 부드럽게 변화시키는 애니메이션 효과를 줍니다.
    /// L2: 애니메이션 최소화 모드 ON이거나 OS 설정(ClientAreaAnimation)이 꺼져 있으면 즉시 변경합니다.
    /// </summary>
    private void AnimateWindowHeight(Window window, double targetTop, double targetHeight)
    {
        // OS 설정 + 앱 설정 중 하나라도 애니메이션 최소화를 요구하면 즉시 적용
        bool reduceMotion = !SystemParameters.ClientAreaAnimation
            || (_configService?.Current.ReducedMotionEnabled == true);

        if (reduceMotion)
        {
            CaptureAndClearTopAnimation(window);
            CaptureAndClearHeightAnimation(window);
            window.Top = targetTop;
            window.Height = targetHeight;
            return;
        }

        CaptureAndClearTopAnimation(window);
        CaptureAndClearHeightAnimation(window);

        var topFrom = window.Top;
        var from = window.Height;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(150);

        var topAnim = new DoubleAnimation(topFrom, targetTop, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };

        var heightAnim = new DoubleAnimation(from, targetHeight, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop
        };

        heightAnim.Completed += (_, _) =>
        {
            window.BeginAnimation(Window.TopProperty, null);
            window.BeginAnimation(Window.HeightProperty, null);
            window.Top = targetTop;
            window.Height = targetHeight;
        };

        window.BeginAnimation(Window.TopProperty, topAnim);
        window.BeginAnimation(Window.HeightProperty, heightAnim);
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
        switch (direction)
        {
            case "Left":  window.Left = screen.Left + EdgeDockMargin; break;
            case "Right": window.Left = screen.Right - window.Width - EdgeDockMargin; break;
            case "Up":
                _verticalAnchor = KeyboardWindowPlacement.VerticalAnchor.Top;
                _verticalAnchorGap = EdgeDockMargin;
                window.Top = screen.Top + EdgeDockMargin;
                break;
            case "Down":
                _verticalAnchor = KeyboardWindowPlacement.VerticalAnchor.Bottom;
                _verticalAnchorGap = EdgeDockMargin;
                window.Top = screen.Bottom - window.Height - EdgeDockMargin;
                break;
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
