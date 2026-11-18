using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// [역할] 최근에 복사한 글자(클립보드)들의 목록을 관리하고 다시 사용할 수 있게 돕는 서비스입니다.
/// [기능] 윈도우 클립보드를 주기적으로 감시하여 새로운 복사본을 기록하고, 최대 50개까지의 이력을 유지합니다.
/// [기능] 즐겨찾기 항목을 별도로 관리하며, 히스토리와 즐겨찾기는 프로그램 종료 후에도 저장됩니다.
/// </summary>
public class ClipboardService : IDisposable
{
    // ── 상수 ──────────────────────────────────────────────────────────────
    private const int MaxHistory = 50;   // 최근 항목 최대 개수

    // ── 이벤트 ────────────────────────────────────────────────────────────
    public event Action? HistoryChanged;
    public event Action? FavoritesChanged;

    // ── 속성 ──────────────────────────────────────────────────────────────
    // 최신 항목이 앞에 오는 리스트
    public IReadOnlyList<string> History => _history;
    public IReadOnlyList<string> Favorites => _favorites;

    // ── 필드 ──────────────────────────────────────────────────────────────
    private readonly List<string> _history = [];
    private readonly List<string> _favorites = [];
    // 텍스트 → 마지막 복사 시각 — 행의 상대 시간("N분 전") 표시용. 구형 저장 파일엔 없으므로 null 허용.
    private readonly Dictionary<string, DateTime> _timestamps = new();
    private readonly DispatcherTimer _pollTimer;
    // [변경 감지 기준] 클립보드 시퀀스 번호 — 텍스트 본문 비교 대신 사용한다.
    // 같은 텍스트를 다시 복사해도 시퀀스는 증가하므로 "전체 삭제 후 같은 내용 재복사"가 정상 추가된다.
    private uint _lastSeq;

    // 저장 파일 경로 (PathResolver.DataDir + clipboard_history.json)
    private static string SavePath => Path.Combine(PathResolver.DataDir, "clipboard_history.json");

    public ClipboardService()
    {
        // 저장된 히스토리와 즐겨찾기 불러오기
        Load();

        // [폴링 기준점] 시작 시점의 시퀀스 — AltKey 시작 전 클립보드 내용이 있는 그대로면 재추가하지 않는다.
        _lastSeq = ClipboardHelper.GetSequenceNumberWithRetry();

        // 클립보드 폴링 (500ms 간격)
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    // ── 클립보드 폴링 ─────────────────────────────────────────────────────

    private void OnPollTick(object? sender, EventArgs e)
    {
        var seq = ClipboardHelper.GetSequenceNumberWithRetry();
        if (seq == _lastSeq) return; // 클립보드 내용이 변한 적 없음
        _lastSeq = seq; // 변화 감지 — 텍스트가 아니어도 시퀀스 갱신으로 다음 폴링 정상화

        // 재시도 로직이 포함된 헬퍼 사용 (다른 프로그램이 클립보드를 점유하고 있어도 안전)
        if (!ClipboardHelper.ContainsTextWithRetry()) return;
        var current = ClipboardHelper.GetTextWithRetry();
        if (current == null) return;
        AddToHistory(current);
    }

    // ── 히스토리 관리 ─────────────────────────────────────────────────────

    /// <summary>
    /// 히스토리에 항목을 추가합니다. 중복 제거 후 맨 앞에 삽입합니다.
    /// </summary>
    private void AddToHistory(string text)
    {
        _history.Remove(text);
        _history.Insert(0, text);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(_history.Count - 1);
        _timestamps[text] = DateTime.Now; // 복사 시각 기록 (상대 시간 표시용)
        HistoryChanged?.Invoke();
        Save();
    }

    /// <summary>해당 텍스트의 마지막 복사 시각을 반환합니다. 기록이 없으면(구형 데이터) null.</summary>
    public DateTime? CopiedAt(string text) => _timestamps.TryGetValue(text, out var t) ? t : null;

    /// <summary>
    /// 기존 항목을 맨 앞으로 이동합니다 (새로 복사한 것처럼).
    /// </summary>
    public void PromoteItem(string text)
    {
        if (!_history.Contains(text)) return;
        _history.Remove(text);
        _history.Insert(0, text);
        _timestamps[text] = DateTime.Now; // 패널에서 붙여넣기해도 "방금"으로 갱신
        HistoryChanged?.Invoke();
        Save();
    }

    /// <summary>
    /// 클립보드에 텍스트를 설정하고 히스토리에서 맨 앞으로 이동합니다.
    /// </summary>
    public void PasteItem(string text)
    {
        // 재시도 로직이 포함된 헬퍼 사용 (다른 프로그램이 클립보드를 점유하고 있어도 안전)
        ClipboardHelper.SetTextWithRetry(text);
        bool clipboardVerified = string.Equals(ClipboardHelper.GetTextWithRetry(), text, StringComparison.Ordinal);
        InputDiagnosticTrace.Log($"CLIPBOARD_SET chars={text.Length} verified={clipboardVerified}");
        _lastSeq = ClipboardHelper.GetSequenceNumberWithRetry(); // 방금 우리가 바꾼 클립보드를 폴링이 "새 복사"로 오인하지 않게

        PromoteItem(text);     // 맨 앞으로 이동
        PromoteFavorite(text); // [2026-11] 핀도 최근 사용 순(MRU) — 칩 스트립에서 첫 칩으로 이동
    }

    /// <summary>
    /// 히스토리 전체를 삭제합니다. (즐겨찾기는 보존 — Win+V의 "히스토리 지우기" 시맨틱)
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
        _timestamps.Clear();
        // [전체 삭제] 현재 시퀀스를 기준점으로 삼아 — 재복사하지 않으면(내용 불변) 재추가되지 않고,
        // 같은 텍스트라도 다시 복사하면 시퀀스가 바뀌어 정상 추가된다.
        // (구 구현은 본문 비교라 같은 텍스트 재복사가 영영 감지되지 않는 문제가 있었다.)
        _lastSeq = ClipboardHelper.GetSequenceNumberWithRetry();
        HistoryChanged?.Invoke();
        Save();
    }

