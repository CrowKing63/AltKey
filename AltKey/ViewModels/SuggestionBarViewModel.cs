using System.Collections.ObjectModel;
using AltKey.Models;
using AltKey.Services;
using WpfApp = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

public partial class SuggestionBarViewModel : ObservableObject
{
    private readonly AutoCompleteService _autoComplete;
    private readonly InputService        _inputService;
    private readonly ConfigService       _configService;

    [ObservableProperty]
    private ObservableCollection<string> suggestions = [];

    [ObservableProperty]
    private bool hasSuggestions;

    [ObservableProperty]
    private bool isVisible;

    public SuggestionBarViewModel(AutoCompleteService autoComplete, InputService inputService, ConfigService configService)
    {
        _autoComplete = autoComplete;
        _inputService = inputService;
        _configService = configService;
        _autoComplete.SuggestionsChanged += OnSuggestionsChanged;
        _configService.ConfigChanged += OnConfigChanged;

        SetVisibleFromConfig();
    }

    private void SetVisibleFromConfig()
    {
        IsVisible = _configService.Current.AutoCompleteEnabled;
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName is null or nameof(AppConfig.AutoCompleteEnabled))
            SetVisibleFromConfig();
    }

    private void OnSuggestionsChanged(IReadOnlyList<string> newSuggestions)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            Suggestions = new ObservableCollection<string>(newSuggestions);
            HasSuggestions = Suggestions.Count > 0;
        });
    }

    [RelayCommand]
    private void AcceptSuggestion(string suggestion)
    {
        var (bsCount, fullWord) = _autoComplete.AcceptSuggestion(suggestion);
        if (_inputService.Mode == InputMode.Unicode)
        {
            _inputService.SendAtomicReplace(bsCount, fullWord);
            _inputService.TrackedOnScreenLength = fullWord.Length;
        }
        else
        {
            for (int i = 0; i < bsCount; i++)
                _inputService.SendKeyPress(VirtualKeyCode.VK_BACK);
            if (fullWord.Length > 0)
                _inputService.SendUnicode(fullWord);
        }
    }
}
