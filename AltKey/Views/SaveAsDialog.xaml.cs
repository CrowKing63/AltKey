using System.Windows;

namespace AltKey.Views;

public partial class SaveAsDialog : Window
{
    public string FileName => FileNameBox.Text.Trim();

    public SaveAsDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => FileNameBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FileNameBox.Text)) return;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
