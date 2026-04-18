# 01 — `BigramFrequencyStore` 신규 서비스

> **목적**: 언어별 (prev_word, next_word) → count 쌍을 로컬 JSON에 누적·조회하는 저장소 서비스를 신규로 추가한다. `WordFrequencyStore`의 규율(1초 디바운스·원자적 쓰기·`UnsafeRelaxedJsonEscaping`·`_saveLock`)을 그대로 계승하되, 키가 `(string, string)`이라는 점만 다르다.
>
> **이 파일만 읽고도 완수 가능해야 한다.** 다른 지시서(02~05)의 코드는 건드리지 않는다.

---

## 1. 체크리스트

- [ ] `AltKey/Services/BigramFrequencyStore.cs` 신규 파일 생성.
- [ ] `AltKey/App.xaml.cs`의 `ServiceCollection`에 `Func<string, BigramFrequencyStore>` 팩토리 DI 등록.
- [ ] `AltKey.Tests/Services/BigramFrequencyStoreTests.cs` 신규 테스트 파일 생성.
- [ ] `dotnet build` 성공.
- [ ] `dotnet test` 신규 테스트 전부 녹색 + 기존 테스트 회귀 없음.

---

## 2. 설계

### 2.1 데이터 모델

- 내부 상태: `Dictionary<string, Dictionary<string, int>> _bigrams`
  - 외부 키: `prev` (확정된 이전 단어, 공백 trim 후 원문 그대로. 영어는 호출자에서 이미 소문자로 정규화된 상태로 온다고 가정).
  - 내부 키: `next` (현재 확정 중인 단어, 동일 정규화 규율).
  - 값: 출현 횟수(양수 정수).
- 이중 Dictionary를 채택한 이유:
  - (a) "prev=안녕일 때 상위 N개 next"를 조회하는 핵심 유스케이스가 단일 `prev` 키 접근으로 끝난다.
  - (b) JSON 직렬화 포맷이 자연스럽다(`{ "안녕": {"하세요": 3, ...}, ... }`).
  - (c) `prev` 단위로 일괄 삭제·프루닝이 쉽다(04번 지시서에서 편집 UI가 필요로 함).

### 2.2 JSON 스키마

파일: `PathResolver.DataDir/user-bigrams.{languageCode}.json`

```json
{
  "안녕": { "하세요": 3, "해": 1 },
  "2026년": { "4월": 5, "초반에": 2 }
}
```

- 최상위 루트는 빈 객체 `{}`로 초기화(파일 부재 시 생성).
- `JsonSerializerOptions`는 `WordFrequencyStore._jsonOptions`와 동일(`UnsafeRelaxedJsonEscaping` + `WriteIndented = true`).

### 2.3 프루닝 정책

- **전체 쌍 수 상한**: `MaxPairs = 50000` (unigram의 10배. 사용자 어휘 5천 개 가정 시 단어당 평균 10개의 next와 연관될 수 있다고 상정).
- **prev별 상한**: `MaxNextPerPrev = 50` (한 이전 단어에 연관된 next는 상위 50개까지만). 초과 시 해당 prev의 하위 20%를 잘라낸다.
- **전체 상한 초과 시**: `_bigrams.SelectMany(kv => kv.Value.Select(...))`를 (count asc, prev asc, next asc)로 정렬해 하위 `총수 / 5`개를 제거한다. `WordFrequencyStore.PruneLowest`의 설계를 그대로 따른다(정확히 N개, 동점 허용).
- 프루닝 후 next 맵이 빈 딕셔너리가 되면 prev 키도 제거한다.

### 2.4 공개 API

