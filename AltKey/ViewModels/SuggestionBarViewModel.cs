using System.Collections.ObjectModel;
using AltKey.Services;
using WpfApp = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

/// T-9.3: 자동 완성 제안 바 ViewModel
public partial class SuggestionBarViewModel : ObservableObject
{
    private readonly AutoCompleteService _autoComplete;
    private readonly InputService        _inputService;

    [ObservableProperty]
    private ObservableCollection<string> suggestions = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    private bool hasSuggestions;

    public bool IsVisible => HasSuggestions;

    public SuggestionBarViewModel(AutoCompleteService autoComplete, InputService inputService)
    {
        _autoComplete = autoComplete;
        _inputService = inputService;
        _autoComplete.SuggestionsChanged += OnSuggestionsChanged;
    }

    private void OnSuggestionsChanged(IReadOnlyList<string> newSuggestions)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            Suggestions = new ObservableCollection<string>(newSuggestions);
            HasSuggestions = Suggestions.Count > 0;
        });
    }

    /// 제안 단어 클릭 시 나머지 문자를 타이핑하고 단어 학습
    [RelayCommand]
    private void AcceptSuggestion(string suggestion)
    {
        var remaining = _autoComplete.AcceptSuggestion(suggestion);
        if (remaining.Length > 0)
            _inputService.SendUnicode(remaining);
    }
}
