using AltKey.Models;
using AltKey.Services.InputLanguage;

namespace AltKey.Services;

/// <summary>
/// [역할] 키보드의 자동 완성 기능을 총괄하는 서비스입니다.
/// [기능] 입력 언어 모듈(KoreanInputModule 등)과 UI 사이를 중개하며, 단어 추천 및 조합 상태 관리를 제어합니다.
/// </summary>
public sealed class AutoCompleteService
{
    private readonly IInputLanguageModule _module; // 한글/영어 등 실제 입력 엔진
    private readonly KoreanDictionary _koDict; // 한글 사전 (기록·제안 경로)
    private readonly EnglishDictionary _enDict; // 영어 사전 (기록·제안 경로)
    private readonly ConfigService _config; // 설정 서비스 (AutoCompleteEnabled 확인용)
    private readonly InputService _inputService; // 입력 모드/키 전송
    private readonly ImeStateService _imeState; // OS IME 판별 (가상키 모드 동기화용)
    private readonly LiveRegionService? _liveRegion; // 접근성 안내 (판별 실패 시)

    public AutoCompleteService(IInputLanguageModule module, KoreanDictionary koDict, EnglishDictionary enDict, ConfigService config,
        InputService inputService, ImeStateService imeState, LiveRegionService? liveRegion = null)
    {
        _module = module;
        _koDict = koDict;
        _enDict = enDict;
        _config = config;
        _inputService = inputService;
        _imeState = imeState;
        _liveRegion = liveRegion;
        // 엔진에서 추천 단어가 바뀌면 UI에도 알림을 보냅니다. (기본 일반 모드)
        _module.SuggestionsChanged += list => SuggestionsChanged?.Invoke(list, SuggestionMode.Normal);
        _module.SubmodeChanged += OnSubmodeChanged;
    }

    /// 현재 입력 중인 단어(조합 중인 한글 등)를 가져옵니다.
    public string CurrentWord => _module.CurrentWord;

    /// <summary>
    /// 자동완성 제안이 변경되었음을 알립니다.
    /// mode가 Bigram이면 바이그램 문맥 기반 추천임을 나타냅니다.
    /// </summary>
    public event Action<IReadOnlyList<string>, SuggestionMode>? SuggestionsChanged;
    public event Action<InputSubmode>? SubmodeChanged;

    /// <summary>
    /// 키 입력이 들어왔을 때 실행됩니다.
    /// </summary>
    public bool OnKey(KeySlot slot, KeyContext ctx)
    {
        return _module.HandleKey(slot, ctx);
    }

    /// <summary>
    /// 추천 목록에서 특정 단어를 선택(수락)했을 때 호출됩니다.
    /// 수락 후 즉시 바이그램 추천을 조회하여 표시합니다.
    /// </summary>
    public (int backspaceCount, string fullWord, int prefixLength) AcceptSuggestion(string suggestion)
    {
        var result = _module.AcceptSuggestion(suggestion);
        // bigram 문맥(_lastCommittedWord)은 모듈이 소유 — 여기서 복제하지 않는다.

        // 수락된 단어를 기준으로 바이그램 추천을 즉시 조회
        // (fullWord는 꼬리 공백이 포함된 삽입 문자열이라 조회 키로는 쓸 수 없다)
        var bigramStore = GetActiveBigramStore();
        var nexts = bigramStore.GetNexts(suggestion, "");
        if (nexts.Count > 0)
        {
            var bigramSuggestions = nexts.Select(n => n.Next).ToList();
            SuggestionsChanged?.Invoke(bigramSuggestions, SuggestionMode.Bigram);
        }

        return result;
    }

    /// <summary>
    /// 바이그램 추천에서 단어를 선택했을 때 호출됩니다.
    /// 꼬리 공백(U+0020)을 포함한 문자열을 반환하며, 내부에서 bigram 학습과 연속 추천을 처리합니다.
    /// </summary>
    public string AcceptBigramSuggestion(string suggestion)
    {
        // 자모 나열 방어: 기록과 문맥 갱신을 건너뜀
        if (ContainsRawJamo(suggestion))
        {
            // [꼬리 공백] 이전 단어의 꼬리 공백은 이제 실제 텍스트 — 플래그만 정리.
            _module.ClearPendingTrailingSpace();
            FlushEngineState();
            return suggestion + " ";
        }

        // 학습은 사전의 단일 경로를 거친다 (적격성 필터 + AutoCompleteEnabled 게이트).
        // BigramFrequencyStore에 직접 Record하는 우회 경로는 금지 — CORE-LOGIC-PROTECTION.md §3.
        if (_config.Current.AutoCompleteEnabled)
            RecordThroughDictionary(suggestion);

        _module.NotifyWordCommitted(suggestion); // 엔진 내부 _lastCommittedWord 갱신

        // [꼬리 공백] 순서: 이전 플래그 정리 → 상태 플러시(내부 OnSeparator가 환원하지 않음) → 새 플래그 설정.
        _module.ClearPendingTrailingSpace();
        FlushEngineState();
        _module.MarkTrailingSpacePending();

        // 수락된 단어를 새 prev로 연속 바이그램 조회
        var nexts = GetActiveBigramStore().GetNexts(suggestion, "");
        if (nexts.Count > 0)
        {
            var bigramSuggestions = nexts.Select(n => n.Next).ToList();
            SuggestionsChanged?.Invoke(bigramSuggestions, SuggestionMode.Bigram);
        }
        else
        {
            // 더 이상 바이그램이 없으면 빈 목록으로 초기화
            SuggestionsChanged?.Invoke(Array.Empty<string>(), SuggestionMode.Normal);
        }

        // 꼬리 공백을 포함한 문자열 반환 (호출자가 유니코드 출력 경로로 처리)
        return suggestion + " ";
    }

