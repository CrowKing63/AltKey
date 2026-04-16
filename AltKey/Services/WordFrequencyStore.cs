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

    /// 단어 빈도 1 증가 (영문 2자 미만 / 한글 1자 미만 무시)
    public void RecordWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        word = word.Trim();
        bool isLatin = word.All(c => c < 128);
        if (isLatin)
        {
            word = word.ToLower();
            if (word.Length < 2) return;
        }
        else
        {
            if (word.Length < 1) return;
        }
        _freq[word] = (_freq.TryGetValue(word, out var c) ? c : 0) + 1;
        if (_freq.Count > MaxWords) PruneLowest();
        Save();
    }

    /// prefix 로 시작하는 단어 제안 (영문 min 2자, 한글 min 1자)
    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 5)
    {
        if (string.IsNullOrEmpty(prefix)) return [];
        bool isLatin = prefix.All(c => c < 128);
        if (isLatin && prefix.Length < 2) return [];
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
            if (!File.Exists(_filePath))
            {
                // 파일이 없으면 빈 파일 생성 (설치 시 자동 생성)
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.WriteAllText(_filePath, "{}");
            }
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
