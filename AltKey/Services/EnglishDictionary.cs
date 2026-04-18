using System.IO;
using System.Reflection;
using System.Text;

namespace AltKey.Services;

public class EnglishDictionary
{
    private readonly WordFrequencyStore _userStore;
    private readonly IReadOnlyList<string> _builtIn;

    public EnglishDictionary(Func<string, WordFrequencyStore> storeFactory)
    {
        _userStore = storeFactory("en");
        _builtIn = LoadBuiltIn();
    }

    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (prefix.Length < 2) return [];

        var userSuggestions = _userStore.GetSuggestions(prefix, count);

        var needed = count - userSuggestions.Count;
        if (needed <= 0) return userSuggestions;

        var userSet = new HashSet<string>(userSuggestions);
        var builtInSuggestions = _builtIn
            .Where(w => w.StartsWith(prefix) && w.Length > prefix.Length
                        && !userSet.Contains(w))
            .Take(needed)
            .ToList();

        return [..userSuggestions, ..builtInSuggestions];
    }

    /// 영어 단어를 사용자 빈도 저장소에 기록 (최소 2자, 소문자 정규화)
    public void RecordWord(string word)
    {
        if (word.Length < 2) return;
        _userStore.RecordWord(word.ToLowerInvariant());
    }

    /// 앱 종료 시 호출 — 사용자 학습 데이터 즉시 저장
    public void Flush() => _userStore.Flush();

    private static IReadOnlyList<string> LoadBuiltIn()
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("AltKey.Assets.Data.en-words.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
    }
}
