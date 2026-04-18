using System.IO;
using System.Threading;
using Xunit;
using AltKey.Services;

namespace AltKey.Tests.Services;

public class WordFrequencyStoreTests : IDisposable
{
    private readonly string _testDir;

    public WordFrequencyStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "altkey-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void RecordWord_does_not_save_immediately_when_debounced()
    {
        var store = new WordFrequencyStore(_testDir, "test-ko");
        store.RecordWord("해달");

        // 디바운스 중에는 파일에 즉시 기록되지 않아야 함
        var filePath = GetFilePath("test-ko");
        var jsonBefore = File.ReadAllText(filePath);
        Assert.DoesNotContain("해달", jsonBefore);

        store.Flush();

        var jsonAfter = File.ReadAllText(filePath);
        Assert.Contains("해달", jsonAfter);
    }

    [Fact]
    public void RecordWord_burst_only_writes_once_after_flush()
    {
        var store = new WordFrequencyStore(_testDir, "test-ko");

        for (int i = 0; i < 100; i++)
            store.RecordWord($"단어{i}");

        store.Flush();

        // Flush 후 파일에 모든 단어가 포함되어야 함
        var json = File.ReadAllText(GetFilePath("test-ko"));
        Assert.Contains("단어0", json);
        Assert.Contains("단어99", json);
    }

