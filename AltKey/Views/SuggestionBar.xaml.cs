namespace AltKey.Views;

public partial class SuggestionBar : System.Windows.Controls.UserControl
{
    public SuggestionBar()
    {
        InitializeComponent();
    }

    private void CurrentWordSlot_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
