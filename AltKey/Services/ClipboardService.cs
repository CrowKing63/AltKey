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
    private const int MaxFavorites = 10; // 즐겨찾기 최대 개수

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
    private readonly DispatcherTimer _pollTimer;
    private string? _lastClipboard;

    // 저장 파일 경로 (PathResolver.DataDir + clipboard_history.json)
    private static string SavePath => Path.Combine(PathResolver.DataDir, "clipboard_history.json");

    public ClipboardService()
    {
        // 저장된 히스토리와 즐겨찾기 불러오기
        Load();

        // 클립보드 폴링 (500ms 간격)
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    // ── 클립보드 폴링 ─────────────────────────────────────────────────────

    private void OnPollTick(object? sender, EventArgs e)
    {
        // 재시도 로직이 포함된 헬퍼 사용 (다른 프로그램이 클립보드를 점유하고 있어도 안전)
        if (!ClipboardHelper.ContainsTextWithRetry()) return;
        var current = ClipboardHelper.GetTextWithRetry();
        if (current == null || current == _lastClipboard) return;
        _lastClipboard = current;
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
        HistoryChanged?.Invoke();
        Save();
    }

    /// <summary>
    /// 기존 항목을 맨 앞으로 이동합니다 (새로 복사한 것처럼).
    /// </summary>
    public void PromoteItem(string text)
    {
        if (!_history.Contains(text)) return;
        _history.Remove(text);
        _history.Insert(0, text);
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
        _lastClipboard = text; // 히스토리 중복 추가 방지
        PromoteItem(text);     // 맨 앞으로 이동
    }

    /// <summary>
    /// 히스토리 전체를 삭제합니다.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
        _lastClipboard = null;
        HistoryChanged?.Invoke();
        Save();
    }

    // ── 즐겨찾기 관리 ─────────────────────────────────────────────────────

    /// <summary>
    /// 즐겨찾기에 항목을 추가합니다. 이미 있으면 무시합니다.
    /// </summary>
    public void AddFavorite(string text)
    {
        if (_favorites.Contains(text)) return;
        if (_favorites.Count >= MaxFavorites)
            _favorites.RemoveAt(_favorites.Count - 1);
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
                Favorites = [.. _favorites]
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
            _favorites.AddRange(data.Favorites.Take(MaxFavorites));
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
}
