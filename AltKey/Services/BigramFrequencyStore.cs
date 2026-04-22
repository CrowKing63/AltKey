using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Timers;

namespace AltKey.Services;

/// (prev_word, next_word) → count 저장소. 언어별 인스턴스.
/// WordFrequencyStore와 동일 규율: 1초 디바운스 + 원자적 쓰기 + UnsafeRelaxedJsonEscaping.
public class BigramFrequencyStore
{
    private const int MaxPairs = 50000;
    private const int MaxNextPerPrev = 50;
    private const string Choseong19 = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    private readonly string _filePath;
    private Dictionary<string, Dictionary<string, int>> _bigrams = [];
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private readonly System.Timers.Timer _debounceTimer;
    private readonly object _saveLock = new();
    private bool _pending;

    public Exception? LastSaveError { get; private set; }

    public int Count
    {
        get
        {
            lock (_saveLock)
            {
                int total = 0;
                foreach (var nexts in _bigrams.Values) total += nexts.Count;
                return total;
            }
        }
    }

    public int NextCountFor(string prev)
    {
        if (string.IsNullOrWhiteSpace(prev)) return 0;
        prev = prev.Trim();
        lock (_saveLock)
        {
            return _bigrams.TryGetValue(prev, out var m) ? m.Count : 0;
        }
    }

    public bool Contains(string prev, string next)
    {
        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next)) return false;
        prev = prev.Trim(); next = next.Trim();
        lock (_saveLock)
        {
            return _bigrams.TryGetValue(prev, out var m) && m.ContainsKey(next);
        }
    }

    public BigramFrequencyStore(string languageCode)
        : this(PathResolver.DataDir, languageCode) { }

    public BigramFrequencyStore(string baseDir, string languageCode)
    {
        _filePath = Path.Combine(baseDir, $"user-bigrams.{languageCode}.json");
        Load();
        _debounceTimer = new System.Timers.Timer(1000) { AutoReset = false };
        _debounceTimer.Elapsed += (_, _) => FlushIfPending();
    }

    public void Record(string prev, string next)
    {
        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next)) return;
        prev = prev.Trim();
        next = next.Trim();
        if (prev.Length == 0 || next.Length == 0) return;

        bool pruneNeeded = false;
        lock (_saveLock)
        {
            if (!_bigrams.TryGetValue(prev, out var map))
            {
                map = new Dictionary<string, int>();
                _bigrams[prev] = map;
            }
            map[next] = (map.TryGetValue(next, out var c) ? c : 0) + 1;

            if (map.Count > MaxNextPerPrev) PrunePerPrev(map);
            pruneNeeded = Count > MaxPairs;
        }
        if (pruneNeeded)
        {
            lock (_saveLock) { PruneGlobal(); }
        }
        ScheduleSave();
    }

    public IReadOnlyList<(string Next, int Count)> GetNexts(string prev, string prefix, int count = 20)
    {
        if (string.IsNullOrWhiteSpace(prev)) return [];
        prev = prev.Trim();

        lock (_saveLock)
        {
            if (!_bigrams.TryGetValue(prev, out var map)) return [];

            IEnumerable<KeyValuePair<string, int>> candidates = map;

            if (!string.IsNullOrEmpty(prefix))
            {
                if (prefix.Length == 1 && IsCompatibleJamoChoseong(prefix[0]))
                {
                    char cho = prefix[0];
                    candidates = candidates.Where(kv =>
                        kv.Key.Length > 0
                        && kv.Key[0] >= '\uAC00' && kv.Key[0] <= '\uD7A3'
                        && GetChoseongChar(kv.Key[0]) == cho);
                }
                else
                {
                    candidates = candidates.Where(kv =>
                        kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && kv.Key.Length >= prefix.Length);
                }
            }

            return candidates
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(count)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }
    }

    public bool RemovePair(string prev, string next)
    {
        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next)) return false;
        prev = prev.Trim(); next = next.Trim();
        bool removed = false;
        lock (_saveLock)
        {
            if (_bigrams.TryGetValue(prev, out var map))
            {
                removed = map.Remove(next);
                if (map.Count == 0) _bigrams.Remove(prev);
            }
        }
        if (removed) ScheduleSave();
        return removed;
    }

    public int RemoveAllFor(string prev)
    {
        if (string.IsNullOrWhiteSpace(prev)) return 0;
        prev = prev.Trim();
        int removedCount = 0;
        lock (_saveLock)
        {
            if (_bigrams.TryGetValue(prev, out var map))
            {
                removedCount = map.Count;
                _bigrams.Remove(prev);
            }
        }
        if (removedCount > 0) ScheduleSave();
        return removedCount;
    }

    public void Clear()
    {
        lock (_saveLock) { _bigrams.Clear(); }
        ScheduleSave();
    }

    public IReadOnlyList<(string Prev, string Next, int Count)> GetAllPairs()
    {
        lock (_saveLock)
        {
            return _bigrams
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .SelectMany(outer => outer.Value
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(inner => (outer.Key, inner.Key, inner.Value)))
                .ToList();
        }
    }

    private void ScheduleSave()
    {
        lock (_saveLock) { _pending = true; }
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void FlushIfPending()
    {
        bool shouldSave;
        lock (_saveLock) { shouldSave = _pending; _pending = false; }
        if (shouldSave) Save();
    }

    public void Flush()
    {
        _debounceTimer.Stop();
        FlushIfPending();
    }

    public void Save()
    {
        try
        {
            Dictionary<string, Dictionary<string, int>> snapshot;
            lock (_saveLock)
            {
                snapshot = _bigrams.ToDictionary(
                    kv => kv.Key,
                    kv => new Dictionary<string, int>(kv.Value));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BigramFrequencyStore] Save failed ({_filePath}): {ex}");
            LastSaveError = ex;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.WriteAllText(_filePath, "{}");
            }
            var json = File.ReadAllText(_filePath);
            _bigrams = JsonSerializer
                .Deserialize<Dictionary<string, Dictionary<string, int>>>(json)
                ?? [];
        }
        catch { _bigrams = []; }
    }

    private static void PrunePerPrev(Dictionary<string, int> map)
    {
        int targetRemoveCount = map.Count / 5;
        if (targetRemoveCount == 0) return;
        var toRemove = map
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(targetRemoveCount)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var k in toRemove) map.Remove(k);
    }

    private void PruneGlobal()
    {
        int total = 0;
        foreach (var m in _bigrams.Values) total += m.Count;
        int targetRemoveCount = total / 5;
        if (targetRemoveCount == 0) return;

        var flat = _bigrams
            .SelectMany(outer => outer.Value.Select(inner =>
                (Prev: outer.Key, Next: inner.Key, Count: inner.Value)))
            .OrderBy(t => t.Count)
            .ThenBy(t => t.Prev, StringComparer.Ordinal)
            .ThenBy(t => t.Next, StringComparer.Ordinal)
            .Take(targetRemoveCount)
            .ToList();

        Debug.WriteLine($"[BigramFrequencyStore] Pruned {flat.Count} of {total} pairs.");
        foreach (var t in flat)
        {
            if (_bigrams.TryGetValue(t.Prev, out var map))
            {
                map.Remove(t.Next);
                if (map.Count == 0) _bigrams.Remove(t.Prev);
            }
        }
    }

    private static bool IsCompatibleJamoChoseong(char c) =>
        c >= '\u3131' && c <= '\u314E';

    private static char GetChoseongChar(char syllable)
    {
        int idx = (syllable - 0xAC00) / (21 * 28);
        return Choseong19[idx];
    }
}