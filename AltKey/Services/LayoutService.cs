using System.IO;
using System.Text.Json;
using AltKey.Models;

namespace AltKey.Services;

public class LayoutService
{
    private readonly string _layoutsDir;
    private readonly Dictionary<string, LayoutConfig> _cache = [];

    public event Action? LayoutsChanged;

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

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new InvalidDataException($"레이아웃 파일 읽기 실패: {name}", ex);
        }

        LayoutConfig layout;
        try
        {
            layout = JsonSerializer.Deserialize<LayoutConfig>(json, JsonOptions.Default)
                ?? throw new InvalidDataException($"레이아웃 파싱 결과 null: {name}");
        }
        catch (JsonException ex)
        {
            // T-6.7: 잘못된 JSON → 구체적인 예외 메시지로 래핑
            throw new InvalidDataException($"레이아웃 JSON 파싱 실패: {name} — {ex.Message}", ex);
        }

        _cache[name] = layout;
        return layout;
    }

    /// <summary>T-6.7: 레이아웃 로드를 시도하고 실패 시 null 반환 (UI에서 폴백 처리)</summary>
    public LayoutConfig? TryLoad(string name, Action<Exception>? onError = null)
    {
        try { return Load(name); }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
            return null;
        }
    }

    /// <summary>T-9.2: 레이아웃을 JSON 파일로 저장하고 캐시 무효화</summary>
    public void Save(string name, LayoutConfig config)
    {
        var path = Path.Combine(_layoutsDir, name + ".json");
        var json = JsonSerializer.Serialize(config, JsonOptions.Default);
        File.WriteAllText(path, json);
        InvalidateCache();
        LayoutsChanged?.Invoke();
    }

    /// <summary>레이아웃 파일 삭제</summary>
    public bool Delete(string name)
    {
        var path = Path.Combine(_layoutsDir, name + ".json");
        if (!File.Exists(path)) return false;
        File.Delete(path);
        InvalidateCache();
        LayoutsChanged?.Invoke();
        return true;
    }

    /// <summary>config 변경 시 캐시 무효화</summary>
    public void InvalidateCache() => _cache.Clear();
}
