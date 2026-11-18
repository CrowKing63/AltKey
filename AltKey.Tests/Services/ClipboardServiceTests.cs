using System.Globalization;
using System.IO;
using AltKey.Services;
using Xunit;

namespace AltKey.Tests.Services;

/// <summary>
/// ClipboardService — 저장 파일 로드/저장, 중복·상위 이동, 핀(MRU) 관리, 타임스탬프(상대 시간) 검증.
/// DataDir을 임시 폴더로 바꿔 실제 사용자 데이터를 건드리지 않습니다 (순차 실행 전제).
/// </summary>
public class ClipboardServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _previousDataDir;

    public ClipboardServiceTests()
    {
        // 현재 유효 DataDir을 기억해 Dispose에서 복원 — OverrideDataDir은 전역이라.
        _previousDataDir = PathResolver.DataDir;
        _tempDir = Directory.CreateTempSubdirectory("altkey-clip-test").FullName;
        PathResolver.OverrideDataDir(_tempDir);
    }

    public void Dispose()
    {
        PathResolver.OverrideDataDir(_previousDataDir);
        try { Directory.Delete(_tempDir, true); } catch { /* 임시 폴더 정리 실패는 무시 */ }
    }

    private static string SavePath => Path.Combine(PathResolver.DataDir, "clipboard_history.json");

    private ClipboardService NewService(string? json = null)
    {
        if (json != null) File.WriteAllText(SavePath, json);
        return new ClipboardService();
    }

    // ── 로드 ────────────────────────────────────────────────────────

    [Fact]
    public void 저장_파일에서_히스토리와_즐겨찾기_로드()
    {
        using var svc = NewService("""
            {"History":["a","b"],"Favorites":["b"],"Timestamps":{"a":"2026-11-17T10:00:00.0000000"}}
            """);

        Assert.Equal(["a", "b"], svc.History);
        Assert.Equal(["b"], svc.Favorites);
        Assert.True(svc.IsFavorite("b"));
    }

    [Fact]
    public void 구형_파일_타임스탬프_없으면_null()
    {
        using var svc = NewService("""{"History":["a"],"Favorites":[]}""");

        // Timestamps 자체가 없는 구형 파일 — CopiedAt은 null (패널은 시간 라벨을 숨김).
        Assert.Null(svc.CopiedAt("a"));
    }

    [Fact]
    public void 저장_파일_없으면_빈_시작()
    {
        using var svc = NewService();

        Assert.Empty(svc.History);
        Assert.Empty(svc.Favorites);
    }

    // ── 타임스탬프 (상대 시간) ────────────────────────────────────────

    [Fact]
    public void 복사_시각이_로드되고_갱신된다()
    {
        using var svc = NewService("""
            {"History":["a","b"],"Favorites":[],"Timestamps":{"a":"2026-11-17T10:00:00.0000000"}}
            """);

        var aTime = svc.CopiedAt("a").Value;
        Assert.Equal(new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Unspecified), aTime);

        // 상위 이동 시 "방금"으로 갱신된다.
        svc.PromoteItem("b");
        var bTime = svc.CopiedAt("b").Value;
        Assert.True(bTime > aTime, $"b({bTime})가 a({aTime})보다 늦어야 함");
    }

    // ── 히스토리 조작 ────────────────────────────────────────────────

    [Fact]
    public void 상위_이동은_맨_앞으로_옮긴다()
    {
        using var svc = NewService("""{"History":["a","b","c"],"Favorites":[]}""");

        svc.PromoteItem("c");
        Assert.Equal(["c", "a", "b"], svc.History);
    }

    [Fact]
    public void 항목_삭제는_타임스탬프까지_지운다()
    {
        using var svc = NewService("""{"History":["a","b"],"Favorites":[],"Timestamps":{"a":"2026-11-17T10:00:00.0000000"}}""");

        svc.RemoveFromHistory("a");
        Assert.Equal(["b"], svc.History);
        Assert.Null(svc.CopiedAt("a"));
    }

    [Fact]
    public void 히스토리_전체_삭제는_즐겨찾기를_보존한다()
    {
        using var svc = NewService("""{"History":["a","b"],"Favorites":["b"]}""");

        svc.ClearHistory();
        Assert.Empty(svc.History);
        Assert.Equal(["b"], svc.Favorites); // Win+V "히스토리 지우기" 시맨틱 — 고정 항목은 남음
    }

    [Fact]
    public void 전체_초기화는_모두_지운다()
    {
        using var svc = NewService("""{"History":["a"],"Favorites":["b"]}""");

        svc.ClearAll();
        Assert.Empty(svc.History);
        Assert.Empty(svc.Favorites);
    }

    // ── 즐겨찾기 ─────────────────────────────────────────────────────

    [Fact]
    public void 즐겨찾기_토글_추가_제거()
    {
        using var svc = NewService("""{"History":["a","b"],"Favorites":[]}""");

        svc.ToggleFavorite("a");
        Assert.True(svc.IsFavorite("a"));

        svc.ToggleFavorite("a");
        Assert.False(svc.IsFavorite("a"));
    }

    [Fact]
    public void 핀_개수_제한이_없다()
    {
        // [2026-11] 한도(10) 제거 — 핀은 칩 스트립에서 가로 스크롤로 늘어나므로 전부 유지된다.
        var favorites = string.Join(",", Enumerable.Range(1, 12).Select(i => $"\u0022f{i}\u0022"));
        using var svc = NewService($"{{\"History\":[],\"Favorites\":[{favorites}]}}");

        Assert.Equal(12, svc.Favorites.Count);

        svc.ToggleFavorite("new");
        Assert.Equal(13, svc.Favorites.Count);
        Assert.True(svc.IsFavorite("f1"), "가장 오래된 항목도 유지됨");
    }

    [Fact]
    public void 핀_상위_이동은_맨_앞으로_옮긴다()
    {
        // [2026-11] MRU — 붙여넣기(PasteItem)가 PromoteFavorite를 호출해 최근 사용 핀이 첫 칩에 온다.
        using var svc = NewService("""{"History":["a","b","c"],"Favorites":["a","b","c"]}""");

        svc.PromoteFavorite("c");
        Assert.Equal(["c", "a", "b"], svc.Favorites);

        // 즐겨찾기에 없는 항목은 무시 (no-op).
        svc.PromoteFavorite("x");
        Assert.Equal(["c", "a", "b"], svc.Favorites);
    }

    [Fact]
    public void 핀_제거해도_히스토리_순서는_그대로다()
    {
        // 칩 ✕ = 핀 해제 — 항목은 최근 리스트의 원래 위치로 복귀해야 한다 (히스토리 순서 불변).
        using var svc = NewService("""{"History":["a","b","c"],"Favorites":["b"]}""");

        svc.RemoveFavorite("b");
        Assert.Equal(["a", "b", "c"], svc.History);
        Assert.False(svc.IsFavorite("b"));
    }

    // ── 저장 ─────────────────────────────────────────────────────────

    [Fact]
    public void 변경_후_즉시_파일에_저장된다()
    {
        using var svc = NewService("""{"History":["a","b"],"Favorites":[]}""");

        svc.PromoteItem("b");

        // 저장 파일에 새 순서가 반영되어야 재기동 후에도 유지된다.
        Assert.True(File.Exists(SavePath));
        var json = File.ReadAllText(SavePath);
        int bIdx = json.IndexOf("\"b\"", StringComparison.Ordinal);
        int aIdx = json.IndexOf("\"a\"", StringComparison.Ordinal);
        Assert.True(bIdx < aIdx, $"파일에서 b({bIdx})가 a({aIdx}) 앞에 와야 함");
    }
}