```csharp
namespace AltKey.Services;

public class BigramFrequencyStore
{
    public BigramFrequencyStore(string languageCode);          // 프로덕션용
    public BigramFrequencyStore(string baseDir, string languageCode); // 테스트용

    public Exception? LastSaveError { get; }

    /// 테스트/통계용. 총 (prev,next) 쌍 수.
    public int Count { get; }

    /// prev에 대해 쌓인 next 개수. 없으면 0.
    public int NextCountFor(string prev);

    /// 특정 쌍이 저장되어 있는지 (주로 테스트).
    public bool Contains(string prev, string next);

    /// 쌍 빈도 1 증가. 입력 trim·null/공백 가드 내장.
    /// prev 또는 next가 null/공백이면 no-op.
    public void Record(string prev, string next);

    /// prev에 딸린 next들 중 prefix로 시작하는 상위 N개를 빈도 내림차순으로 반환.
    /// prefix가 빈 문자열이면 prev 전체 next 중 상위 N개를 반환.
    /// 초성 자모(U+3131~U+314E) 한 글자 prefix도 대응(첫 음절의 초성이 일치하면 매치).
    public IReadOnlyList<(string Next, int Count)> GetNexts(string prev, string prefix, int count = 5);

    /// 개별 쌍 제거. 존재하지 않으면 false.
    /// 해당 prev의 next 맵이 비게 되면 prev 키도 정리.
    public bool RemovePair(string prev, string next);

    /// 특정 prev 전체(딸린 모든 next) 삭제. 삭제된 쌍 수 반환.
    public int RemoveAllFor(string prev);

    /// 저장소 전체 비움 (사용자 확인 뒤에만).
    public void Clear();

    /// 편집기 UI용 스냅샷. prev 오름차순, 같은 prev 안에서 count 내림차순.
    /// 대량 데이터에 대비해 IEnumerable이 아닌 List로 한 번에 snapshot.
    public IReadOnlyList<(string Prev, string Next, int Count)> GetAllPairs();

    /// 앱 종료 시 즉시 저장.
    public void Flush();

    /// 디바운스 타이머 경유 저장 실행 — 테스트에서 직접 호출 가능.
    public void FlushIfPending();
}
```

### 2.5 동시성·락

- 내부 락 `private readonly object _saveLock = new();`
- 모든 public 메서드는 `_freq` 대신 `_bigrams`를 건드릴 때 `_saveLock`을 잡는다.
- `ScheduleSave()`는 반드시 락 바깥에서 호출(타이머 내부 락 재진입 회피).
- WPF Dispatcher 단일 스레드 환경이지만, `System.Timers.Timer` 콜백이 ThreadPool에서 실행되므로 `FlushIfPending` → `Save`가 락을 재진입하지 않도록 주의.

### 2.6 초성 단독 매칭

- prefix가 길이 1이고 U+3131~U+314E(호환 자모 초성 영역)일 때:
  - `WordFrequencyStore.GetSuggestionsByChoseong`와 동일한 `GetChoseongChar` 로직을 **소유 복사**한다(정적 메서드 한 줄, 공유 유틸로 뽑지 않음 — 범위 최소화).
  - next 단어의 첫 음절이 U+AC00~U+D7A3 범위이고 초성이 일치하면 매치.
- prefix가 빈 문자열일 때: 필터 없이 상위 N개.
- 영어 prefix(ASCII)일 때: `StringComparison.OrdinalIgnoreCase`로 StartsWith. 호출자(EnglishDictionary)가 이미 소문자 정규화 후 넘긴다는 가정이지만 방어적으로 OrdinalIgnoreCase 사용.

---

## 3. 구현 스켈레톤

> 아래는 실제로 붙여 넣을 수 있는 수준의 참조 구현. 필요한 부분만 골라 사용하되, **`WordFrequencyStore`의 구조를 그대로 재활용해야 한다** — 디바운스·원자적 쓰기·`UnsafeRelaxedJsonEscaping`·예외 로깅·`LastSaveError` 노출·테스트용 baseDir 오버로드.

```csharp
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
            pruneNeeded = Count > MaxPairs; // 락 내부에서 Count 재계산 대신 간단히 호출
        }
        if (pruneNeeded)
        {
            lock (_saveLock) { PruneGlobal(); }
        }
        ScheduleSave();
    }

    public IReadOnlyList<(string Next, int Count)> GetNexts(string prev, string prefix, int count = 5)
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
```

### 3.1 주의할 점