    /// <summary>
    /// 히스토리에서 항목 하나를 삭제합니다. (행의 ✕ 버튼)
    /// 즐겨찾기에서는 건드리지 않습니다 — 완전히 없애려면 ★(고정 해제)부터 해야 합니다.
    /// 참고: 삭제된 텍스트가 지금 시스템 클립보드에 남아 있으면 재복사 시 다시 추가됩니다 (허용).
    /// </summary>
    public void RemoveFromHistory(string text)
    {
        if (!_history.Remove(text)) return;
        _timestamps.Remove(text);
        HistoryChanged?.Invoke();
        Save();
    }

    // ── 즐겨찾기 관리 ─────────────────────────────────────────────────────

    /// <summary>
    /// 즐겨찾기에 항목을 추가합니다. 이미 있으면 무시합니다.
    /// [2026-11] 최대 개수 제한 없음 — 핀은 칩 스트립에서 가로 스크롤로 늘어나므로
    /// 한도 때문에 목록이 줄어드리는 구조가 사라졌습니다.
    /// </summary>
    public void AddFavorite(string text)
    {
        if (_favorites.Contains(text)) return;
        _favorites.Insert(0, text);
        FavoritesChanged?.Invoke();
        Save();
    }

    /// <summary>
    /// 핀을 최근 사용 순(MRU)으로 맨 앞으로 이동합니다.
    /// [2026-11] 칩 스트립이 MRU 순서로 표시되도록 — 붙여넣기(PasteItem) 시 호출해
    /// "최근에 쓴 핀"이 항상 첫 칩에 오게 합니다. 즐겨찾기에 없으면 무시합니다.
    /// </summary>
    public void PromoteFavorite(string text)
    {
        if (!_favorites.Contains(text)) return;
        _favorites.Remove(text);
        _favorites.Insert(0, text);
        FavoritesChanged?.Invoke();
        Save();
    }

    /// <summary>
    /// 즐겨찾기에서 항목을 제거합니다.
    /// </summary>
    public void RemoveFavorite(string text)
    {
        if (!_favorites.Remove(text)) return;
        FavoritesChanged?.Invoke();
        Save();
    }

    /// <summary>
    /// 즐겨찾기 토글 (있으면 제거, 없으면 추가).
    /// </summary>
    public void ToggleFavorite(string text)
    {
        if (_favorites.Contains(text))
            RemoveFavorite(text);
        else
            AddFavorite(text);
    }

