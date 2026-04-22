using AltKey.Models;

namespace AltKey.Services.InputLanguage;

public sealed class KoreanInputModule : IInputLanguageModule
{
    private readonly InputService      _input;
    private readonly HangulComposer    _composer = new();
    private readonly KoreanDictionary  _koDict;
    private readonly EnglishDictionary _enDict;
    private readonly ConfigService     _config;

    private InputSubmode _submode = InputSubmode.HangulJamo;
    private string _englishPrefix = "";
    private string? _lastCommittedWord;
    private string? _suggestionContext;
    private bool _compositionCancelled = false;
    private bool _wordAlreadyCommitted = false;

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

    public string CurrentWord => _submode == InputSubmode.HangulJamo
        ? _composer.Current
        : _englishPrefix;

    public event Action<IReadOnlyList<string>>? SuggestionsChanged;
    public event Action<InputSubmode>? SubmodeChanged;

    public bool HandleKey(KeySlot slot, KeyContext ctx)
    {
        if (_submode == InputSubmode.QuietEnglish)
        {
            return HandleQuietEnglish(slot, ctx);
        }

        bool isComboKey = ctx.HasActiveModifiersExcludingShift;

        if (isComboKey)
        {
            if (ctx.InputMode == InputMode.Unicode && ctx.TrackedOnScreenLength > 0)
                FinalizeComposition();
            return false;
        }

        if (slot.Action is SendKeyAction { Vk: var vkStr }
            && Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
        {
            if (vk == VirtualKeyCode.VK_BACK)
            {
                return HandleBackspace(ctx);
            }

            if (IsSeparator(vk, ctx.ShowUpperCase))
            {
                FinalizeComposition();
                return false;
            }
        }

        string? jamo = GetHangulJamoFromSlot(slot, ctx.ShowUpperCase);

        if (jamo is null)
        {
            return false;
        }

        if (ctx.InputMode == InputMode.Unicode)
        {
            int prevLen = ctx.TrackedOnScreenLength;
            _compositionCancelled = false;
            _wordAlreadyCommitted = false;
            _composer.Feed(jamo);
            string newOutput = _composer.Current;
            _input.SendAtomicReplace(prevLen, newOutput);
            SuggestionsChanged?.Invoke(_koDict.GetSuggestions(newOutput, _suggestionContext));
            return true;
        }

        _compositionCancelled = false;
        _wordAlreadyCommitted = false;
        _composer.Feed(jamo);
        SuggestionsChanged?.Invoke(_koDict.GetSuggestions(_composer.Current, _suggestionContext));
        return false;
    }

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
            FinalizeComposition();
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
            TrackEnglishKey(ch);
            _input.SendUnicode(ch.ToString());
            return true;
        }
        return false;
    }

    private static char GetEnglishCharFromSlot(KeySlot slot, bool showUpperCase)
    {
        string? label = showUpperCase && slot.EnglishShiftLabel is { Length: > 0 }
            ? slot.EnglishShiftLabel
            : slot.EnglishLabel ?? slot.Label;

        if (label is { Length: 1 } && label[0] < 128)
            return showUpperCase ? char.ToUpperInvariant(label[0]) : label[0];

        return '\0';
    }

    private bool HandleBackspace(KeyContext ctx)
    {
        // QuietEnglish 모드에서는 prefix를 줄임
        if (_submode == InputSubmode.QuietEnglish && _englishPrefix.Length > 0)
        {
            _englishPrefix = _englishPrefix[..^1];
            SuggestionsChanged?.Invoke(_enDict.GetSuggestions(_englishPrefix, _suggestionContext));
            return false;
        }

        if (ctx.InputMode == InputMode.Unicode && ctx.TrackedOnScreenLength > 0)
        {
            int prevLen = ctx.TrackedOnScreenLength;
            _composer.Backspace();
            string newOutput = _composer.Current;
            _input.SendAtomicReplace(prevLen, newOutput);
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

    public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
    {
        int bsCount;
        bool learningEnabled = _config.Current.AutoCompleteEnabled;

        if (_submode == InputSubmode.HangulJamo)
        {
            bsCount = _input.Mode == InputMode.Unicode
                ? _composer.Current.Length
                : _composer.CompletedLength + _composer.CompositionDepth;
            if (learningEnabled)
            {
                _koDict.RecordWord(suggestion);
                if (_lastCommittedWord is { Length: > 0 })
                    _koDict.RecordBigram(_lastCommittedWord, suggestion);
            }
            _composer.Reset();
        }
        else
        {
            bsCount = _englishPrefix.Length;
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
        return (bsCount, suggestion);
    }

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

    public void OnSeparator() => FinalizeComposition();

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

    public void CommitCurrentWord()
    {
        if (_compositionCancelled) return;

        bool learningEnabled = _config.Current.AutoCompleteEnabled;

        if (_submode == InputSubmode.HangulJamo && _composer.Current.Length > 0)
        {
            var word = _composer.Current;
            if (learningEnabled)
            {
                _koDict.RecordWord(word);
                if (_lastCommittedWord is { Length: > 0 })
                    _koDict.RecordBigram(_lastCommittedWord, word);
            }
            _lastCommittedWord = word;
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
            _lastCommittedWord = word;
        }

        _wordAlreadyCommitted = true;
        _suggestionContext = null;
        _composer.Reset();
        _englishPrefix = "";
        _input.ResetTrackedLength();
        SuggestionsChanged?.Invoke(Array.Empty<string>());
    }

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

    private void FinalizeComposition()
    {
        if (!_composer.HasComposition && _englishPrefix.Length == 0) return;

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
        _suggestionContext = _lastCommittedWord;
        SuggestionsChanged?.Invoke(Array.Empty<string>());
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

    private static bool IsHangulJamo(string s) =>
        s.Length == 1 && (s[0] >= '\u3131' && s[0] <= '\u3163' || s[0] >= '\uAC00' && s[0] <= '\uD7A3');

    private static bool IsSeparator(VirtualKeyCode vk, bool isShifted)
    {
        if (vk is VirtualKeyCode.VK_SPACE or VirtualKeyCode.VK_RETURN
            or VirtualKeyCode.VK_TAB or VirtualKeyCode.VK_OEM_PERIOD
            or VirtualKeyCode.VK_OEM_COMMA)
        {
            return true;
        }

        if (vk is VirtualKeyCode.VK_OEM_7 or VirtualKeyCode.VK_OEM_4 or 
            VirtualKeyCode.VK_OEM_6 or VirtualKeyCode.VK_OEM_1)
        {
            return true;
        }

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
