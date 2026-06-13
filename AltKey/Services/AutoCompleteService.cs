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
    private readonly KoreanDictionary _koDict; // 한글 사전 (bigram 저장소 접근용)
    private readonly EnglishDictionary _enDict; // 영어 사전 (bigram 저장소 접근용)

    private string? _lastCommittedWord; // 직전에 수락된 단어 (bigram 문맥용)

    public AutoCompleteService(IInputLanguageModule module, KoreanDictionary koDict, EnglishDictionary enDict)
    {
        _module = module;
        _koDict = koDict;
        _enDict = enDict;
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
    public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
    {
        var result = _module.AcceptSuggestion(suggestion);
        _lastCommittedWord = result.fullWord;

        // 수락된 단어를 기준으로 바이그램 추천을 즉시 조회
        var bigramStore = GetActiveBigramStore();
        var nexts = bigramStore.GetNexts(result.fullWord, "");
        if (nexts.Count > 0)
        {
            var bigramSuggestions = nexts.Select(n => n.Next).ToList();
            SuggestionsChanged?.Invoke(bigramSuggestions, SuggestionMode.Bigram);
        }

        return result;
    }

    /// <summary>
    /// 바이그램 추천에서 단어를 선택했을 때 호출됩니다.
    /// 선행 공백(U+0020)을 포함한 문자열을 반환하며, 내부에서 bigram 학습과 연속 추천을 처리합니다.
    /// </summary>
    public string AcceptBigramSuggestion(string suggestion)
    {
        // 바이그램 학습 (_module.OnSeparator 전에 먼저 실행)
        var bigramStore = GetActiveBigramStore();
        if (_lastCommittedWord is { Length: > 0 })
            bigramStore.Record(_lastCommittedWord, suggestion);

        _lastCommittedWord = suggestion;
        _module.NotifyWordCommitted(suggestion); // 엔진 내부 _lastCommittedWord 동기화

        // 내부 상태 정리 (public OnSeparator 경유 시 _lastCommittedWord 덮어쓰기 방지)
        FlushEngineState();

        // 수락된 단어를 새 prev로 연속 바이그램 조회
        var nexts = bigramStore.GetNexts(suggestion, "");
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

        // 선행 공백을 포함한 문자열 반환 (호출자가 유니코드 출력 경로로 처리)
        return " " + suggestion;
    }

    /// <summary>
    /// 공백이나 엔터처럼 단어를 끝내는 키가 눌렸을 때 호출됩니다.
    /// 수동 입력 단어를 bigram에 기록합니다.
    /// </summary>
    public void OnSeparator()
    {
        // OnSeparator 내부에서 CurrentWord가 초기화될 수 있으므로 먼저 캡처
        string current = _module.CurrentWord;

        if (_lastCommittedWord is { Length: > 0 } && current.Length > 0)
            GetActiveBigramStore().Record(_lastCommittedWord, current);

        if (current.Length > 0)
            _lastCommittedWord = current;

        _module.OnSeparator();
    }

    /// 엔진 상태만 정리하고 _lastCommittedWord는 변경하지 않습니다.
    private void FlushEngineState() => _module.OnSeparator();

    /// 레이아웃이 바뀌거나 리셋이 필요할 때 호출합니다.
    public void ResetState()
    {
        _lastCommittedWord = null;
        _module.Reset();
    }

    /// "가/A" 버튼을 눌러 한글/영어 입력 모드를 전환합니다.
    public void ToggleKoreanSubmode() => _module.ToggleSubmode();

    /// 현재 입력 중인 단어를 확정 짓습니다.
    public void CommitCurrentWord() => _module.CommitCurrentWord();

    /// 현재 조합 중인 글자를 취소합니다.
    public void CancelComposition() => _module.CancelComposition();

    public InputSubmode ActiveSubmode => _module.ActiveSubmode;
    public string ComposeStateLabel => _module.ComposeStateLabel;

    /// 현재 서브모드에 맞는 bigram 저장소를 반환합니다.
    private BigramFrequencyStore GetActiveBigramStore() =>
        _module.ActiveSubmode == InputSubmode.HangulJamo
            ? _koDict.BigramStore
            : _enDict.BigramStore;

    private void OnSubmodeChanged(InputSubmode _)
    {
        // 언어 전환 시 bigram 문맥 초기화
        _lastCommittedWord = null;
        SubmodeChanged?.Invoke(_);
    }
}
