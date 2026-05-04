namespace AltKey.Services;

/// <summary>
/// 사용자 단어/바이그램 편집기가 사전 구현체(KoreanDictionary, EnglishDictionary)에 직접 결합되지 않도록
/// 공용 데이터 접근 경계를 정의합니다.
/// </summary>
public interface IUserDictionaryRepository
{
    void SelectLanguage(bool korean);

    string NormalizeWord(string rawWord);

    IReadOnlyList<(string Word, int Frequency)> GetAllWords();

    void SetWordFrequency(string word, int frequency);

    bool RemoveWord(string word);

    void ClearWords();

    IReadOnlyList<(string Prev, string Next, int Count)> GetAllBigrams();

    bool RemoveBigramPair(string prev, string next);

    int RemoveAllBigramsFor(string prev);

    void ClearBigrams();

    void Flush();
}

