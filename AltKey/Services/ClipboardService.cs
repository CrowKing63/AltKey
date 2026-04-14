using System.Windows.Threading;
using WpfClipboard = System.Windows.Clipboard;

namespace AltKey.Services;

public class ClipboardService : IDisposable
{
    private const int MaxHistory = 20;

    public event Action? HistoryChanged;

    // 최신 항목이 앞에 오는 리스트
    public IReadOnlyList<string> History => _history;

    private readonly List<string> _history = [];
    private readonly DispatcherTimer _pollTimer;
    private string? _lastClipboard;

    public ClipboardService()
    {
        // 클립보드 폴링 (500ms 간격)
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        try
        {
            if (!WpfClipboard.ContainsText()) return;
            var current = WpfClipboard.GetText();
            if (current == _lastClipboard) return;
            _lastClipboard = current;
            AddToHistory(current);
        }
        catch { /* 클립보드 접근 실패 무시 */ }
    }

    private void AddToHistory(string text)
    {
        // 중복 제거 후 맨 앞에 삽입
        _history.Remove(text);
        _history.Insert(0, text);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(_history.Count - 1);
        HistoryChanged?.Invoke();
    }

    public void PasteItem(string text)
    {
        // 클립보드에 텍스트 설정 후 Ctrl+V 전송
        WpfClipboard.SetText(text);
        _lastClipboard = text; // 히스토리 중복 추가 방지
    }

    public void ClearHistory()
    {
        _history.Clear();
        _lastClipboard = null;
        HistoryChanged?.Invoke();
    }

    public void Dispose() => _pollTimer.Stop();
}
