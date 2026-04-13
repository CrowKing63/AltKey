using System.Collections.ObjectModel;
using System.IO;
using AltKey.Models;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly LayoutService _layoutService;

    public KeyboardViewModel Keyboard { get; }

    [ObservableProperty]
    private string currentLayoutName = "";

    [ObservableProperty]
    private ObservableCollection<string> availableLayouts = [];

    public MainViewModel(
        ConfigService configService,
        LayoutService layoutService,
        KeyboardViewModel keyboardViewModel)
    {
        _configService = configService;
        _layoutService = layoutService;
        Keyboard = keyboardViewModel;
    }

    public Task InitializeAsync()
    {
        AvailableLayouts = new ObservableCollection<string>(
            _layoutService.GetAvailableLayouts());

        var defaultName = _configService.Current.DefaultLayout;
        SwitchLayout(defaultName);
        return Task.CompletedTask;
    }

    [RelayCommand]
    public void SwitchLayout(string name)
    {
        try
        {
            var layout = _layoutService.Load(name);
            Keyboard.LoadLayout(layout);
            CurrentLayoutName = layout.Name;
        }
        catch (FileNotFoundException)
        {
            // 레이아웃 파일 없음 — 무시
        }
    }
}
