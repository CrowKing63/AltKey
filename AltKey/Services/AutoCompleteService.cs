using AltKey.Models;

namespace AltKey.Services;

/// 자동 완성 서비스 — 영문 알파벳 + 한글 자모 조합 지원
/// IsKoreanMode가 true면 한글 자동 완성, false면 영문 자동 완성을 사용한다.
public class AutoCompleteService
{
    private readonly WordFrequencyStore _store;
    private readonly KoreanDictionary _koreanDict;
    private readonly HangulComposer _hangul = new();
    private string _currentWord = "";
    private bool _isHangulMode = false;

    /// 현재 레이아웃이 한국어인지 여부 (MainViewModel에서 레이아웃 전환 시 설정)
    public bool IsKoreanMode { get; set; }

    /// 제안 목록이 바뀔 때 발생 (UI 스레드가 아닐 수 있으므로 Dispatcher.Invoke 필요)
    public event Action<IReadOnlyList<string>>? SuggestionsChanged;

    /// 현재 조합 중인 단어 (SuggestionBarViewModel 에서 나머지 문자 계산에 사용)
    public string CurrentWord => _isHangulMode ? _hangul.Current : _currentWord;

    public AutoCompleteService(WordFrequencyStore store, KoreanDictionary koreanDict)
    {
        _store = store;
        _koreanDict = koreanDict;
    }

    /// 한글 자모 입력 (KeyboardViewModel에서 호출)
    public void OnHangulInput(string jamo)
    {
        _isHangulMode = true;
        _hangul.Feed(jamo);
        var suggestions = _koreanDict.GetSuggestions(_hangul.Current);
        SuggestionsChanged?.Invoke(suggestions);
    }

    /// InputService 의 SendKeyAction 처리 시 호출
    /// 한국어 모드에서는 영문 자동 완성 추적을 건너뛴다.
    public void OnKeyInput(VirtualKeyCode vk)
    {
        if (IsKoreanMode) return;

        if (IsWordSeparator(vk))
        {
            // 단어 완성: 학습 후 초기화
            if (_currentWord.Length >= 2)
                _store.RecordWord(_currentWord);
            _currentWord = "";
            SuggestionsChanged?.Invoke([]);
            return;
        }

        if (vk == VirtualKeyCode.VK_BACK)
        {
            if (_currentWord.Length > 0)
                _currentWord = _currentWord[..^1];
        }
        else
        {
            var ch = VkToChar(vk);
            if (ch != '\0') _currentWord += ch;
        }

        var suggestions = _store.GetSuggestions(_currentWord);
        SuggestionsChanged?.Invoke(suggestions);
    }

    /// 제안 단어를 수락하면 현재 단어로 학습하고 상태 초기화
    /// 반환값: (삭제할 기존 입력 길이, 입력할 전체 단어)
    public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
    {
        var prefix = _isHangulMode ? _hangul.Current : _currentWord;
        var bsCount = prefix.Length;

        _store.RecordWord(suggestion);
        _hangul.Reset();
        _currentWord = "";
        _isHangulMode = false;
        SuggestionsChanged?.Invoke([]);
        return (bsCount, suggestion);
    }

    /// 레이아웃 전환 시 상태 초기화
    public void ResetState()
    {
        _hangul.Reset();
        _currentWord = "";
        _isHangulMode = false;
        SuggestionsChanged?.Invoke([]);
    }

    // ── 내부 헬퍼 ───────────────────────────────────────────────────────────

    private static bool IsWordSeparator(VirtualKeyCode vk) =>
        vk is VirtualKeyCode.VK_SPACE  or VirtualKeyCode.VK_RETURN
           or VirtualKeyCode.VK_TAB    or VirtualKeyCode.VK_OEM_PERIOD
           or VirtualKeyCode.VK_OEM_COMMA;

    /// VK_A~VK_Z → 'a'~'z', 그 외 '\0'
    private static char VkToChar(VirtualKeyCode vk)
    {
        if (vk >= VirtualKeyCode.VK_A && vk <= VirtualKeyCode.VK_Z)
            return (char)('a' + ((int)vk - (int)VirtualKeyCode.VK_A));
        return '\0';
    }
}