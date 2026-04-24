using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// [역할] 앱의 설정(config.json)을 로드하고 저장하며, 변경 사항을 관리하는 서비스입니다.
/// [기능] 파일 읽기/쓰기, 설정값 업데이트 알림, 이전 버전 설정 파일의 호환성 유지(Migration)를 처리합니다.
/// </summary>
public class ConfigService
{
    /// 현재 앱에 적용된 모든 설정 데이터입니다.
    public AppConfig Current { get; private set; } = new();

    /// <summary>설정 값이 변경되었을 때 발생하는 이벤트입니다.</summary>
    /// <param name="propertyName">변경된 속성의 이름입니다. 전체가 바뀌었으면 null입니다.</param>
    public event Action<string?>? ConfigChanged;

    public ConfigService()
    {
        // 설정 파일이 저장될 폴더가 없다면 생성합니다.
        Directory.CreateDirectory(Path.GetDirectoryName(PathResolver.ConfigPath)!);
        Load();
    }

    /// <summary>
    /// config.json 파일에서 설정을 불러옵니다. 파일이 없으면 기본 설정으로 파일을 생성합니다.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(PathResolver.ConfigPath)) { Save(); return; }
        try
        {
            var json = File.ReadAllText(PathResolver.ConfigPath);
            Current = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default) ?? new();
            MigrateWindowConfig(json); // 예전 버전의 설정 형식을 최신 형식으로 변환합니다.
        }
        catch
        {
            Current = new AppConfig();
        }
    }

    /// <summary>
    /// 예전 버전(Width/Height 기반)의 설정을 최신 버전(배율 Scale 기반)으로 변환해주는 도우미 함수입니다.
    /// </summary>
    private void MigrateWindowConfig(string json)
    {
        try
        {
            var node = JsonNode.Parse(json)?["Window"]?.AsObject();
            if (node == null) return;
            if (node.ContainsKey("Scale")) return;

            if (node.TryGetPropertyValue("Width", out var widthNode) && widthNode != null)
            {
                var width = (double)widthNode;
                var scale = (int)Math.Round(width / 900.0 * 100);
                Current.Window.Scale = Math.Clamp(scale, 60, 200);
            }
        }
        catch { }
    }

    /// <summary>
    /// 현재 설정 내용을 config.json 파일에 물리적으로 저장합니다.
    /// 다른 프로세스가 파일을 사용 중일 수 있으므로 실패 시 몇 번 재시도합니다.
    /// </summary>
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
                Thread.Sleep(300); // 잠시 기다린 후 재시도
            }
        }
    }

    /// <summary>
    /// 설정을 안전하게 변경하고 즉시 파일에 저장하며, 변경되었음을 다른 컴포넌트들에 알립니다.
    /// </summary>
    /// <param name="updater">설정 값을 수정하는 함수</param>
    /// <param name="propertyName">수정한 속성 이름</param>
    public void Update(Action<AppConfig> updater, string? propertyName = null)
    {
        updater(Current);
        Save();
        ConfigChanged?.Invoke(propertyName);
    }
}
