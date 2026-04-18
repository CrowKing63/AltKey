using System.IO;
using System.Reflection;
using System.Text;

namespace AltKey.Services;

/// 내장 빈도 사전 + 사용자 학습을 결합한 한국어 단어 제안 서비스
public class KoreanDictionary
{
    private static readonly string Choseong19 = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    private readonly WordFrequencyStore _userStore;
    private readonly IReadOnlyList<string> _builtIn;
    private readonly Dictionary<char, List<string>> _builtInByChoseong = new();

    public KoreanDictionary(Func<string, WordFrequencyStore> storeFactory)
    {
        _userStore = storeFactory("ko");
        _builtIn = LoadBuiltIn();
        IndexBuiltInByChoseong();
    }

    private void IndexBuiltInByChoseong()
    {
        foreach (var w in _builtIn)
        {
            if (w.Length == 0) continue;
            var first = w[0];
            if (first < 0xAC00 || first > 0xD7A3) continue;
            int choIdx = (first - 0xAC00) / (21 * 28);
            char choChar = Choseong19[choIdx];
            if (!_builtInByChoseong.TryGetValue(choChar, out var list))
            {
                list = new List<string>();
                _builtInByChoseong[choChar] = list;
            }
            list.Add(w);
        }
    }

    private static bool IsCompatibleJamoChoseong(char c)
    {
        return c >= '\u3131' && c <= '\u314E';
    }

    /// prefix로 시작하는 한국어 단어 제안 (사용자 학습 우선, 그 다음 내장 사전)
    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (prefix.Length < 1) return [];

        // 초성만 입력된 경우 (호환 자모 U+3131~U+314E, 길이 1)
        if (prefix.Length == 1 && IsCompatibleJamoChoseong(prefix[0]))
            return GetSuggestionsByChoseong(prefix[0], count);

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

    private IReadOnlyList<string> GetSuggestionsByChoseong(char choseong, int count)
    {
        var result = new List<string>(count);
        var added = new HashSet<string>();

        // 1) 사용자 학습 사전에서 해당 초성으로 시작하는 단어 (빈도순)
        var userWords = _userStore.GetSuggestionsByChoseong(choseong, count);
        foreach (var w in userWords)
        {
            if (added.Add(w))
                result.Add(w);
            if (result.Count >= count) return result;
        }

        // 2) 내장 사전에서 해당 초성으로 시작하는 단어 보충
        if (_builtInByChoseong.TryGetValue(choseong, out var builtInList))
        {
            foreach (var w in builtInList)
            {
                if (added.Add(w))
                    result.Add(w);
                if (result.Count >= count) break;
            }
        }

        return result;
    }

    /// 한국어 단어를 사용자 빈도 저장소에 기록 (완성 음절 2개 이상만 학습)
    public void RecordWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        word = word.Trim();

        // 완성 한글 음절만 센다 (자모 단독 U+3131~U+3163 은 제외).
        int syllableCount = 0;
        foreach (var ch in word)
        {
            if (ch >= '\uAC00' && ch <= '\uD7A3')
                syllableCount++;
        }

        // 최소 2음절 이상일 때만 학습.
        if (syllableCount < 2) return;

        _userStore.RecordWord(word);
    }

    public WordFrequencyStore UserStore => _userStore;

    /// 사용자 학습 저장소에서 단어를 제거. 내장 사전은 건드리지 않음.
    /// 단어가 없거나 내장 전용이면 false.
    public bool TryRemoveUserWord(string word) =>
        !string.IsNullOrWhiteSpace(word) && _userStore.RemoveWord(word.Trim());

    /// 앱 종료 시 호출 — 사용자 학습 데이터 즉시 저장
    public void Flush() => _userStore.Flush();

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