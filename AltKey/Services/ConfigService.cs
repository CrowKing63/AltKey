using System.IO;
using System.Text.Json;
using System.Threading;
using AltKey.Models;

namespace AltKey.Services;

public class ConfigService
{
    public AppConfig Current { get; private set; } = new();

    /// <summary>설정 변경 시 발생하는 이벤트</summary>
    public event Action<string?>? ConfigChanged; // null = 전체, 아니면 특정 속성명

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

    public void Save()
    {
        const int maxRetries = 3;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                File.WriteAllText(PathResolver.ConfigPath,
                    JsonSerializer.Serialize(Current, JsonOptions.Default));
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(300); // 파일 잠금 해제 대기
            }
        }
    }

    public void Update(Action<AppConfig> updater)
    {
        updater(Current);
        Save();
        ConfigChanged?.Invoke(null);
    }
}
