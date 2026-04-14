using System.IO;
using System.Windows.Media;

namespace AltKey.Services;

public class SoundService : IDisposable
{
    private MediaPlayer? _player;
    private bool _enabled;

    public void Configure(bool enabled, string? customPath)
    {
        _enabled = enabled;

        _player?.Stop();
        _player?.Close();
        _player = null;

        if (!enabled) return;

        var path = ResolvePath(customPath);
        if (path is null) return;

        _player = new MediaPlayer { Volume = 1.0 };
        _player.Open(new Uri(path, UriKind.Absolute));
        // 첫 재생 지연 최소화를 위해 미리 Position을 초기화
        _player.MediaOpened += (_, _) => _player.Position = TimeSpan.Zero;
    }

    /// <summary>
    /// 사용할 사운드 파일 경로를 결정한다.
    /// 우선순위: 사용자 지정 파일 → Assets/Sounds/click.wav → Assets/Sounds/ 내 첫 번째 WAV
    /// </summary>
    private static string? ResolvePath(string? customPath)
    {
        // 1. 사용자가 직접 지정한 파일
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            return customPath;

        // 2. 기본 파일: Assets/Sounds/click.wav
        var soundsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
        var clickPath = Path.Combine(soundsDir, "click.wav");
        if (File.Exists(clickPath)) return clickPath;

        // 3. 폴백: Assets/Sounds/ 폴더 내 첫 번째 WAV 파일
        if (Directory.Exists(soundsDir))
        {
            var first = Directory.GetFiles(soundsDir, "*.wav").FirstOrDefault();
            if (first is not null) return first;
        }

        return null;
    }

    public void Play()
    {
        if (!_enabled || _player == null) return;
        _player.Position = TimeSpan.Zero;
        _player.Play();
    }

    public void Dispose()
    {
        _player?.Stop();
        _player?.Close();
        _player = null;
    }
}
