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

    public AutoCompleteService(IInputLanguageModule module)
    {
        _module = module;
        // 엔진에서 추천 단어가 바뀌면 UI에도 알림을 보냅니다.
        _module.SuggestionsChanged += list => SuggestionsChanged?.Invoke(list);
        _module.SubmodeChanged += submode => SubmodeChanged?.Invoke(submode);
    }

    /// 현재 입력 중인 단어(조합 중인 한글 등)를 가져옵니다.
    public string CurrentWord => _module.CurrentWord;

    public event Action<IReadOnlyList<string>>? SuggestionsChanged;
    public event Action<InputSubmode>? SubmodeChanged;

    /// <summary>
    /// 키 입력이 들어왔을 때 실행됩니다.
    /// 엔진에서 이 키를 처리했다면(true 반환), 다른 시스템 키 입력 처리를 건너뜁니다.
    /// </summary>
    public bool OnKey(KeySlot slot, KeyContext ctx) => _module.HandleKey(slot, ctx);

    /// <summary>
    /// 추천 목록에서 특정 단어를 선택(수락)했을 때 호출됩니다.
    /// </summary>
    public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
        => _module.AcceptSuggestion(suggestion);

    /// 공백이나 엔터처럼 단어를 끝내는 키가 눌렸을 때 호출됩니다.
    public void OnSeparator() => _module.OnSeparator();

    /// 레이아웃이 바뀌거나 리셋이 필요할 때 호출합니다.
    public void ResetState() => _module.Reset();

    /// "가/A" 버튼을 눌러 한글/영어 입력 모드를 전환합니다.
    public void ToggleKoreanSubmode() => _module.ToggleSubmode();

    /// 현재 입력 중인 단어를 확정 짓습니다.
    public void CommitCurrentWord() => _module.CommitCurrentWord();
    
    /// 현재 조합 중인 글자를 취소합니다.
    public void CancelComposition() => _module.CancelComposition();

    public InputSubmode ActiveSubmode => _module.ActiveSubmode;
    public string ComposeStateLabel => _module.ComposeStateLabel;
}