    /// <summary>
    /// 해당 항목이 즐겨찾기인지 확인합니다.
    /// </summary>
    public bool IsFavorite(string text) => _favorites.Contains(text);

    // ── 사용자 설정 복원·초기화 연동 ─────────────────────────────────────

    /// <summary>
    /// 사용자 설정 복원 시 호출 — 파일을 다시 읽어 인메모리 히스토리·즐겨찾기를 바꿉니다.
    /// 종료 시 Dispose의 최종 저장이 복원된 내용을 덮어쓰지 않도록 합니다.
    /// </summary>
    public void ReloadFromDisk()
    {
        _history.Clear();
        _favorites.Clear();
        Load();
        HistoryChanged?.Invoke();
        FavoritesChanged?.Invoke();
    }

    /// <summary>
    /// 사용자 설정 초기화 시 호출 — 히스토리와 즐겨찾기를 모두 삭제합니다.
    /// (ClearHistory는 히스토리만 비우므로, 즐겨찾기까지 함께 정리해야 처음 설치 상태가 됩니다.)
    /// </summary>
    public void ClearAll()
    {
        _history.Clear();
        _favorites.Clear();
        _timestamps.Clear();
        // 초기화 후 현재 클립보드 내용이 있는 그대로면 재추가하지 않는다 (ClearHistory와 같은 시퀀스 기준).
        _lastSeq = ClipboardHelper.GetSequenceNumberWithRetry();
        HistoryChanged?.Invoke();
        FavoritesChanged?.Invoke();
        Save();
    }

    // ── 파일 저장/로드 ────────────────────────────────────────────────────

    /// <summary>
    /// 히스토리와 즐겨찾기를 JSON 파일로 저장합니다.
    /// </summary>
    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SavePath);
            if (dir != null) Directory.CreateDirectory(dir);

            var data = new ClipboardData
            {
                History = [.. _history],
                Favorites = [.. _favorites],
                // 복사 시각은 ISO 문자열로 저장 (구형 파일엔 이 필드가 없으므로 로드 시 null 허용)
                Timestamps = _timestamps.ToDictionary(kv => kv.Key, kv => kv.Value.ToString("o"))
            };
            File.WriteAllText(SavePath,
                JsonSerializer.Serialize(data, JsonOptions.Default));
        }
        catch { /* 저장 실패는 무시 (다음 기회에 재시도) */ }
    }

    /// <summary>
    /// JSON 파일에서 히스토리와 즐겨찾기를 불러옵니다.
    /// </summary>
    private void Load()
    {
        if (!File.Exists(SavePath)) return;
        try
        {
            var json = File.ReadAllText(SavePath);
            var data = JsonSerializer.Deserialize<ClipboardData>(json, JsonOptions.Default);
            if (data == null) return;

            _history.Clear();
            _history.AddRange(data.History.Take(MaxHistory));

            _favorites.Clear();
            // [2026-11] 한도 제거 — 저장된 핀을 전부 불러온다 (이전 파일엔 10개까지만 저장됨).
            _favorites.AddRange(data.Favorites);

            // 타임스탬프 로드 — 구형 파일엔 Timestamps 자체가 없거나 개별 항목 파싱이 실패할 수 있음 (null 허용)
            _timestamps.Clear();
            if (data.Timestamps is not null)
            {
                foreach (var kv in data.Timestamps)
                {
                    if (DateTime.TryParse(kv.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
                        _timestamps[kv.Key] = t;
                }
            }
        }
        catch
        {
            // 로드 실패 시 빈 상태로 시작
        }
    }

    // ── 정리 ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _pollTimer.Stop();
        Save(); // 종료 시 최종 저장
    }
}

/// <summary>
/// 클립보드 히스토리와 즐겨찾기를 저장하기 위한 데이터 클래스입니다.
/// </summary>
file class ClipboardData
{
    public List<string> History { get; set; } = [];
    public List<string> Favorites { get; set; } = [];
    // 텍스트 → ISO 8601 복사 시각 (구형 파일엔 없음 — 로드 시 null 허용)
    public Dictionary<string, string>? Timestamps { get; set; }
}
