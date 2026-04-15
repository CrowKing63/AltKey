using AltKey.Models;

namespace AltKey.Services;

/// T-9.3: 자동 완성 서비스 — 키 입력을 추적해 단어 제안 이벤트를 발생시킨다.
/// 초기 버전은 영문 알파벳만 지원 (한글은 IME/TSF 통합 필요).
public class AutoCompleteService
{
    private readonly WordFrequencyStore _store;
    private string _currentWord = "";

    /// 제안 목록이 바뀔 때 발생 (UI 스레드가 아닐 수 있으므로 Dispatcher.Invoke 필요)
    public event Action<IReadOnlyList<string>>? SuggestionsChanged;

    /// 현재 조합 중인 단어 (SuggestionBarViewModel 에서 나머지 문자 계산에 사용)
    public string CurrentWord => _currentWord;

    public AutoCompleteService(WordFrequencyStore store)
    {
        _store = store;
    }

    /// InputService 의 SendKeyAction 처리 시 호출
    public void OnKeyInput(VirtualKeyCode vk)
    {
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
    /// 반환값: 현재 입력 후 추가로 타이핑해야 할 나머지 문자열
    public string AcceptSuggestion(string suggestion)
    {
        var remaining = suggestion.Length > _currentWord.Length
            ? suggestion[_currentWord.Length..]
            : "";

        _store.RecordWord(suggestion);
        _currentWord = "";
        SuggestionsChanged?.Invoke([]);
        return remaining;
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
