using System.Windows;
using AltKey.ViewModels;

namespace AltKey.Views;

public partial class LayoutEditorWindow : Window
{
    private readonly LayoutEditorViewModel _vm;

    public LayoutEditorWindow(LayoutEditorViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveAsDialog { Owner = this };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.FileName))
            _vm.SaveAsCommand.Execute(dlg.FileName);
    }
}
