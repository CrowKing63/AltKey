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

    private string GetFilePath(string lang) => Path.Combine(_testDir, $"user-words.{lang}.json");
}
