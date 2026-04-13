using System.IO;
using System.Text.Json;
using AltKey.Models;

namespace AltKey.Services;

public class ConfigService
{
    public AppConfig Current { get; private set; } = new();

    public ConfigService()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathResolver.ConfigPath)!);
        Load();
    }

    public void Load()
    {
        if (!File.Exists(PathResolver.ConfigPath)) { Save(); return; }
        try
        {
            var json = File.ReadAllText(PathResolver.ConfigPath);
            Current = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default) ?? new();
        }
        catch
        {
            Current = new AppConfig();
        }
    }

    public void Save() =>
        File.WriteAllText(PathResolver.ConfigPath,
            JsonSerializer.Serialize(Current, JsonOptions.Default));

    public void Update(Action<AppConfig> updater)
    {
        updater(Current);
        Save();
    }
}