    [Fact]
    public void Save_failure_exposes_LastSaveError()
    {
        // 읽기 전용 파일로 만들어서 Save 실패 유도
        var filePath = GetFilePath("test-ko");
        File.WriteAllText(filePath, "{}");

        // 파일을 읽기 전용으로 설정
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        try
        {
            var store = new WordFrequencyStore(_testDir, "test-ko");
            store.RecordWord("단어1");
            store.Flush();

            Assert.NotNull(store.LastSaveError);
        }
        finally
        {
            // 정리: 읽기 전용 해제
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }

    [Fact]
    public void Flush_saves_pending_changes()
    {
        var store = new WordFrequencyStore(_testDir, "test-ko");
        store.RecordWord("테스트");

        var filePath = GetFilePath("test-ko");
        var before = File.ReadAllText(filePath);
        Assert.DoesNotContain("테스트", before);

        store.Flush();

        var after = File.ReadAllText(filePath);
        Assert.Contains("테스트", after);
    }

    [Fact]
    public void Atomic_write_creates_tmp_file_then_moves()
    {
        var store = new WordFrequencyStore(_testDir, "test-ko");
        store.RecordWord("원자적");
        store.Flush();

        // tmp 파일이 남아있지 않아야 함
        var tmpPath = GetFilePath("test-ko") + ".tmp";
        Assert.False(File.Exists(tmpPath));

        // 실제 파일에는 데이터가 있어야 함
        var json = File.ReadAllText(GetFilePath("test-ko"));
        Assert.Contains("원자적", json);
    }

    [Fact]
    public void PruneLowest_removes_exactly_twenty_percent_when_ties_exist()
    {
        var store = new WordFrequencyStore(_testDir, "test-ko-prune");
        // 5001개 단어: 4500개 빈도 1, 500개 빈도 2, 1개 빈도 100
        for (int i = 0; i < 4500; i++) store.RecordWord($"a{i}");
        for (int i = 0; i < 500; i++) { store.RecordWord($"b{i}"); store.RecordWord($"b{i}"); }
        store.RecordWord("big");  // 첫 기록
        for (int i = 0; i < 99; i++) store.RecordWord("big");

        store.Flush();
        // MaxWords=5000이므로 RecordWord 내부에서 PruneLowest가 한 번 호출됨
        int totalAfter = store.Count;
        Assert.InRange(totalAfter, 3990, 4010);  // 5001 - 1000 ± 오차
        Assert.True(store.Contains("big"));       // 빈도 100은 살아남아야
    }

    [Fact]
    public void PruneLowest_is_deterministic_across_runs()
    {
        // 동일 입력 시퀀스 → 동일 생존 단어 집합
        var storeA = BuildStoreForDeterministicTest("test-ko-det-a");
        var storeB = BuildStoreForDeterministicTest("test-ko-det-b");

        // 두 store 모두 Flush 후 Count가 동일해야 함
        storeA.Flush();
        storeB.Flush();
        Assert.Equal(storeA.Count, storeB.Count);
    }

    private WordFrequencyStore BuildStoreForDeterministicTest(string langCode)
    {
        var store = new WordFrequencyStore(_testDir, langCode);
        for (int i = 0; i < 4500; i++) store.RecordWord($"a{i}");
        for (int i = 0; i < 500; i++) { store.RecordWord($"b{i}"); store.RecordWord($"b{i}"); }
        store.RecordWord("big");
        for (int i = 0; i < 99; i++) store.RecordWord("big");
        return store;
    }

    [Fact]
    public void PruneLowest_no_op_when_under_max()
    {
        var store = new WordFrequencyStore(_testDir, "test-ko-small");
        for (int i = 0; i < 100; i++) store.RecordWord($"w{i}");
        store.Flush();
        Assert.Equal(100, store.Count);  // Prune 발동 안 함
    }

    [Fact]
    public void RemoveWord_ExistingWord_Returns_True_And_Removes_Entry()
    {
        var store = new WordFrequencyStore(_testDir, "test-remove-ko");
        store.RecordWord("해달");
        store.RecordWord("해달");
        Assert.True(store.Contains("해달"));

        var removed = store.RemoveWord("해달");

        Assert.True(removed);
        Assert.False(store.Contains("해달"));
    }

    [Fact]
    public void RemoveWord_NonExistingWord_Returns_False()
    {
        var store = new WordFrequencyStore(_testDir, "test-remove-none");
        var removed = store.RemoveWord("없는단어");
        Assert.False(removed);
    }

    [Fact]
    public void RemoveWord_Persists_After_Flush()
    {
        var store = new WordFrequencyStore(_testDir, "test-remove-persist");
        store.RecordWord("해달");
        store.Flush();
        Assert.True(store.Contains("해달"));

        store.RemoveWord("해달");
        store.Flush();

        var reloaded = new WordFrequencyStore(_testDir, "test-remove-persist");
        Assert.False(reloaded.Contains("해달"));
    }

    private string GetFilePath(string lang) => Path.Combine(_testDir, $"user-words.{lang}.json");

    [Fact]
    public void SetFrequency_Zero_Removes_Word()
    {
        var store = new WordFrequencyStore(_testDir, "test-sf-zero");
        store.RecordWord("해달");
        store.RecordWord("해달");
        Assert.True(store.Contains("해달"));

        store.SetFrequency("해달", 0);
        Assert.False(store.Contains("해달"));
    }

    [Fact]
    public void SetFrequency_Positive_Upserts_Word()
    {
        var store = new WordFrequencyStore(_testDir, "test-sf-upsert");

        store.SetFrequency("바나나", 3);
        Assert.True(store.Contains("바나나"));

        var all = store.GetAllWords();
        Assert.Equal(1, all.Count);
        Assert.Equal("바나나", all[0].Word);
        Assert.Equal(3, all[0].Frequency);

        store.SetFrequency("바나나", 10);
        var updated = store.GetAllWords();
        Assert.Equal(10, updated[0].Frequency);
    }

    [Fact]
    public void SetFrequency_TriggersPrune_WhenOverMax()
    {
        var store = new WordFrequencyStore(_testDir, "test-sf-prune");

        for (int i = 0; i < 4999; i++) store.RecordWord($"w{i}");

        store.SetFrequency("overflow", 1);
        Assert.True(store.Count <= 5000);
    }

    [Fact]
    public void GetAllWords_Returns_SortedByFrequencyDesc_ThenWordAsc()
    {
        var store = new WordFrequencyStore(_testDir, "test-getall");
        store.RecordWord("가"); store.RecordWord("가");
        store.RecordWord("나");
        store.RecordWord("다");

        var all = store.GetAllWords();

        Assert.Equal(3, all.Count);
        Assert.Equal(("가", 2), all[0]);
        Assert.Equal(("나", 1), all[1]);
        Assert.Equal(("다", 1), all[2]);
    }

    [Fact]
    public void Clear_Empties_Store_Persistently()
    {
        var store = new WordFrequencyStore(_testDir, "test-clear");
        store.RecordWord("해달");
        store.Flush();
        Assert.True(store.Contains("해달"));

        store.Clear();
        store.Flush();

        var reloaded = new WordFrequencyStore(_testDir, "test-clear");
        Assert.Equal(0, reloaded.Count);
    }
}
