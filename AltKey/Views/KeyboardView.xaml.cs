using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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
            double newWidth = Math.Max(400, window.Width + e.HorizontalChange);
            double newHeight = Math.Max(200, window.Height + e.VerticalChange);
            window.Width = newWidth;
            window.Height = newHeight;
        }
    }

    // 닫기 버튼
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Window.GetWindow(this)?.Close();
    }
}
