using AltKey.Models;

namespace AltKey.Services.InputLanguage;

/// <summary>
/// [역할] 한국어 입력(한글 조합 및 제안)의 핵심 로직을 담당하는 모듈입니다.
/// [기능] 키 입력을 받아 한글로 조합하거나, 영어 입력 모드(QuietEnglish)를 처리하고, 단어 추천 목록을 생성합니다.
/// </summary>
public sealed class KoreanInputModule : IInputLanguageModule
{
    private readonly InputService      _input;
    private readonly HangulComposer    _composer = new(); // 한글 초/중/종성 조합기
    private readonly KoreanDictionary  _koDict;   // 한글 단어 사전 및 추천 엔진
    private readonly EnglishDictionary _enDict;   // 영어 단어 사전
    private readonly ConfigService     _config;   // 설정 서비스

    private InputSubmode _submode = InputSubmode.HangulJamo; // 현재 '가'(한글) 또는 'A'(영어) 상태
    private string _englishPrefix = ""; // 영어 모드에서 입력 중인 글자들
    private string? _lastCommittedWord; // 직전에 입력 완료된 단어 (다음 단어 추천용)
    private string? _suggestionContext; // 추천의 문맥 정보
    private bool _compositionCancelled = false; // 조합이 취소되었는지 여부
    private bool _wordAlreadyCommitted = false; // 이미 단어가 저장되었는지 여부

    public KoreanInputModule(InputService input, KoreanDictionary koDict, EnglishDictionary enDict, ConfigService config)
    {
        _input  = input;
        _koDict = koDict;
        _enDict = enDict;
        _config = config;
    }

    public string LanguageCode => "ko";
    public InputSubmode ActiveSubmode => _submode;
    public string ComposeStateLabel => _submode == InputSubmode.HangulJamo ? "가" : "A";

    // 현재 화면에 표시되거나 조합 중인 단어를 가져옵니다.
    public string CurrentWord => _submode == InputSubmode.HangulJamo
        ? _composer.Current
        : _englishPrefix;

    public event Action<IReadOnlyList<string>>? SuggestionsChanged;
    public event Action<InputSubmode>? SubmodeChanged;

