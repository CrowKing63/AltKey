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
    private const int MaxNextPerPrev = 50;
    private const string Choseong19 = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    private readonly int _maxPairs;
    private Dictionary<string, Dictionary<string, int>> _bigrams = [];
    // 캐시: _bigrams 전체 쌍 수. 모든 mutation 경로에서 함께 갱신되어 항상 실제와 일치한다 (O(1) 조회).
    private int _totalCount;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    // 디바운스 저장용
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
                return _totalCount;
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

    public BigramFrequencyStore(string baseDir, string languageCode, int maxPairs = 200000)
    {
        _maxPairs = maxPairs;
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
            bool isNewPair = !map.ContainsKey(next);
            map[next] = (map.TryGetValue(next, out var c) ? c : 0) + 1;
            if (isNewPair) _totalCount++;

            if (map.Count > MaxNextPerPrev) _totalCount -= PrunePerPrev(map);
            pruneNeeded = _totalCount > _maxPairs;
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
            if (removed) _totalCount--;
        }
        if (removed) ScheduleSave();
        return removed;
    }

    /// <summary>
    /// [역할] 편집기에서 지정한 bigram 빈도를 직접 저장합니다.
    /// [접근성] 별도 창 전환 없이 인라인 입력으로도 예측 데이터 보정이 가능하도록 지원합니다.
    /// </summary>
    public void SetPairCount(string prev, string next, int count)
    {
        if (string.IsNullOrWhiteSpace(prev) || string.IsNullOrWhiteSpace(next)) return;
        prev = prev.Trim(); next = next.Trim();
        count = Math.Max(1, count);

        lock (_saveLock)
        {
            if (!_bigrams.TryGetValue(prev, out var map))
            {
                map = new Dictionary<string, int>();
                _bigrams[prev] = map;
            }
            bool isNewPair = !map.ContainsKey(next);
            map[next] = count;
            if (isNewPair) _totalCount++;
            if (map.Count > MaxNextPerPrev) _totalCount -= PrunePerPrev(map);
        }

        ScheduleSave();
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
            _totalCount -= removedCount;
        }
        if (removedCount > 0) ScheduleSave();
        return removedCount;
    }

    public void Clear()
    {
        lock (_saveLock)
        {
            _bigrams.Clear();
            _totalCount = 0;
        }
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

    /// 앱 종료 시 호출 — 파일 저장
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
                // 파일이 없으면 빈 파일 생성 (설치 시 자동 생성)
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.WriteAllText(_filePath, "{}");
            }
            var json = File.ReadAllText(_filePath);
            _bigrams = JsonSerializer
                .Deserialize<Dictionary<string, Dictionary<string, int>>>(json)
                ?? [];
            int total = 0;
            foreach (var m in _bigrams.Values) total += m.Count;
            _totalCount = total;
        }
        catch { _bigrams = []; _totalCount = 0; }
    }

    /// <summary>
    /// [역할] 외부 프로세스(AltKey.Tools)에서 bigram 파일이 바뀐 뒤, 메모리 데이터를 파일 기준으로 다시 읽어옵니다.
    /// [주의] 디바운스 대기 중인 변경사항을 먼저 Flush하여 데이터 충돌 가능성을 줄입니다.
    /// </summary>
    public void ReloadFromDisk()
    {
        Flush();
        lock (_saveLock)
        {
            _bigrams = [];
        }
        Load();
    }

    private static int PrunePerPrev(Dictionary<string, int> map)
    {
        int targetRemoveCount = map.Count / 5;
        if (targetRemoveCount == 0) return 0;
        var toRemove = map
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(targetRemoveCount)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var k in toRemove) map.Remove(k);
        return toRemove.Count;
    }

    private void PruneGlobal()
    {
        int total = _totalCount;
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

        Debug.WriteLine(
            $"[BigramFrequencyStore] Pruned {flat.Count} of {total} pairs.");
        foreach (var t in flat)
        {
            if (_bigrams.TryGetValue(t.Prev, out var map))
            {
                map.Remove(t.Next);
                if (map.Count == 0) _bigrams.Remove(t.Prev);
            }
        }
        _totalCount -= flat.Count;
    }

    private static bool IsCompatibleJamoChoseong(char c) =>
        c >= '\u3131' && c <= '\u314E';

    private static char GetChoseongChar(char syllable)
    {
        int idx = (syllable - 0xAC00) / (21 * 28);
        return Choseong19[idx];
    }
}
