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
        IsVisible = _configService.Current.AutoCompleteEnabled
                  || _configService.Current.KoreanAutoCompleteEnabled;
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName is null or nameof(AppConfig.AutoCompleteEnabled)
            or nameof(AppConfig.KoreanAutoCompleteEnabled))
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
        // 기존 입력된 문자를 Backspace로 삭제
        for (int i = 0; i < bsCount; i++)
            _inputService.SendKeyPress(VirtualKeyCode.VK_BACK);
        // 전체 단어를 유니코드로 입력
        if (fullWord.Length > 0)
            _inputService.SendUnicode(fullWord);
    }
}
