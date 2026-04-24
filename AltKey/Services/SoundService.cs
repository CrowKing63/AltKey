using System.IO;
using System.Windows.Media;

namespace AltKey.Services;

/// <summary>
/// [역할] 키보드 버튼을 클릭했을 때 소리를 재생하는 서비스입니다.
/// [기능] 설정에 따라 효과음을 켜거나 끄고, 사용자가 지정한 WAV 파일을 재생합니다.
/// </summary>
public class SoundService : IDisposable
{
    private MediaPlayer? _player;
    private bool _enabled;

    /// <summary>
    /// 사운드 재생 환경을 설정합니다. (켜기/끄기 및 파일 경로 지정)
    /// </summary>
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
        // 재생 준비(MediaOpened)가 되면 위치를 0으로 초기화하여 즉시 재생 가능하게 합니다.
        _player.MediaOpened += (_, _) => _player.Position = TimeSpan.Zero;
    }

    /// <summary>
    /// 실제 재생할 사운드 파일의 경로를 결정합니다.
    /// 1. 사용자가 직접 지정한 파일
    /// 2. Assets/Sounds/click.wav (기본 효과음)
    /// 3. 해당 폴더 내의 아무 WAV 파일
    /// </summary>
    private static string? ResolvePath(string? customPath)
    {
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            return customPath;

        var soundsDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
        var clickPath = Path.Combine(soundsDir, "click.wav");
        if (File.Exists(clickPath)) return clickPath;

        if (Directory.Exists(soundsDir))
        {
            var first = Directory.GetFiles(soundsDir, "*.wav").FirstOrDefault();
            if (first is not null) return first;
        }

        return null;
    }

    /// <summary>
    /// 효과음을 1회 재생합니다.
    /// </summary>
    public void Play()
    {
        if (!_enabled || _player == null) return;
        _player.Position = TimeSpan.Zero; // 처음부터 재생
        _player.Play();
    }

    public void Dispose()
    {
        _player?.Stop();
        _player?.Close();
        _player = null;
    }
}
