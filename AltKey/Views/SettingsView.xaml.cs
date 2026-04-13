namespace AltKey.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // 상위 KeyboardView의 DataContext(MainViewModel)를 찾아 IsSettingsOpen = false
        if (System.Windows.Window.GetWindow(this)?.DataContext is AltKey.ViewModels.MainViewModel vm)
        {
            vm.IsSettingsOpen = false;
        }
    }
}
