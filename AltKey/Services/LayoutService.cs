using System.IO;
using System.Text.Json;
using AltKey.Models;

namespace AltKey.Services;

public class LayoutService
{
    private readonly string _layoutsDir;
    private readonly Dictionary<string, LayoutConfig> _cache = [];

    public LayoutService() : this(PathResolver.LayoutsDir) { }

    protected LayoutService(string layoutsDir)
    {
        _layoutsDir = layoutsDir;
        Directory.CreateDirectory(_layoutsDir);
    }

    public IReadOnlyList<string> GetAvailableLayouts()
    {
        return Directory.GetFiles(_layoutsDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList();
    }

    public LayoutConfig Load(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;

        var path = Path.Combine(_layoutsDir, $"{name}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"레이아웃 파일 없음: {path}");

        var json = File.ReadAllText(path);
        var layout = JsonSerializer.Deserialize<LayoutConfig>(json, JsonOptions.Default)
            ?? throw new InvalidDataException($"레이아웃 파싱 실패: {name}");

        _cache[name] = layout;
        return layout;
    }

    /// <summary>config 변경 시 캐시 무효화</summary>
    public void InvalidateCache() => _cache.Clear();
}
