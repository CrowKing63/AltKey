using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AltKey.ViewModels;
using WpfRect = System.Windows.Shapes.Rectangle;

namespace AltKey.Views;

public partial class KeyboardView : System.Windows.Controls.UserControl
{
    public KeyboardView()
    {
        InitializeComponent();
    }

    // T-1.5: 창 드래그 이동
    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.DragMove();
        }
    }

    // T-1.9: 창 리사이즈 핸들
    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            double newWidth  = Math.Max(400, window.Width  + e.HorizontalChange);
            double newHeight = Math.Max(200, window.Height + e.VerticalChange);
            window.Width  = newWidth;
            window.Height = newHeight;
        }
    }

    // 닫기 버튼
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
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

    // T-3.10: 레이아웃 전환
    private void OnLayoutSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0
            && e.AddedItems[0] is string name
            && DataContext is MainViewModel vm)
        {
            vm.SwitchLayout(name);
        }
    }
}
