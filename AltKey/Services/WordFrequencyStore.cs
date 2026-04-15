using System.IO;
using System.Text.Json;

namespace AltKey.Services;

/// T-9.3: 단어 빈도 저장소 — 로컬 JSON 파일에 학습 데이터 유지
public class WordFrequencyStore
{
    private const int MaxWords = 5000;

    private readonly string _filePath;
    private Dictionary<string, int> _freq = [];

    public WordFrequencyStore()
    {
        _filePath = Path.Combine(PathResolver.DataDir, "user-words.json");
        Load();
    }

    /// 단어 빈도 1 증가 (2자 미만 무시)
    public void RecordWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 2) return;
        word = word.Trim().ToLower();
        _freq[word] = (_freq.TryGetValue(word, out var c) ? c : 0) + 1;
        if (_freq.Count > MaxWords) PruneLowest();
    }

    /// prefix 로 시작하는 단어 제안 (빈도 내림차순, prefix 보다 긴 것만)
    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (prefix.Length < 2) return [];
        return _freq
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                         && kv.Key.Length > prefix.Length)
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();
    }

    /// 앱 종료 시 호출 — 파일 저장
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(_freq);
            File.WriteAllText(_filePath, json);
        }
        catch { /* 저장 실패 — 무시 */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            _freq = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? [];
        }
        catch { _freq = []; }
    }

    private void PruneLowest()
    {
        // 빈도 하위 20% 제거
        var threshold = _freq.Values.OrderBy(v => v).ElementAt(_freq.Count / 5);
        foreach (var key in _freq.Keys.Where(k => _freq[k] <= threshold).ToList())
            _freq.Remove(key);
    }
}
