using System.Collections.ObjectModel;
using System.Linq;
using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using WpfApp = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

public partial class SuggestionBarViewModel : ObservableObject
{
    private readonly AutoCompleteService _autoComplete;
    private readonly InputService        _inputService;
    private readonly ConfigService       _configService;
    private readonly KoreanDictionary    _koDict;
    private readonly EnglishDictionary   _enDict;

    [ObservableProperty]
    private ObservableCollection<string> suggestions = [];

    [ObservableProperty]
    private bool hasSuggestions;

    [ObservableProperty]
    private bool isVisible;

    [ObservableProperty]
    private string currentWord = "";

    [ObservableProperty]
    private bool hasCurrentWord;

    // [접근성][L3] 스위치 스캔에서 훑을 제안 바 대상 목록입니다.
    [ObservableProperty]
    private ObservableCollection<ScanTargetVm> scanTargets = [];
    [ObservableProperty]
    private ObservableCollection<ScanTargetVm> suggestionScanTargets = [];

    // [접근성][L3] 키보드 스캔 엔진이 제안 목록 갱신을 감지할 수 있도록 알립니다.
    public event Action? ScanTargetsChanged;

    public SuggestionBarViewModel(
        AutoCompleteService autoComplete,
        InputService inputService,
        ConfigService configService,
        KoreanDictionary koDict,
        EnglishDictionary enDict)
    {
        _autoComplete = autoComplete;
        _inputService = inputService;
        _configService = configService;
        _koDict = koDict;
        _enDict = enDict;
        _autoComplete.SuggestionsChanged += OnSuggestionsChanged;
        _configService.ConfigChanged += OnConfigChanged;

        SetVisibleFromConfig();
    }

    private void SetVisibleFromConfig()
    {
        IsVisible = _configService.Current.AutoCompleteEnabled;
        RebuildScanTargets();
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName is null or nameof(AppConfig.AutoCompleteEnabled))
            SetVisibleFromConfig();
    }

    private void OnSuggestionsChanged(IReadOnlyList<string> newSuggestions)
    {
        string captured = _autoComplete.CurrentWord;
        void Apply()
        {
            CurrentWord = captured;
            HasCurrentWord = captured.Length > 0;
            Suggestions = new ObservableCollection<string>(newSuggestions);
            HasSuggestions = Suggestions.Count > 0;
            RebuildScanTargets();
        }

        var app = WpfApp.Current;
        if (app?.Dispatcher is null)
            Apply();
        else
            app.Dispatcher.Invoke(Apply);
    }

    private void RebuildScanTargets()
    {
        var nextTargets = new List<ScanTargetVm>();
        if (IsVisible)
        {
            if (HasCurrentWord && !string.IsNullOrWhiteSpace(CurrentWord))
            {
                nextTargets.Add(new ScanTargetVm
                {
                    DisplayText = CurrentWord,
                    Kind = "CurrentWord",
                    AccessibleName = $"현재 단어 저장 {CurrentWord}",
                    Activate = () => CommitCurrentWordCommand.Execute(null),
                    SetScanFocused = isFocused => CurrentWordScanFocused = isFocused
                });
            }

            foreach (var suggestion in Suggestions)
            {
                string item = suggestion;
                nextTargets.Add(new ScanTargetVm
                {
                    DisplayText = item,
                    Kind = "Suggestion",
                    AccessibleName = $"제안 단어 {item}",
                    Activate = () => AcceptSuggestionCommand.Execute(item),
                    SetScanFocused = isFocused =>
                    {
                        if (isFocused) FocusedSuggestion = item;
                        else if (FocusedSuggestion == item) FocusedSuggestion = "";
                    }
                });
            }
        }

        ScanTargets = new ObservableCollection<ScanTargetVm>(nextTargets);
        SuggestionScanTargets = new ObservableCollection<ScanTargetVm>(
            nextTargets.Where(t => t.Kind == "Suggestion"));
        ScanTargetsChanged?.Invoke();
    }

    [ObservableProperty]
    private bool currentWordScanFocused;

    [ObservableProperty]
    private string focusedSuggestion = "";

    [RelayCommand]
    private void AcceptSuggestion(string suggestion)
    {
        var (bsCount, fullWord) = _autoComplete.AcceptSuggestion(suggestion);
        if (_inputService.Mode == InputMode.Unicode)
        {
            _inputService.SendAtomicReplace(bsCount, fullWord);
            _inputService.ResetTrackedLength();
        }
        else
        {
            for (int i = 0; i < bsCount; i++)
                _inputService.SendKeyPress(VirtualKeyCode.VK_BACK);
            if (fullWord.Length > 0)
                _inputService.SendUnicode(fullWord);
        }
    }

    [RelayCommand]
    private void CommitCurrentWord()
    {
        _autoComplete.CommitCurrentWord();
        CurrentWord = "";
        HasCurrentWord = false;
        RebuildScanTargets();
    }

    [RelayCommand]
    private void CancelCurrentWord()
    {
        _autoComplete.CancelComposition();
        CurrentWord = "";
        HasCurrentWord = false;
        RebuildScanTargets();
    }

    [RelayCommand]
    private void RemoveSuggestion(string suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion)) return;

        bool removed = _autoComplete.ActiveSubmode == InputSubmode.HangulJamo
            ? _koDict.TryRemoveUserWord(suggestion)
            : _enDict.TryRemoveUserWord(suggestion);

        if (removed)
        {
            void Apply()
            {
                Suggestions.Remove(suggestion);
                HasSuggestions = Suggestions.Count > 0;
                RebuildScanTargets();
            }

            var app = WpfApp.Current;
            if (app?.Dispatcher is null)
                Apply();
            else
                app.Dispatcher.Invoke(Apply);
        }
    }
}
