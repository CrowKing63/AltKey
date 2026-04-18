using System.IO;
using System.Text.Json;
using Xunit;
using AltKey.Services;

namespace AltKey.Tests.Services;

public class BigramFrequencyStoreTests : IDisposable
{
    private readonly string _tempDir;

    public BigramFrequencyStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "altkey-bigram-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* 베스트 에포트 */ }
    }

    private BigramFrequencyStore NewStore(string lang = "ko")
        => new(_tempDir, lang);

    [Fact]
    public void Record_new_pair_increments_count_to_1()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        Assert.Equal(1, store.Count);
        Assert.True(store.Contains("안녕", "하세요"));
    }

    [Fact]
    public void Record_same_pair_twice_has_count_2()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        store.Record("안녕", "하세요");
        Assert.Equal(1, store.Count); // 쌍은 하나
        var nexts = store.GetNexts("안녕", "하", count: 10);
        Assert.Single(nexts);
        Assert.Equal(2, nexts[0].Count);
    }

    [Fact]
    public void Record_null_or_whitespace_is_noop()
    {
        var store = NewStore();
        store.Record("", "하세요");
        store.Record("안녕", "");
        store.Record("  ", "하세요");
        store.Record("안녕", "  ");
        store.Record(null!, "하세요");
        store.Record("안녕", null!);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void GetNexts_filters_by_prefix_and_orders_by_count_desc()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        store.Record("안녕", "하세요");
        store.Record("안녕", "하세요");
        store.Record("안녕", "하루");
        store.Record("안녕", "하늘");

        var nexts = store.GetNexts("안녕", "하", count: 10);
        Assert.Equal(3, nexts.Count);
        Assert.Equal("하세요", nexts[0].Next);
        Assert.Equal(3, nexts[0].Count);
        // "하루"와 "하늘"은 빈도가 같으므로 알파벳순
        Assert.Equal("하늘", nexts[1].Next);
        Assert.Equal("하루", nexts[2].Next);
    }

    [Fact]
    public void GetNexts_empty_prefix_returns_top_N()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        store.Record("안녕", "하세요");
        store.Record("안녕", "해");
        store.Record("안녕", "한");

        var nexts = store.GetNexts("안녕", "", count: 2);
        Assert.Equal(2, nexts.Count);
        Assert.Equal("하세요", nexts[0].Next);
        Assert.Equal("한", nexts[1].Next);
    }

    [Fact]
    public void GetNexts_choseong_jamo_prefix_matches_by_initial_consonant()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        store.Record("안녕", "하니");
        store.Record("안녕", "해요");
        // ㅎ 초성으로 검색
        var nexts = store.GetNexts("안녕", "ㅎ", count: 10);
        Assert.Equal(3, nexts.Count);
        Assert.Contains(nexts, n => n.Next == "하세요");
        Assert.Contains(nexts, n => n.Next == "하니");
        Assert.Contains(nexts, n => n.Next == "해요");
    }

    [Fact]
    public void GetNexts_with_unknown_prev_returns_empty()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        var nexts = store.GetNexts("없는", "하", count: 10);
        Assert.Empty(nexts);
    }

    [Fact]
    public void RemovePair_removes_only_target_and_cleans_empty_prev()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        store.Record("안녕", "해");
        Assert.True(store.Contains("안녕", "하세요"));
        Assert.True(store.Contains("안녕", "해"));

        // 한 쌍만 제거
        Assert.True(store.RemovePair("안녕", "하세요"));
        Assert.False(store.Contains("안녕", "하세요"));
        Assert.True(store.Contains("안녕", "해"));
        Assert.Equal(1, store.NextCountFor("안녕"));

        // 마지막 쌍 제거 → prev 키도 정리
        Assert.True(store.RemovePair("안녕", "해"));
        Assert.Equal(0, store.NextCountFor("안녕"));
    }

    [Fact]
    public void RemoveAllFor_removes_all_nexts_of_prev()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        store.Record("안녕", "해");
        store.Record("안녕", "한");

        int removed = store.RemoveAllFor("안녕");
        Assert.Equal(3, removed);
        Assert.Equal(0, store.NextCountFor("안녕"));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Clear_empties_store()
    {
        var store = NewStore();
        store.Record("안녕", "하세요");
        store.Record("감사", "합니다");
        Assert.Equal(2, store.Count);

        store.Clear();
        Assert.Equal(0, store.Count);
        Assert.Empty(store.GetAllPairs());
    }

    [Fact]
    public void GetAllPairs_snapshot_is_sorted_prev_asc_then_count_desc()
    {
        var store = NewStore();
        store.Record("감사", "합니다");
        store.Record("감사", "합니다");
        store.Record("감사", "해요");
        store.Record("안녕", "하세요");
        store.Record("안녕", "해");

        var pairs = store.GetAllPairs();
        Assert.Equal(4, pairs.Count);
        // prev 오름차순: 감사 < 안녕
        Assert.Equal("감사", pairs[0].Prev);
        Assert.Equal("감사", pairs[1].Prev);
        Assert.Equal("안녕", pairs[2].Prev);
        Assert.Equal("안녕", pairs[3].Prev);
        // 같은 prev 내에서 count 내림차순
        Assert.Equal("합니다", pairs[0].Next);
        Assert.Equal(2, pairs[0].Count);
        Assert.Equal("해요", pairs[1].Next);
        Assert.Equal(1, pairs[1].Count);
    }

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
    public void Reload_from_disk_round_trips_all_pairs()
    {
        var store1 = NewStore("ko");
        store1.Record("안녕", "하세요");
        store1.Record("감사", "합니다");
        store1.Flush();

        // 새 인스턴스로 로드
        var store2 = NewStore("ko");
        Assert.Equal(2, store2.Count);
        Assert.True(store2.Contains("안녕", "하세요"));
        Assert.True(store2.Contains("감사", "합니다"));
    }

    [Fact]
    public void PerPrev_pruning_limits_nexts_below_cap()
    {
        var store = NewStore();
        // MaxNextPerPrev = 50이므로 51개 next 기록
        for (int i = 0; i < 51; i++)
            store.Record("안녕", $"next{i:D3}");

        // 프루닝 후 next 개수는 50 이하
        int nextCount = store.NextCountFor("안녕");
        Assert.True(nextCount <= 50);
    }

    private string GetFilePath(string lang) => Path.Combine(_tempDir, $"user-bigrams.{lang}.json");
}