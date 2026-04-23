using System.Windows;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey.Views;

public partial class LayoutEditorWindow : Window
{
    public LayoutEditorWindow(LayoutEditorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        FocusTracker.Register(this);
    }
}
