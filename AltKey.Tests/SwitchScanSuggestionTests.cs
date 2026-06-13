using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using AltKey.Tests.InputLanguage;
using AltKey.ViewModels;

namespace AltKey.Tests;

public class SwitchScanSuggestionTests
{
    [Fact]
    public void SuggestionBar_scan_targets_include_current_word_and_suggestions()
    {
        var module = new FakeInputLanguageModule();
        var autoComplete = new AutoCompleteService(module, new KoreanDictionaryTestable(), new EnglishDictionaryTestable());
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = true;
        var vm = new SuggestionBarViewModel(
            autoComplete,
            new FakeInputService(),
            config,
            new KoreanDictionaryTestable(),
            new EnglishDictionaryTestable());

        module.SetState("해", ["해요", "해도"]);

        Assert.Equal(3, vm.ScanTargets.Count);
        Assert.Equal("CurrentWord", vm.ScanTargets[0].Kind);
        Assert.Equal("Suggestion", vm.ScanTargets[1].Kind);
        Assert.Equal("Suggestion", vm.ScanTargets[2].Kind);
    }

    [Fact]
    public void SuggestionBar_scan_targets_empty_when_autocomplete_off()
    {
        var module = new FakeInputLanguageModule();
        var config = new ConfigService();
        config.Current.AutoCompleteEnabled = false;
        var autoComplete = new AutoCompleteService(module, new KoreanDictionaryTestable(), new EnglishDictionaryTestable());
        var vm = new SuggestionBarViewModel(
            autoComplete,
            new FakeInputService(),
            config,
            new KoreanDictionaryTestable(),
            new EnglishDictionaryTestable());

        module.SetState("해", ["해요"]);

        Assert.Empty(vm.ScanTargets);
    }

    private sealed class FakeInputLanguageModule : IInputLanguageModule
    {
        public string LanguageCode => "ko";
        public InputSubmode ActiveSubmode => InputSubmode.HangulJamo;
        public string ComposeStateLabel => "가";
        public string CurrentWord { get; private set; } = "";

        public event Action<IReadOnlyList<string>>? SuggestionsChanged;

        // 이 테스트는 서브모드 변경 경로를 검증하지 않으므로, 인터페이스 계약만 만족하는 빈 이벤트 구현을 둡니다.
        public event Action<InputSubmode>? SubmodeChanged
        {
            add { }
            remove { }
        }

        public bool HandleKey(KeySlot slot, KeyContext ctx) => false;
        public (int backspaceCount, string fullWord) AcceptSuggestion(string suggestion) => (0, suggestion);
        public void ToggleSubmode() { }
        public void OnSeparator() { }
        public void Reset() { }
        public void CommitCurrentWord() { }
        public void CancelComposition() { }

        public void SetState(string currentWord, IReadOnlyList<string> suggestions)
        {
            CurrentWord = currentWord;
            SuggestionsChanged?.Invoke(suggestions);
        }
    }
}