    /// <summary>
    /// 사용자가 누른 키를 분석하여 한글 조합을 진행하거나 시스템에 키를 보냅니다.
    /// </summary>
    /// <returns>true면 이 모듈에서 입력을 완전히 처리했다는 뜻입니다.</returns>
    public bool HandleKey(KeySlot slot, KeyContext ctx)
    {
        // 'A'(영어) 서브모드인 경우 별도 함수에서 처리합니다.
        if (_submode == InputSubmode.QuietEnglish)
        {
            return HandleQuietEnglish(slot, ctx);
        }

        // 컨트롤(Ctrl), 알트(Alt) 등이 눌린 조합키는 한글 조합에서 제외합니다.
        bool isComboKey = ctx.HasActiveModifiersExcludingShift;

        if (isComboKey)
        {
            if (ctx.InputMode == InputMode.Unicode && ctx.TrackedOnScreenLength > 0)
                FinalizeComposition(); // 조합 중이던 글자가 있다면 확정 지음
            return false;
        }

        // 백스페이스나 공백, 엔터 등 특수키 처리
        if (slot.Action is SendKeyAction { Vk: var vkStr }
            && Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
        {
            if (vk == VirtualKeyCode.VK_BACK)
            {
                return HandleBackspace(ctx);
            }

            // 단어를 구분하는 키(공백)가 눌리면 현재 조합을 끝냅니다.
            if (IsSeparator(vk, ctx.ShowUpperCase))
            {
                bool isWordSeparator = vk is VirtualKeyCode.VK_SPACE;
                FinalizeComposition(keepContextForBigram: isWordSeparator);
                return false;
            }
        }

        // 현재 버튼에 할당된 한글 자모(ㄱ, ㄴ, ㅏ 등)를 가져옵니다.
        string? jamo = GetHangulJamoFromSlot(slot, ctx.ShowUpperCase);

        if (jamo is null)
        {
            // 실제로 화면에 텍스트를 보내는 키(숫자·기호 등 SendKey)에만 조합을 확정한다.
            // ToggleSticky(Shift/Ctrl/Win/Alt), ToggleKoreanSubmode 등은 텍스트를 만들지 않으므로
            // 조합 상태를 건드리지 않는다. (쌍받침 회귀 방지)
            if (slot.Action is SendKeyAction && ctx.InputMode == InputMode.Unicode)
                FinalizeComposition();
            return false;
        }

        // 한글 조합기에 글자를 넣고 화면에 반영합니다.
        if (ctx.InputMode == InputMode.Unicode)
        {
            _compositionCancelled = false;
            _wordAlreadyCommitted = false;
            // Feed 전의 현재 조합 텍스트(== 화면)를 붙잡아 둔다.
            // 한글 조합에서는 한 타포마다 바뀌는 것이 항상 꼬리 1~2자뿐이므로,
            // 공통 접두사 아래만 지우고 새 꼬리만 다시 쓴다 (suffix-only rewrite).
            string oldOutput = _composer.Current;
            _composer.Feed(jamo);
            string newOutput = _composer.Current;
            int prefix = CommonPrefixLength(oldOutput, newOutput);
            _input.SendAtomicReplace(oldOutput.Length - prefix, newOutput.Substring(prefix), newOutput.Length);
            SuggestionsChanged?.Invoke(_koDict.GetSuggestions(newOutput, _suggestionContext));
            return true;
        }

        _compositionCancelled = false;
        _wordAlreadyCommitted = false;
        _composer.Feed(jamo);
        SuggestionsChanged?.Invoke(_koDict.GetSuggestions(_composer.Current, _suggestionContext));
        return false;
    }

    /// <summary>
    /// 한국어 레이아웃 내에서 'A'를 눌러 영어 입력 중일 때의 처리 로직입니다.
    /// </summary>
    private bool HandleQuietEnglish(KeySlot slot, KeyContext ctx)
    {
        if (slot.Action is not SendKeyAction { Vk: var vkStr }
            || !Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
            return false;

        if (vk == VirtualKeyCode.VK_BACK)
        {
            return HandleBackspace(ctx);
        }

        if (IsSeparator(vk, ctx.ShowUpperCase))
        {
            bool isWordSeparator = vk is VirtualKeyCode.VK_SPACE;
            FinalizeComposition(keepContextForBigram: isWordSeparator);
            return false;
        }

        char ch = GetEnglishCharFromSlot(slot, ctx.ShowUpperCase);

        if (ctx.HasActiveModifiersExcludingShift || ctx.InputMode == InputMode.VirtualKey)
        {
            TrackEnglishKey(ch != '\0' ? ch : VkToEnglishChar(vk, ctx.ShowUpperCase));
            return false;
        }

        if (ch == '\0')
            ch = VkToEnglishChar(vk, ctx.ShowUpperCase);

        if (ch != '\0')
        {
            // 알파벳만 단어 추적에 포함하고, 숫자·기호는 현재 단어를 확정 후 전송한다.
            if (char.IsLetter(ch))
                TrackEnglishKey(ch);
            else
                FinalizeComposition();
            _input.SendUnicode(ch.ToString());
            return true;
        }
        return false;
    }

    private static char GetEnglishCharFromSlot(KeySlot slot, bool showUpperCase)
    {
        // Shift 중이면 EnglishShiftLabel → ShiftLabel 순서로, 아닐 때면 EnglishLabel → Label 순서로 폴백한다.
        string? label = showUpperCase
            ? (slot.EnglishShiftLabel is { Length: > 0 } ? slot.EnglishShiftLabel : slot.ShiftLabel)
            : (slot.EnglishLabel is { Length: > 0 } ? slot.EnglishLabel : slot.Label);

        if (label is { Length: 1 } && label[0] < 128)
            return showUpperCase ? char.ToUpperInvariant(label[0]) : label[0];

        return '\0';
    }

    /// <summary>
    /// 백스페이스 키 처리. 조합 중인 한글의 획을 하나 지우거나 입력된 영어를 지웁니다.
    /// </summary>
    private bool HandleBackspace(KeyContext ctx)
    {
        if (_submode == InputSubmode.QuietEnglish && _englishPrefix.Length > 0)
        {
            _englishPrefix = _englishPrefix[..^1];
            SuggestionsChanged?.Invoke(_enDict.GetSuggestions(_englishPrefix, _suggestionContext));
            return false;
        }

        if (ctx.InputMode == InputMode.Unicode && ctx.TrackedOnScreenLength > 0)
        {
            // Backspace 전의 현재 조합 텍스트를 붙잡아 둔다.
            // 삭제 후 새 텍스트는 항상 옛 텍스트의 접두사이므로,
            // 지워진 꼬리 만큼만 백스페이스하고 전체를 다시 쓰지 않는다 (suffix-only rewrite).
            string oldOutput = _composer.Current;
            _composer.Backspace();
            string newOutput = _composer.Current;
            int prefix = CommonPrefixLength(oldOutput, newOutput);
            _input.SendAtomicReplace(oldOutput.Length - prefix, newOutput.Substring(prefix), newOutput.Length);
            SuggestionsChanged?.Invoke(_koDict.GetSuggestions(newOutput, _suggestionContext));
            return true;
        }

        if (_composer.HasComposition)
        {
            _composer.Backspace();
            SuggestionsChanged?.Invoke(_koDict.GetSuggestions(_composer.Current, _suggestionContext));
        }
        return false;
    }

    private void TrackEnglishKey(char ch)
    {
        if (ch != '\0')
        {
            _compositionCancelled = false;
            _wordAlreadyCommitted = false;
            _englishPrefix += ch;
            SuggestionsChanged?.Invoke(_enDict.GetSuggestions(_englishPrefix, _suggestionContext));
        }
    }

    /// <summary>
    /// 추천 단어 중 하나를 선택했을 때의 처리입니다.
    /// </summary>
    /// <param name="suggestion">사용자가 선택한 단어</param>
    /// <returns>지워야 할 글자 수, 입력할 전체 단어, 현재 조합과 공유하는 접두사 길이</returns>
    public (int backspaceCount, string fullWord, int prefixLength) AcceptSuggestion(string suggestion)
    {
        int bsCount;
        int prefixLength = 0;
        bool learningEnabled = _config.Current.AutoCompleteEnabled;

        if (_submode == InputSubmode.HangulJamo)
        {
            // Unicode 모드에서 현재 조합 텍스트는 화면에 있는 글자다.
            // 제안어와 다른 꼬리 부분만 지우고 새 꼬리만 쓰게 한다 (suffix-only rewrite).
            string oldOutput = _composer.Current;
            if (_input.Mode == InputMode.Unicode)
            {
                prefixLength = CommonPrefixLength(oldOutput, suggestion);
                bsCount = oldOutput.Length - prefixLength;
            }
            else
            {
                // VirtualKey 모드: OS IME가 조합을 처리하므로 전체를 지우고 전체 단어 전송.
                bsCount = _composer.CompletedLength + _composer.CompositionDepth;
            }
            if (learningEnabled)
            {
                _koDict.RecordWord(suggestion); // 사용자가 선택한 단어를 학습함
                if (_lastCommittedWord is { Length: > 0 })
                    _koDict.RecordBigram(_lastCommittedWord, suggestion); // 앞 단어와의 관계를 학습함
            }
            _composer.Reset();
        }
        else
        {
            // QuietEnglish에서도 누적 중인 prefix가 화면에 있는 글자다. 같은 suffix 규칙 적용.
            string oldOutput = _englishPrefix;
            if (_input.Mode == InputMode.Unicode)
            {
                prefixLength = CommonPrefixLength(oldOutput, suggestion);
                bsCount = oldOutput.Length - prefixLength;
            }
            else
            {
                // VirtualKey 모드: 전체를 지우고 전체 단어 전송.
                bsCount = _englishPrefix.Length;
            }
            if (learningEnabled)
            {
                _enDict.RecordWord(suggestion);
                if (_lastCommittedWord is { Length: > 0 })
                    _enDict.RecordBigram(_lastCommittedWord, suggestion);
            }
            _englishPrefix = "";
        }

        _lastCommittedWord = suggestion;
        _suggestionContext = null;

        SuggestionsChanged?.Invoke(Array.Empty<string>());
        return (bsCount, suggestion, prefixLength);
    }

    /// <summary>
    /// '가'(한글) 모드와 'A'(영어) 모드를 서로 바꿉니다.
    /// </summary>
    public void ToggleSubmode()
    {
        FinalizeComposition();

        _lastCommittedWord = null;
        _suggestionContext = null;

        _submode = _submode == InputSubmode.HangulJamo
            ? InputSubmode.QuietEnglish
            : InputSubmode.HangulJamo;

        SuggestionsChanged?.Invoke(Array.Empty<string>());
        SubmodeChanged?.Invoke(_submode);
    }

    /// <summary>
    /// 외부(AutoCompleteService)에서 단어가 확정되었음을 알립니다.
    /// 엔진 내부의 _lastCommittedWord를 갱신하여 bigram 컨텍스트를 동기화합니다.
    /// </summary>
    public void NotifyWordCommitted(string word)
    {
        _lastCommittedWord = word;
    }

    /// <summary>
    /// 직전에 확정된 단어 (bigram 문맥). AutoCompleteService가 prev로 읽는 유일한 경로.
    /// </summary>
    public string? LastCommittedWord => _lastCommittedWord;

    /// <summary>
    /// 단어 구분자(공백)가 눌렸을 때 호출됩니다. bigram 문맥을 유지합니다.
    /// </summary>
    public void OnSeparator() => FinalizeComposition(keepContextForBigram: true);

    /// <summary>
    /// 모든 입력 상태를 초기값으로 되돌립니다.
    /// </summary>
    public void Reset()
    {
        _composer.Reset();
        _englishPrefix = "";
        _submode = InputSubmode.HangulJamo;
        _lastCommittedWord = null;
        _suggestionContext = null;
        _compositionCancelled = false;
        _wordAlreadyCommitted = false;
        SuggestionsChanged?.Invoke(Array.Empty<string>());
    }

    /// <summary>
    /// 현재 입력 중인 단어를 확정하고 학습 엔진에 기록합니다.
    /// "의도적 확정" 경로(스캔 바 등)이므로 bigram 체인에 참여하지 않습니다 —
    /// _lastCommittedWord를 리셋해 다음 단어가 이 단어와 쌍을 이루지 않게 합니다.
    /// </summary>
    public void CommitCurrentWord()
    {
        if (_compositionCancelled) return;

        bool learningEnabled = _config.Current.AutoCompleteEnabled;

        // unigram 학습만 — 이전 단어와의 bigram 쌍은 기록하지 않음.
        if (_submode == InputSubmode.HangulJamo && _composer.Current.Length > 0)
        {
            if (learningEnabled)
                _koDict.RecordWord(_composer.Current);
        }
        else if (_submode == InputSubmode.QuietEnglish && _englishPrefix.Length >= 2)
        {
            if (learningEnabled)
                _enDict.RecordWord(_englishPrefix);
        }

        // 의도적 확정은 bigram 체인을 끊음 — 다음 단어가 이 단어와 짝을 이루지 않음.
        _lastCommittedWord = null;
        _wordAlreadyCommitted = true;
        _suggestionContext = null;
        _composer.Reset();
        _englishPrefix = "";
        _input.ResetTrackedLength();
        SuggestionsChanged?.Invoke(Array.Empty<string>());
    }

    /// <summary>
    /// 현재 조합 중인 내용을 모두 버리고 초기화합니다. (Esc 키 등)
    /// </summary>
    public void CancelComposition()
    {
        _compositionCancelled = true;

        _composer.Reset();
        _englishPrefix = "";
        _lastCommittedWord = null;
        _suggestionContext = null;
        SuggestionsChanged?.Invoke(Array.Empty<string>());
        _input.ResetTrackedLength();
    }

    /// <summary>
    /// 단어 입력이 끝났을 때 호출되어 현재까지의 내용을 사전 학습에 반영합니다.
    /// <paramref name="keepContextForBigram"/>가 true이면 공백 같은 단어 구분자로,
    /// 바로 다음 단어의 bigram 제안을 띄웁니다.
    /// false이면 구두점 등으로 문맥을 리셋합니다.
    /// </summary>
    private void FinalizeComposition(bool keepContextForBigram = false)
    {
        if (!_composer.HasComposition && _englishPrefix.Length == 0 && _lastCommittedWord is null) return;

        bool learningEnabled = _config.Current.AutoCompleteEnabled
                               && !_compositionCancelled
                               && !_wordAlreadyCommitted;
        string? committed = null;

        if (_submode == InputSubmode.HangulJamo && _composer.Current.Length > 0)
        {
            var word = _composer.Current;
            if (learningEnabled)
            {
                _koDict.RecordWord(word);
                if (_lastCommittedWord is { Length: > 0 })
                    _koDict.RecordBigram(_lastCommittedWord, word);
            }
            committed = word;
        }
        else if (_submode == InputSubmode.QuietEnglish && _englishPrefix.Length >= 2)
        {
            var word = _englishPrefix;
            if (learningEnabled)
            {
                _enDict.RecordWord(word);
                if (_lastCommittedWord is { Length: > 0 })
                    _enDict.RecordBigram(_lastCommittedWord, word);
            }
            committed = word;
        }

        if (committed is not null)
            _lastCommittedWord = committed;

        _compositionCancelled = false;
        _wordAlreadyCommitted = false;
        _composer.Reset();
        _englishPrefix = "";

        if (keepContextForBigram && _lastCommittedWord is { Length: > 0 })
        {
            _suggestionContext = _lastCommittedWord;
            var suggestions = _submode == InputSubmode.HangulJamo
                ? _koDict.GetSuggestions("", _lastCommittedWord)
                : _enDict.GetSuggestions("", _lastCommittedWord);
            SuggestionsChanged?.Invoke(suggestions);
        }
        else
        {
            _suggestionContext = null;
            _lastCommittedWord = null; // 문맥이 끊기는 구분자(마침표, 엔터 등)인 경우 이전 단어 정보를 리셋함
            SuggestionsChanged?.Invoke(Array.Empty<string>());
        }

        _input.ResetTrackedLength();
    }

    private static string? GetHangulJamoFromSlot(KeySlot slot, bool showUpperCase)
    {
        if (showUpperCase && slot.ShiftLabel is { Length: 1 } && IsHangulJamo(slot.ShiftLabel))
            return slot.ShiftLabel;
        if (IsHangulJamo(slot.Label))
            return slot.Label;
        return null;
    }

    /// <summary>
    /// 두 문자열의 공통 접두사 길이를 반환한다.
    /// 조합 텍스트 재작성 시 바뀌지 않은 앞부분을 남기고 꼬리만 지우게 한다
    /// (suffix-only rewrite, docs/auto-complet/TASK-09-suffix-only-rewrite.md).
    /// </summary>
    private static int CommonPrefixLength(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        int i = 0;
        while (i < n && a[i] == b[i]) i++;
        return i;
    }

    /// 한글 자모 또는 완성형 한글인지 확인합니다.
    private static bool IsHangulJamo(string s) =>
        s.Length == 1 && (s[0] >= '\u3131' && s[0] <= '\u3163' || s[0] >= '\uAC00' && s[0] <= '\uD7A3');

    /// <summary>
    /// 특정 키(공백, 엔터, 쉼표 등)가 단어를 끝내는 구분자인지 판별합니다.
    /// </summary>
    private static bool IsSeparator(VirtualKeyCode vk, bool isShifted)
    {
        if (vk is VirtualKeyCode.VK_SPACE or VirtualKeyCode.VK_RETURN
            or VirtualKeyCode.VK_TAB or VirtualKeyCode.VK_OEM_PERIOD
            or VirtualKeyCode.VK_OEM_COMMA or VirtualKeyCode.VK_ESCAPE
            or VirtualKeyCode.VK_DELETE)
        {
            return true;
        }

        if (vk is VirtualKeyCode.VK_OEM_7 or VirtualKeyCode.VK_OEM_4 or 
            VirtualKeyCode.VK_OEM_6 or VirtualKeyCode.VK_OEM_1)
        {
            return true;
        }

        // Shift와 함께 눌린 숫자키 등도 구분자로 취급할 수 있습니다.
        if (isShifted && (vk is VirtualKeyCode.VK_1 or VirtualKeyCode.VK_OEM_2 or 
                          VirtualKeyCode.VK_9 or VirtualKeyCode.VK_0))
        {
            return true;
        }

        return false;
    }

    private static char VkToEnglishChar(VirtualKeyCode vk, bool upperCase)
    {
        if (vk >= VirtualKeyCode.VK_A && vk <= VirtualKeyCode.VK_Z)
        {
            char c = (char)('a' + ((int)vk - (int)VirtualKeyCode.VK_A));
            return upperCase ? char.ToUpperInvariant(c) : c;
        }
        return '\0';
    }
}
