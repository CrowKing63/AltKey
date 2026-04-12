using System;
using System.IO;
using System.Text.Json;
using AltKey.Models;

namespace AltKey.Services;

public class ConfigService
{
    private readonly string _configPath;
    private AppConfig _config = new();

    public AppConfig Current => _config;

    public ConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AltKey");
        Directory.CreateDirectory(dir);
        _configPath = Path.Combine(dir, "config.json");
        Load();
    }

    private void Load()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                if (loaded != null)
                {
                    _config = loaded;
                }
            }
            catch
            {
                _config = new AppConfig();
            }
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_configPath, json);
    }

    public void Update(Action<AppConfig> mutator)
    {
        mutator(_config);
        Save();
    }
}
