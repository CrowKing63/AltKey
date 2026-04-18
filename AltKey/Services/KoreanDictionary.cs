using System.IO;
using System.Reflection;
using System.Text;

namespace AltKey.Services;

/// 내장 빈도 사전 + 사용자 학습을 결합한 한국어 단어 제안 서비스
public class KoreanDictionary
{
    private readonly WordFrequencyStore _userStore;
    private readonly IReadOnlyList<string> _builtIn;

    public KoreanDictionary(Func<string, WordFrequencyStore> storeFactory)
    {
        _userStore = storeFactory("ko");
        _builtIn = LoadBuiltIn();
    }

    /// prefix로 시작하는 한국어 단어 제안 (사용자 학습 우선, 그 다음 내장 사전)
    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (prefix.Length < 1) return [];

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

    /// 한국어 단어를 사용자 빈도 저장소에 기록 (최소 1자)
    public void RecordWord(string word)
    {
        if (word.Length < 1) return;
        _userStore.RecordWord(word);
    }

    private static IReadOnlyList<string> LoadBuiltIn()
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream("AltKey.Assets.Data.ko-words.txt");
        if (stream is null) return [];
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
    }
}