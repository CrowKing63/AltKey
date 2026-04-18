using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfMsgBox = System.Windows.MessageBox;
using WpfMsgBoxButton = System.Windows.MessageBoxButton;
using WpfMsgBoxImage = System.Windows.MessageBoxImage;
using WpfMsgBoxResult = System.Windows.MessageBoxResult;

namespace AltKey.ViewModels;

public partial class UserDictionaryEditorViewModel : ObservableObject
{
    private readonly KoreanDictionary _koDict;
    private readonly EnglishDictionary _enDict;

    private WordFrequencyStore _activeStore;
    private BigramFrequencyStore _activeBigramStore;

    [ObservableProperty]
    private ObservableCollection<WordEntryVm> words = [];

    [ObservableProperty]
    private string searchQuery = "";

    [ObservableProperty]
    private string newWord = "";

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    private ObservableCollection<BigramPairRow> bigramRows = [];

    [ObservableProperty]
    private BigramPairRow? selectedBigramRow;

    [ObservableProperty]
    private bool isWordTabSelected = true;

    partial void OnIsWordTabSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(WordTabVisibility));
        OnPropertyChanged(nameof(BigramTabVisibility));
        OnPropertyChanged(nameof(IsBigramTabSelected));
    }

    public bool IsBigramTabSelected
    {
        get => !IsWordTabSelected;
        set
        {
            if (value && IsWordTabSelected)
            {
                IsWordTabSelected = false;
            }
        }
    }

    public Visibility WordTabVisibility => IsWordTabSelected ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BigramTabVisibility => IsWordTabSelected ? Visibility.Collapsed : Visibility.Visible;

    private bool _isKoreanTabActive = true;
    public bool IsKoreanTabActive
    {
        get => _isKoreanTabActive;
        set
        {
            if (SetProperty(ref _isKoreanTabActive, value) && value)
                SwitchTab(korean: true);
        }
    }
    public bool IsEnglishTabActive
    {
        get => !_isKoreanTabActive;
        set
        {
            if (value && _isKoreanTabActive)
            {
                _isKoreanTabActive = false;
                OnPropertyChanged(nameof(IsKoreanTabActive));
                OnPropertyChanged(nameof(IsEnglishTabActive));
                SwitchTab(korean: false);
            }
        }
    }

    public ICollectionView FilteredWords { get; }

    public UserDictionaryEditorViewModel(KoreanDictionary koDict, EnglishDictionary enDict)
    {
        _koDict = koDict;
        _enDict = enDict;
        _activeStore = _koDict.UserStore;
        _activeBigramStore = _koDict.BigramStore;

        FilteredWords = CollectionViewSource.GetDefaultView(Words);
        FilteredWords.Filter = obj =>
        {
            if (obj is not WordEntryVm entry) return false;
            if (string.IsNullOrWhiteSpace(SearchQuery)) return true;
            return entry.Word.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);
        };
    }

    public void OnLoaded()
    {
        SearchQuery = "";
        _isKoreanTabActive = true;
        OnPropertyChanged(nameof(IsKoreanTabActive));
        OnPropertyChanged(nameof(IsEnglishTabActive));
        _activeStore = _koDict.UserStore;
        _activeBigramStore = _koDict.BigramStore;
        ReloadWords();
        LoadBigrams();
    }

    public void OnClosing()
    {
        _koDict.Flush();
        _enDict.Flush();
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilteredWords.Refresh();
        UpdateStatus();
    }

    private void SwitchTab(bool korean)
    {
        _activeStore = korean ? _koDict.UserStore : _enDict.UserStore;
        _activeBigramStore = korean ? _koDict.BigramStore : _enDict.BigramStore;
        ReloadWords();
        LoadBigrams();
    }

    private void ReloadWords()
    {
        Words.Clear();
        foreach (var (w, f) in _activeStore.GetAllWords())
        {
            var entry = new WordEntryVm(w, f);
            entry.FrequencyChanged += OnEntryFrequencyChanged;
            Words.Add(entry);
        }
        FilteredWords.Refresh();
        UpdateStatus();
    }

    private void OnEntryFrequencyChanged(WordEntryVm entry)
    {
        _activeStore.SetFrequency(entry.Word, entry.Frequency);
        if (entry.Frequency <= 0)
        {
            Words.Remove(entry);
            FilteredWords.Refresh();
            UpdateStatus();
        }
    }

    [RelayCommand]
    private void AddWord()
    {
        var w = NewWord.Trim();
        if (w.Length == 0) return;

        var normalized = _isKoreanTabActive ? w : w.ToLowerInvariant();

        _activeStore.SetFrequency(normalized, GetFrequencyOrDefault(normalized, 1));
        NewWord = "";
        ReloadWords();
    }

    private int GetFrequencyOrDefault(string word, int fallback)
    {
        var existing = _activeStore.GetAllWords()
            .FirstOrDefault(p => p.Word == word);
        return existing.Word == null ? fallback : existing.Frequency + 1;
    }

    [RelayCommand]
    private void RemoveOne(WordEntryVm entry)
    {
        if (entry is null) return;
        _activeStore.RemoveWord(entry.Word);
        Words.Remove(entry);
        FilteredWords.Refresh();
        UpdateStatus();
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        var toRemove = Words.Where(w => w.IsSelected).ToList();
        if (toRemove.Count == 0) return;

        var result = WpfMsgBox.Show(
            $"선택한 {toRemove.Count}개의 단어를 삭제하시겠습니까?",
            "단어 삭제 확인",
            WpfMsgBoxButton.YesNo,
            WpfMsgBoxImage.Question);
        if (result != WpfMsgBoxResult.Yes) return;

        foreach (var entry in toRemove)
        {
            _activeStore.RemoveWord(entry.Word);
            Words.Remove(entry);
        }
        FilteredWords.Refresh();
        UpdateStatus();
    }

    [RelayCommand]
    private void ClearAll()
    {
        var label = _isKoreanTabActive ? "한국어" : "영어";
        var result = WpfMsgBox.Show(
            $"{label} 사용자 사전의 모든 단어({Words.Count}개)를 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
            "전체 삭제 확인",
            WpfMsgBoxButton.YesNo,
            WpfMsgBoxImage.Warning);
        if (result != WpfMsgBoxResult.Yes) return;

        _activeStore.Clear();
        Words.Clear();
        FilteredWords.Refresh();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int total = Words.Count;
        int shown = FilteredWords.Cast<object>().Count();
        StatusText = string.IsNullOrWhiteSpace(SearchQuery)
            ? $"총 {total}개 단어"
            : $"총 {total}개 단어 중 {shown}개 일치";
    }

    public void LoadBigrams()
    {
        var pairs = _activeBigramStore.GetAllPairs();
        BigramRows.Clear();
        foreach (var (prev, next, count) in pairs)
            BigramRows.Add(new BigramPairRow(prev, next, count));
    }

    [RelayCommand]
    private void RemoveBigramPair(BigramPairRow row)
    {
        if (row is null) return;
        if (_activeBigramStore.RemovePair(row.Prev, row.Next))
            BigramRows.Remove(row);
    }

    [RelayCommand]
    private void RemoveBigramsByPrev(BigramPairRow row)
    {
        if (row is null) return;
        int removed = _activeBigramStore.RemoveAllFor(row.Prev);
        if (removed > 0)
        {
            for (int i = BigramRows.Count - 1; i >= 0; i--)
                if (BigramRows[i].Prev == row.Prev) BigramRows.RemoveAt(i);
        }
    }

    [RelayCommand]
    private void ClearAllBigrams()
    {
        var label = _isKoreanTabActive ? "한국어" : "영어";
        var result = WpfMsgBox.Show(
            $"{label} 바이그램 데이터를 모두 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
            "바이그램 전체 삭제 확인",
            WpfMsgBoxButton.YesNo,
            WpfMsgBoxImage.Warning);
        if (result != WpfMsgBoxResult.Yes) return;

        _activeBigramStore.Clear();
        BigramRows.Clear();
    }
}

public sealed record BigramPairRow(string Prev, string Next, int Count);

public partial class WordEntryVm : ObservableObject
{
    public string Word { get; }

    private int _frequency;
    public int Frequency
    {
        get => _frequency;
        set
        {
            if (SetProperty(ref _frequency, value))
                FrequencyChanged?.Invoke(this);
        }
    }

    [ObservableProperty]
    private bool isSelected;

    public event Action<WordEntryVm>? FrequencyChanged;

    public WordEntryVm(string word, int frequency)
    {
        Word = word;
        _frequency = frequency;
    }
}