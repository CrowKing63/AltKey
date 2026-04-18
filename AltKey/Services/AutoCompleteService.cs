using AltKey.Models;
using AltKey.Services.InputLanguage;

namespace AltKey.Services;

public sealed class AutoCompleteService
{
    private readonly IInputLanguageModule _module;

    public AutoCompleteService(IInputLanguageModule module)
    {
        _module = module;
        _module.SuggestionsChanged += list => SuggestionsChanged?.Invoke(list);
        _module.SubmodeChanged += submode => SubmodeChanged?.Invoke(submode);
    }

    public string CurrentWord => _module.CurrentWord;

    public event Action<IReadOnlyList<string>>? SuggestionsChanged;
    public event Action<InputSubmode>? SubmodeChanged;

    /// KeyboardViewModel.KeyPressed가 호출.
    /// true면 호출자가 HandleAction 스킵.
    public bool OnKey(KeySlot slot, KeyContext ctx) => _module.HandleKey(slot, ctx);

    /// 자동완성 제안 수락.
    public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion)
        => _module.AcceptSuggestion(suggestion);

    /// 공백/엔터/탭 등 단어 분리자 도달 시 호출.
    public void OnSeparator() => _module.OnSeparator();

    /// 레이아웃 전환·"가/A" 토글 등으로 상태 초기화가 필요할 때.
    public void ResetState() => _module.Reset();

    /// Submode 토글을 외부에 노출 — "가/A" 버튼 액션에서 사용.
    public void ToggleKoreanSubmode() => _module.ToggleSubmode();

    public InputSubmode ActiveSubmode => _module.ActiveSubmode;
    public string ComposeStateLabel => _module.ComposeStateLabel;
}
