using System.Windows;
using AltKey.Services;
using AltKey.ViewModels;

using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AltKey.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        FocusTracker.Register(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(WpfKeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape) { Close(); return; }
        base.OnKeyDown(e);
    }

    protected override void OnClosed(System.EventArgs e)
    {
        _vm.OnSettingsWindowClosed();
        base.OnClosed(e);
    }
}