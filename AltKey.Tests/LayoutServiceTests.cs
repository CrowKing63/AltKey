using System.Text.Json;
using AltKey.Models;
using AltKey.Services;

namespace AltKey.Tests;

/// <summary>
/// LayoutService 단위 테스트.
/// 실제 파일 시스템 사용 (임시 폴더), PathResolver 우회를 위해
/// <see cref="TestLayoutService"/> 헬퍼 서브클래스를 사용한다.
/// </summary>
public class LayoutServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TestLayoutService _service;

    public LayoutServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"AltKeyTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new TestLayoutService(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _opts = JsonOptions.Default;

    private string WriteLayout(string name, LayoutConfig config)
    {
        var path = Path.Combine(_tempDir, $"{name}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config, _opts));
        return path;
    }

    private static LayoutConfig MakeSimpleLayout(string name = "test") => new(
        Name: name,
        Language: "en",
        Rows:
        [
            new KeyRow([
                new KeySlot("A", null, new SendKeyAction("VK_A"), 1.0)
            ])
        ]
    );

    // ── 테스트 케이스 ──────────────────────────────────────────────────────────

    [Fact]
    public void Load_ExistingLayout_ReturnsLayoutConfig()
    {
        WriteLayout("simple", MakeSimpleLayout("simple"));

        var result = _service.Load("simple");

        Assert.Equal("simple", result.Name);
        Assert.Single(result.Rows);
        Assert.Single(result.Rows[0].Keys);
        Assert.Equal("A", result.Rows[0].Keys[0].Label);
    }

    [Fact]
    public void Load_SameName_ReturnsCachedInstance()
    {
        WriteLayout("cached", MakeSimpleLayout("cached"));

        var first  = _service.Load("cached");
        var second = _service.Load("cached");

        Assert.Same(first, second);
    }

    [Fact]
    public void Load_NonExistentFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => _service.Load("does-not-exist"));
    }

    [Fact]
    public void GetAvailableLayouts_ReturnsAllJsonFiles()
    {
        WriteLayout("layout-a", MakeSimpleLayout("layout-a"));
        WriteLayout("layout-b", MakeSimpleLayout("layout-b"));

        var layouts = _service.GetAvailableLayouts();

        Assert.Contains("layout-a", layouts);
        Assert.Contains("layout-b", layouts);
        Assert.Equal(2, layouts.Count);
    }

    [Fact]
    public void InvalidateCache_ForcesReload()
    {
        WriteLayout("reloadable", MakeSimpleLayout("reloadable"));

        var first = _service.Load("reloadable");
        _service.InvalidateCache();

        // 캐시 무효화 후 파일을 업데이트
        WriteLayout("reloadable", MakeSimpleLayout("reloadable-updated"));

        var second = _service.Load("reloadable");

        Assert.NotSame(first, second);
        Assert.Equal("reloadable-updated", second.Name);
    }

    // ── 내부 테스트용 서브클래스 ───────────────────────────────────────────────

    /// PathResolver를 우회하여 임시 폴더를 사용하는 LayoutService
    private sealed class TestLayoutService : LayoutService
    {
        public TestLayoutService(string dir) : base(dir) { }
    }
}
