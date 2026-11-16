using System.IO;
using System.Text.Json;
using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// [역할] 스크래치패드의 메모 내용을 저장하고 다시 불러오는 서비스입니다.
/// [기능] 타이핑을 디바운스(500ms)로 묶어 파일에 쓰고, 닫기·앱 종료 시 남은 내용을 즉시 저장합니다.
/// [참고] 손상된 JSON은 빈 내용으로 시작합니다 — ClipboardService와 같은 관용입니다.
/// </summary>
public class ScratchPadService : IDisposable
{
    private const string DefaultFileName = "scratchpad.json";

    // 파일 경로: 포터블 모드(exe 폴더)·설치형(%AppData%\AltKey) 모두 PathResolver로 통일됩니다.
    private readonly string _savePath;

    // 타이핑이 멈춘 뒤 이 시간(ms)이 지나면 파일을 실제로 씁니다 — 한 글자씩 쓰는 것을 막기 위함.
    private readonly int _debounceMs;

    private readonly object _sync = new();
    private System.Threading.Timer? _timer;
    private string? _pendingText;
    private bool _dirty;
    private bool _disposed;

    /// <param name="savePath">전체 경로. 기본값은 DataDir\scratchpad.json입니다 (테스트에서 임시 경로를 줌).</param>
    /// <param name="debounceMs">디바운스 시간(ms). 테스트에서 짧은 값을 줍니다.</param>
    public ScratchPadService(string? savePath = null, int debounceMs = 500)
    {
        _savePath = savePath ?? Path.Combine(PathResolver.DataDir, DefaultFileName);
        _debounceMs = Math.Max(debounceMs, 1);
    }

    /// <summary>
    /// 저장된 메모를 불러옵니다. 파일이 없거나 손상되면 빈 문자열을 반환합니다.
    /// </summary>
    public string Load()
    {
        if (!File.Exists(_savePath)) return "";
        try
        {
            var data = JsonSerializer.Deserialize<ScratchPadData>(File.ReadAllText(_savePath), JsonOptions.Default);
            return data?.Text ?? "";
        }
        catch
        {
            // 손상된 파일: 빈 상태로 시작 (다음 저장 때 덮어씀)
            return "";
        }
    }

    /// <summary>
    /// 타이핑이 멈추면 디바운스 시간 뒤에 파일을 씁니다. 호출을 거듭해도 마지막 값만 저장됩니다.
    /// </summary>
    public void ScheduleSave(string text)
    {
        lock (_sync)
        {
            if (_disposed) return;
            _pendingText = text;
            _dirty = true;

            if (_timer is null)
                _timer = new System.Threading.Timer(OnDebounceFired, null, _debounceMs, Timeout.Infinite);
            else
                _timer.Change(_debounceMs, Timeout.Infinite);
        }
    }

    /// <summary>
    /// 대기 중인 디바운스를 멈추고, 아직 저장되지 않은 내용이 있으면 즉시 파일에 씁니다.
    /// 닫기(✕)·앱 종료 경로에서 사용합니다. 저장할 변경이 없으면 아무것도 하지 않습니다.
    /// </summary>
    public void SaveNow()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            if (!_dirty || _pendingText is null) return;
            WriteFile(_pendingText);
        }
    }

    /// <summary>
    /// 디스포즈 시(앱 종료) 아직 저장되지 않은 내용이 있으면 즉시 씁니다.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            if (_dirty && _pendingText is not null)
                WriteFile(_pendingText);
            _disposed = true;
        }

        _timer?.Dispose();
    }

    // ── 사용자 설정 복원·초기화 연동 ─────────────────────────────────────

    /// <summary>
    /// 사용자 설정 초기화 시 호출 — 인메모리 미저장 내용을 버립니다.
    /// 파일은 이미 삭제된 상태라, 닫기·종료 시 저장(WriteFile)이 오래된 내용으로 파일을 다시 만들지 않도록 합니다.
    /// </summary>
    public void AbandonPending()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _pendingText = null;
            _dirty = false;
        }
    }

    /// <summary>
    /// 사용자 설정 복원 시 호출 — 미저장 내용을 디스크(복원된 파일) 기준으로 다시 읽습니다.
    /// 닫기·종료 시 저장이 복원 전의 오래된 내용으로 덮어쓰지 않도록 합니다.
    /// </summary>
    public void ReanchorFromDisk()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _pendingText = Load();
            _dirty = true;
        }
    }

    private void OnDebounceFired(object? state)
    {
        lock (_sync)
        {
            if (_disposed || !_dirty || _pendingText is null) return;
            WriteFile(_pendingText);
        }
    }

    // 파일 쓰기 실패(권한·디스크 등)는 조용히 넘깁니다 — 다음 타이핑 디바운스에서 재시도됩니다.
    private void WriteFile(string text)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_savePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var data = new ScratchPadData
            {
                Text = text,
                UpdatedAt = DateTime.Now
            };
            File.WriteAllText(_savePath, JsonSerializer.Serialize(data, JsonOptions.Default));

            lock (_sync)
            {
                _dirty = false;
            }
        }
        catch { /* 저장 실패는 무시 (다음 기회에 재시도) */ }
    }

    // 저장 파일 형식: { "text": "...", "updated_at": "2026-11-15T..." } (JsonOptions의 snake_case 규칙)
    private sealed class ScratchPadData
    {
        public string Text { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}
