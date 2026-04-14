using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AltKey.Services;
using AltKey.ViewModels;
using WpfRect = System.Windows.Shapes.Rectangle;

namespace AltKey.Views;

public partial class KeyboardView : System.Windows.Controls.UserControl
{
    private string _releaseUrl = string.Empty;
    private double _expandedHeight = 0;
    private bool _isCollapsed = false;

    // 비율 고정 리사이즈: 드래그 시작 시 캡처한 가로/세로 비율
    private double _resizeAspectRatio = 900.0 / 350.0;

    public KeyboardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // T-6.4: 로드 시 업데이트 체크
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var svc = new UpdateService();
            var (hasUpdate, version, url) = await svc.CheckAsync();
            if (hasUpdate)
            {
                _releaseUrl             = url;
                UpdateVersionText.Text  = version;
                UpdateBanner.Visibility = Visibility.Visible;
            }
        }
        catch { /* 업데이트 체크 실패 — 무시 */ }
    }

    // T-6.4: 다운로드 버튼
    private void OpenReleasePage_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_releaseUrl))
            Process.Start(new ProcessStartInfo(_releaseUrl) { UseShellExecute = true });
    }

    // T-6.4: 배너 닫기
    private void DismissUpdate_Click(object sender, RoutedEventArgs e)
    {
        UpdateBanner.Visibility = Visibility.Collapsed;
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
