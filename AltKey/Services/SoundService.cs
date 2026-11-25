using System.IO;
using System.Media;
using System.Windows.Media;

namespace AltKey.Services;

/// <summary>
/// [역할] 키보드 버튼을 클릭했을 때 소리를 재생하는 서비스입니다.
/// [기능] 설정에 따라 효과음을 켜거나 끄고, 사용자가 지정한 WAV 파일을 재생합니다.
/// [폴백] 미디어 엔진(Media Foundation)이 일부 환경(가상머신·샌드박스 등)에서
///        파일을 열지 못하면 WinMM 기반 백업 플레이어로 자동 전환해 키 소리가 나오도록 합니다.
/// </summary>
public class SoundService : IDisposable
{
    private MediaPlayer? _player;      // 기본 플레이어 (볼륨 조절 지원)
    private SoundPlayer? _fallback;    // 백업 플레이어 (미디어 엔진 실패 시 사용)
    private bool _enabled;
    private bool _mediaBroken;         // 기본 플레이어의 파일 열기 실패 여부
    private double _volume;            // 기본 플레이어의 볼륨(0.0~1.0) — 미리듣기 플레이어에 그대로 적용
    private string? _resolvedPath;

    /// <summary>
    /// 사운드 재생 환경을 설정합니다. (켜기/끄기, 파일 경로 지정, 크기 조절)
    /// volumePercent: 0(무음)~100(전체). 기본값 100입니다.
    /// preset: 키 사운드 스타일 — 1=또렷(기본), 2=균형, 3=빠른 스냅, 4=타자기, 5=타자기(깊은 타건), 6=옛날 레트로, 7=맥 레트로(번들된 Assets/Sounds/{preset}.wav),
    ///         0=사용자 지정 파일(customPath) 사용.
    /// </summary>
    public void Configure(bool enabled, string? customPath, int volumePercent = 100, int preset = 1)
    {
        _enabled = enabled;
        _mediaBroken = false;

        DisposePlayers();

        if (!enabled) return;

        var path = ResolvePath(preset, customPath);
        if (path is null) return;
        _resolvedPath = path;

        // MediaPlayer의 Volume 범위는 0.0~1.0이므로, 퍼센트를 0~1으로 바꿉니다.
        _volume = Math.Clamp(volumePercent, 0, 100) / 100.0;
        _player = new MediaPlayer { Volume = _volume };
        // 재생 준비(MediaOpened)가 되면 위치를 0으로 초기화하여 즉시 재생 가능하게 합니다.
        _player.MediaOpened += (_, _) => _player.Position = TimeSpan.Zero;
        // 미디어 엔진이 파일을 못 여는 환경(가상머신·샌드박스 등)에서 백업 플레이어로 전환합니다.
        _player.MediaFailed += (_, _) => _mediaBroken = true;

        try
        {
            _player.Open(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            // Open이 바로 예외를 던지는(동기 실패) 경우에도 미디어 엔진 실패로 봅니다.
            _mediaBroken = true;
        }
    }

    /// <summary>
    /// 실제 재생할 사운드 파일의 경로를 결정합니다. 우선순위:
    /// 1. 사용자가 직접 지정한 파일 — 기존 설정(사용자 WAV)이 새로 도입된 기본 스타일에 덮어쓰이지 않도록 최우선
    /// 2. 번들된 스타일 프리셋 Assets/Sounds/{preset}.wav (1=또렷(기본), 2=균형, 3=빠른 스냅, 4=타자기, 5=타자기(깊은 타건), 6=옛날 레트로, 7=맥 레트로)
    /// 둘 다 없으면 null(무음)을 반환합니다.
    /// </summary>
    internal static string? ResolvePath(int preset, string? customPath)
    {
        // 1) 사용자 지정 파일
        if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            return customPath;

        // 2) 번들된 스타일 프리셋 (출처·라이선스는 Assets/Sounds/README.txt)
        if (preset is >= 1 and <= 7)
        {
            var presetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", $"{preset}.wav");
            if (File.Exists(presetPath)) return presetPath;
        }

        return null;
    }

    /// <summary>
    /// 효과음을 1회 재생합니다.
    /// 기본 플레이어(볼륨 조절 지원)를 사용하고, 미디어 엔진 실패 시 WinMM 백업 플레이어로 전환합니다.
    /// 참고: 백업 플레이어 경로는 볼륨 설정이 적용되지 않습니다(재생 자체는 됩니다).
    /// </summary>
    public void Play()
    {
        if (!_enabled || _resolvedPath is null) return;

        if (_player is not null && !_mediaBroken)
        {
            _player.Position = TimeSpan.Zero; // 처음부터 재생
            _player.Play();
            return;
        }

        try
        {
            _fallback ??= new SoundPlayer(_resolvedPath);
            _fallback.Play();
        }
        catch
        {
            // 백업 플레이어조차 만들 수 없으면(예: 파일 손상) 재생을 포기합니다.
        }
    }

    /// <summary>
    /// 설정 화면에서 스타일/파일을 바꿀 때 새 소리를 즉시 한 번 들어보도록 재생합니다.
    /// 실제 키 입력에 쓰는 _player(기본 플레이어)와 상태를 공유하지 않는 독립된 1회용 플레이어를 만들고,
    /// 미디어가 열리는 순간에 재생합니다 — Open() 직후 바로 Play()를 걸어도 무시될 수 있기 때문에
    /// 이 순서가 가장 확실합니다. 미디어 엔진이 못 열 경우 WinMM 백업 플레이어로 한 번 재생합니다.
    /// </summary>
    public void Preview()
    {
        if (!_enabled || _resolvedPath is null) return;
        var path = _resolvedPath;

        try
        {
            var played = false; // 중복 재생 방지(미디어 실패가 연속 발생하는 환경 대비)
            var player = new MediaPlayer { Volume = _volume };
            player.MediaOpened += (_, _) =>
            {
                played = true;
                try
                {
                    player.Position = TimeSpan.Zero;
                    player.Play();
                }
                catch
                {
                    // 재생 실패는 무시합니다(설정 미리듣기라 화면은 그대로 유지).
                }
            };
            player.MediaFailed += (_, _) =>
            {
                if (played) return;
                try { new SoundPlayer(path).Play(); } catch { }
            };
            player.MediaEnded += (_, _) => player.Close();
            player.Open(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            // 미디어 엔진이 바로 예외를 던지는 경우 백업 플레이어로 대체합니다.
            try { new SoundPlayer(path).Play(); } catch { }
        }
    }

    private void DisposePlayers()
    {
        _player?.Stop();
        _player?.Close();
        _player = null;
        _fallback?.Dispose();
        _fallback = null;
        _resolvedPath = null;
    }

    public void Dispose() => DisposePlayers();
}
