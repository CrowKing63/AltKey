using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Timers;

namespace AltKey.Services;

/// T-9.3: 단어 빈도 저장소 — 언어별 인스턴스, 로컬 JSON 파일에 학습 데이터 유지
public class WordFrequencyStore
{
    private const int MaxWords = 5000;

    private readonly string _filePath;
    private Dictionary<string, int> _freq = [];
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    // 디바운스 저장용
    private readonly System.Timers.Timer _debounceTimer;
    private readonly object _saveLock = new();
    private bool _pending;

    public Exception? LastSaveError { get; private set; }

    /// 테스트용: 현재 저장된 단어 수
    public int Count
    {
        get { lock (_saveLock) { return _freq.Count; } }
    }

    /// 테스트용: 단어 존재 여부
    public bool Contains(string word)
    {
        lock (_saveLock) { return _freq.ContainsKey(word); }
    }

    public WordFrequencyStore(string languageCode)
        : this(PathResolver.DataDir, languageCode)
    {
    }

    /// 테스트용: 커스텀 디렉토리 지정
    public WordFrequencyStore(string baseDir, string languageCode)
    {
        _filePath = Path.Combine(baseDir, $"user-words.{languageCode}.json");
        Load();
        _debounceTimer = new System.Timers.Timer(1000) { AutoReset = false };
        _debounceTimer.Elapsed += (_, _) => FlushIfPending();
    }

    /// 단어 빈도 1 증가 (최소 길이·대소문자 정규화는 호출자 책임)
    public void RecordWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        word = word.Trim();
        if (word.Length == 0) return;
        lock (_saveLock)
        {
            _freq[word] = (_freq.TryGetValue(word, out var c) ? c : 0) + 1;
            if (_freq.Count > MaxWords) PruneLowest();
        }
        ScheduleSave();
    }

    /// 단어 빈도를 명시적으로 설정. <=0 이면 제거.
    /// 새 단어 추가 겸용.
    public void SetFrequency(string word, int frequency)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        word = word.Trim();
        if (word.Length == 0) return;

        lock (_saveLock)
        {
            if (frequency <= 0)
            {
                _freq.Remove(word);
            }
            else
            {
                _freq[word] = frequency;
                if (_freq.Count > MaxWords) PruneLowest();
            }
        }
        ScheduleSave();
    }

    /// 저장된 모든 단어의 스냅샷 반환. 빈도 내림차순, 같은 빈도는 단어 오름차순.
    public IReadOnlyList<(string Word, int Frequency)> GetAllWords()
    {
        lock (_saveLock)
        {
            return _freq
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }
    }

    /// 저장소를 완전히 비운다 (UI 측 확인 대화상자 뒤에서만 호출할 것).
    public void Clear()
    {
        lock (_saveLock) { _freq.Clear(); }
        ScheduleSave();
    }

    /// 단어를 사용자 사전에서 제거. 존재하지 않으면 false.
    /// 성공 시 디바운스 저장 예약.
    public bool RemoveWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        word = word.Trim();
        bool removed;
        lock (_saveLock)
        {
            removed = _freq.Remove(word);
        }
        if (removed) ScheduleSave();
        return removed;
    }

    /// prefix 로 시작하는 단어 제안 (빈도 내림차순)
    public IReadOnlyList<string> GetSuggestions(string prefix, int count = 20)
    {
        if (string.IsNullOrEmpty(prefix)) return [];
        lock (_saveLock)
        {
            return _freq
                .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                             && kv.Key.Length > prefix.Length)
                .OrderByDescending(kv => kv.Value)
                .Take(count)
                .Select(kv => kv.Key)
                .ToList();
        }
    }

    /// 초성(호환 자모)으로 시작하는 단어 제안 (첫 음절의 초성이 일치하는 단어)
    public IReadOnlyList<string> GetSuggestionsByChoseong(char choseong, int count = 20)
    {
        lock (_saveLock)
        {
            return _freq
                .Where(kv => kv.Key.Length > 0
                             && kv.Key[0] >= '\uAC00' && kv.Key[0] <= '\uD7A3'
                             && GetChoseongChar(kv.Key[0]) == choseong)
                .OrderByDescending(kv => kv.Value)
                .Take(count)
                .Select(kv => kv.Key)
                .ToList();
        }
    }

    private static char GetChoseongChar(char syllable)
    {
        const string choseong = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        int idx = (syllable - 0xAC00) / (21 * 28);
        return choseong[idx];
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
            Dictionary<string, int> snapshot;
            lock (_saveLock) { snapshot = new(_freq); }

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WordFrequencyStore] Save failed ({_filePath}): {ex}");
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
            _freq = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? [];
        }
        catch { _freq = []; }
    }

    private void PruneLowest()
    {
        int targetRemoveCount = _freq.Count / 5;
        if (targetRemoveCount == 0) return;

        var toRemove = _freq
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(targetRemoveCount)
            .Select(kv => kv.Key)
            .ToList();

        Debug.WriteLine(
            $"[WordFrequencyStore] Pruned {toRemove.Count} of {_freq.Count} words.");

        foreach (var k in toRemove) _freq.Remove(k);
    }
}