    /// <summary>
    /// 공백이나 엔터처럼 단어를 끝내는 키가 눌렸을 때 호출됩니다.
    /// bigram 기록은 KoreanInputModule.FinalizeComposition이 사전 경유로 단일 처리한다.
    /// (여기서 직접 Record하면 빈도가 2배로 세진다 — CORE-LOGIC-PROTECTION.md §3.)
    /// </summary>
    public void OnSeparator() => _module.OnSeparator();

    /// 엔진 상태만 정리하고 _lastCommittedWord는 변경하지 않습니다.
    private void FlushEngineState() => _module.OnSeparator();

    /// 레이아웃이 바뀌거나 리셋이 필요할 때 호출합니다.
    public void ResetState() => _module.Reset();

    /// <summary>
    /// 가/A·한/영 통합 키의 전환 처리입니다 (docs/feature-input-toggle-unification/plan.md).
    /// 유니코드 모드(자동완성 ON 또는 패드 포커스) → 내부 서브모드 토글.
    /// 가상키 모드 → OS IME를 토글하고, 판별 결과로 내부 서브모드를 동기화한다.
    /// forceUnicode는 패드 버튼이 외부 모드 추측과 무관하게 항상 유니코드 분기로 들어가기 위해 쓴다.
    /// </summary>
    public void ToggleInputLanguage(bool forceUnicode = false)
    {
        var effectiveMode = (forceUnicode || _inputService.Mode == InputMode.Unicode)
            ? InputMode.Unicode
            : InputMode.VirtualKey;

        if (effectiveMode == InputMode.Unicode)
        {
            _module.ToggleSubmode();
            return;
        }

        // 가상키 모드: OS IME를 토글한 뒤, 변화가 반영될 때까지 폴링해 현재 상태로 동기화한다.
        // 즉시 한 번만 조회하면 토글 처리 전의 이전 상태를 읽어 내부 서브모드가 동기화되지 않는다.
        var pre = _imeState.QueryForegroundImeState();
        _inputService.SendKeyPress(VirtualKeyCode.VK_HANGUL);

        switch (_imeState.QueryForegroundImeStateAfterToggle(pre))
        {
            case ImeState.Korean:
                _module.SetInputLanguage(InputSubmode.HangulJamo);
                return;
            case ImeState.English:
                _module.SetInputLanguage(InputSubmode.QuietEnglish);
                return;
            default:
                // 판별 불가 — 내부 상태를 바꾸지 않고 접근성 안내만 발화
                _liveRegion?.Announce("OS IME 상태를 알 수 없음");
                return;
        }
    }

    /// <summary>
    /// OS IME 상태 변경을 내부 서브모드에 반영한다.
    /// 사용자가 한/영 키를 직접 누르거나 다른 창으로 포커스를 옮긴 경우 문자 키 라벨을 맞춘다.
    /// 판별 불가(Unknown)는 내부 상태를 바꾸지 않는다.
    /// </summary>
    public void SyncFromImeState(ImeState state)
    {
        switch (state)
        {
            case ImeState.Korean:
                _module.SetInputLanguage(InputSubmode.HangulJamo);
                break;
            case ImeState.English:
                _module.SetInputLanguage(InputSubmode.QuietEnglish);
                break;
        }
    }

    /// 현재 입력 중인 단어를 확정 짓습니다.
    public void CommitCurrentWord() => _module.CommitCurrentWord();

    /// 현재 조합 중인 글자를 취소합니다.
    public void CancelComposition() => _module.CancelComposition();

    public InputSubmode ActiveSubmode => _module.ActiveSubmode;
    public string ComposeStateLabel => _module.ComposeStateLabel;

    /// 자모 단독 문자(U+3131-U+314E: ㄱ-ㅎ, U+3161-U+3175: ㅏ-ㅣ)가 하나라도 포함되어 있으면 true를 반환합니다.
    /// bigram 기록과 _lastCommittedWord 오염을 방어합니다.
    private static bool ContainsRawJamo(string word) =>
        word.Any(c => c is (>= '\u3131' and <= '\u314E')
                        or (>= '\u3161' and <= '\u3175'));

    /// 현재 서브모드에 맞는 bigram 저장소를 반환합니다.
    private BigramFrequencyStore GetActiveBigramStore() =>
        _module.ActiveSubmode == InputSubmode.HangulJamo
            ? _koDict.BigramStore
            : _enDict.BigramStore;

    /// <summary>
    /// 활성 사전 경유로 단어 + bigram 쌍을 기록한다 (적격성 필터가 사전 내부에 있다).
    /// prev는 모듈이 소유한 _lastCommittedWord에서 읽는다.
    /// </summary>
    private void RecordThroughDictionary(string word)
    {
        var prev = _module.LastCommittedWord;
        if (_module.ActiveSubmode == InputSubmode.QuietEnglish)
        {
            _enDict.RecordWord(word);
            if (prev is { Length: > 0 })
                _enDict.RecordBigram(prev, word);
        }
        else
        {
            _koDict.RecordWord(word);
            if (prev is { Length: > 0 })
                _koDict.RecordBigram(prev, word);
        }
    }

    private void OnSubmodeChanged(InputSubmode submode)
    {
        // 언어 전환 시 bigram 문맥 초기화는 ToggleSubmode → FinalizeComposition이 처리한다.
        SubmodeChanged?.Invoke(submode);
    }
}