- `Record()`가 `Count > MaxPairs` 판정을 위해 `Count`를 호출하면 재귀 락 진입이 된다(같은 `_saveLock`을 잠그러 다시 들어감 — C# `lock`은 재진입 가능하지만 성능 비용 있음). 위 스켈레톤처럼 락 안에서 직접 `_bigrams.Values` 합계를 내거나, `Count` 호출을 락 바깥에서 한다.
- `GetAllPairs()`가 대규모 결과를 반환할 때는 락 안에 머무는 시간이 길다. 현재 `MaxPairs = 50000` 전제하에 수천 대역이면 OK지만, 테스트는 소규모로만 한다.
- **절대 하지 말 것**: `JsonSerializerOptions`에서 `UnsafeRelaxedJsonEscaping` 제거, `File.Move` 원자적 쓰기를 `File.WriteAllText` 직접 호출로 교체, 디바운스 제거.

---

## 4. DI 등록

`AltKey/App.xaml.cs`의 `ConfigureServices` 블록에서 `WordFrequencyStore` 팩토리 바로 아래에 다음을 추가.

```csharp
services.AddSingleton<Func<string, BigramFrequencyStore>>(
    sp => lang => new BigramFrequencyStore(lang));
```

> **주의**: `WordFrequencyStore`와 마찬가지로 팩토리가 호출마다 새 인스턴스를 만든다. 이는 언어별(ko/en) 하나씩 필요하기 때문이다. `KoreanDictionary`/`EnglishDictionary`가 각각 팩토리를 주입받아 자신의 언어 코드로 호출한다(02번 지시서 참고).

---

## 5. 테스트 명세

`AltKey.Tests/Services/BigramFrequencyStoreTests.cs` 신규 파일. `WordFrequencyStoreTests.cs`의 스타일(임시 디렉터리 + `Directory.CreateTempSubdirectory`)을 그대로 모방한다.

```csharp
using AltKey.Services;
using System.IO;
using System.Text.Json;
using Xunit;

namespace AltKey.Tests.Services;

public class BigramFrequencyStoreTests : IDisposable
{
    private readonly string _tempDir;

    public BigramFrequencyStoreTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("altkey-bigram-tests").FullName;
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* 베스트 에포트 */ }
    }

    private BigramFrequencyStore NewStore(string lang = "ko")
        => new(_tempDir, lang);

    [Fact]
    public void Record_new_pair_increments_count_to_1() { /* ... */ }

    [Fact]
    public void Record_same_pair_twice_has_count_2() { /* ... */ }

    [Fact]
    public void Record_null_or_whitespace_is_noop() { /* ... */ }

    [Fact]
    public void GetNexts_filters_by_prefix_and_orders_by_count_desc() { /* ... */ }

    [Fact]
    public void GetNexts_empty_prefix_returns_top_N() { /* ... */ }

    [Fact]
    public void GetNexts_choseong_jamo_prefix_matches_by_initial_consonant() { /* ... */ }

    [Fact]
    public void GetNexts_with_unknown_prev_returns_empty() { /* ... */ }

    [Fact]
    public void RemovePair_removes_only_target_and_cleans_empty_prev() { /* ... */ }

    [Fact]
    public void RemoveAllFor_removes_all_nexts_of_prev() { /* ... */ }

    [Fact]
    public void Clear_empties_store() { /* ... */ }

    [Fact]
    public void GetAllPairs_snapshot_is_sorted_prev_asc_then_count_desc() { /* ... */ }

    [Fact]
    public void Flush_writes_json_with_unicode_escaping_disabled()
    {
        var store = NewStore("ko");
        store.Record("안녕", "하세요");
        store.Flush();

        var path = Path.Combine(_tempDir, "user-bigrams.ko.json");
        var text = File.ReadAllText(path);
        Assert.Contains("안녕", text);    // \uXXXX 로 이스케이프되면 실패
        Assert.Contains("하세요", text);
    }

    [Fact]
    public void Reload_from_disk_round_trips_all_pairs() { /* Flush → new store → 동일 데이터 */ }

    [Fact]
    public void PerPrev_pruning_limits_nexts_below_cap()
    {
        // 51개 next 기록 후 Record 한 번 더 → map.Count가 MaxNextPerPrev 이하
        // 정확한 값 비교보다 "상한 이하"인지 확인
    }
}
```

### 5.1 테스트 구현 가이드

- 각 테스트는 `NewStore()`로 새 인스턴스를 만든다. 디바운스 타이머가 살아있어도 `Dispose`에서 정리되지 않는다 — 필요 시 `store.Flush()`로 명시적 종결.
- 한글 이스케이프 검증 테스트(`Flush_writes_json_with_unicode_escaping_disabled`)는 실제 파일을 열어 문자열 포함 여부를 확인한다.
- 대용량 프루닝 테스트는 `MaxPairs` 직접 검증 대신 "상한 초과 시 프루닝이 돌아 하한 이하로 떨어진다"는 정성적 검증으로 충분하다.
- Thread-safety 단위 테스트는 이번에는 생략. CORE-LOGIC 보호 규정상 WPF Dispatcher 단일 스레드 전제이므로, 통합 테스트에서 간접 확인한다.

---

## 6. 완료 조건

- [ ] `BigramFrequencyStore.cs` 커밋됨, 공개 API가 §2.4 시그니처와 일치.
- [ ] DI 등록이 `App.xaml.cs`에 추가됨(다른 코드의 사용 여부와 무관).
- [ ] 신규 테스트 14개 모두 녹색.
- [ ] `dotnet build`·`dotnet test` 전체 녹색(기존 테스트 회귀 없음).
- [ ] 커밋 메시지: `feat(ac-bigram): add BigramFrequencyStore with JSON persistence`.

---

## 7. 다음 단계

01 완료 후 [02-dictionary-context.md](02-dictionary-context.md)로 이동해 `KoreanDictionary`/`EnglishDictionary`가 이 저장소를 조회하도록 확장한다.
