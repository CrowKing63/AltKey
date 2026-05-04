namespace AltKey.Services;

/// <summary>
/// 한글/영어 사용자 사전 및 바이그램 저장소를 편집기 관점에서 단일 경계로 묶는 어댑터입니다.
/// </summary>
public sealed class UserDictionaryRepository : IUserDictionaryRepository
{
    private readonly KoreanDictionary _koDict;
    private readonly EnglishDictionary _enDict;

    private WordFrequencyStore _activeStore;
    private BigramFrequencyStore _activeBigramStore;
    private bool _isKorean = true;

    public UserDictionaryRepository(KoreanDictionary koDict, EnglishDictionary enDict)
    {
        _koDict = koDict;
        _enDict = enDict;
        _activeStore = _koDict.UserStore;
        _activeBigramStore = _koDict.BigramStore;
    }

    public void SelectLanguage(bool korean)
    {
        _isKorean = korean;
        _activeStore = korean ? _koDict.UserStore : _enDict.UserStore;
        _activeBigramStore = korean ? _koDict.BigramStore : _enDict.BigramStore;
    }

    public string NormalizeWord(string rawWord)
    {
        var trimmed = rawWord.Trim();
        return _isKorean ? trimmed : trimmed.ToLowerInvariant();
    }

    public IReadOnlyList<(string Word, int Frequency)> GetAllWords() => _activeStore.GetAllWords();

    public void SetWordFrequency(string word, int frequency) => _activeStore.SetFrequency(word, frequency);

    public bool RemoveWord(string word) => _activeStore.RemoveWord(word);

    public void ClearWords() => _activeStore.Clear();

    public IReadOnlyList<(string Prev, string Next, int Count)> GetAllBigrams() => _activeBigramStore.GetAllPairs();

    public bool RemoveBigramPair(string prev, string next) => _activeBigramStore.RemovePair(prev, next);

    public int RemoveAllBigramsFor(string prev) => _activeBigramStore.RemoveAllFor(prev);

    public void ClearBigrams() => _activeBigramStore.Clear();

    public void Flush()
    {
        _koDict.Flush();
        _enDict.Flush();
    }
}

