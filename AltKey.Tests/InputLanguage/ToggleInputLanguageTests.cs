using AltKey.Models;
using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// AutoCompleteService.ToggleInputLanguage(통합 한/영 전환)의 모드별 분기를 검증합니다.
/// </summary>
public class ToggleInputLanguageTests
{
    private static (AutoCompleteService service, KoreanInputModule module, FakeInputService input, StubImeStateService ime) Build()
    {
        var input = new FakeInputService { Mode = InputMode.Unicode };
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        var module = new KoreanInputModule(input, koDict, enDict, config);
        var ime = new StubImeStateService();
        var service = new AutoCompleteService(module, koDict, enDict, config, input, ime);
        return (service, module, input, ime);
    }

    private static (AutoCompleteService service, KoreanInputModule module, FakeInputService input, StubImeStateService ime) BuildVirtualKey()
    {
        var input = new FakeInputService { Mode = InputMode.VirtualKey };
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        var module = new KoreanInputModule(input, koDict, enDict, config);
        var ime = new StubImeStateService();
        var service = new AutoCompleteService(module, koDict, enDict, config, input, ime);
        return (service, module, input, ime);
    }

    /// 유니코드(자동완성/패드) 모드: 내부 토글만 하고 OS IME는 만지지 않습니다.
    [Fact]
    public void unicode_mode_toggles_submode_without_sending_key()
    {
        var (service, module, input, _) = Build();

        service.ToggleInputLanguage();

        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
        Assert.Empty(input.KeyPresses);
    }

    /// 유니코드 강제(패드): 외부 모드는 가상키라도 forceUnicode로 유니코드 분기.
    [Fact]
    public void force_unicode_always_goes_unicode_path()
    {
        var (service, module, input, ime) = BuildVirtualKey();

        service.ToggleInputLanguage(forceUnicode: true);

        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
        Assert.Empty(input.KeyPresses);
    }

    /// 가상키 모드 + 판별 "한국어": VK_HANGUL 전송 후 내부 서브모드를 한글로 동기화.
    [Fact]
    public void virtualkey_korean_sync_internal_state()
    {
        var (service, module, input, ime) = BuildVirtualKey();
        module.SetInputLanguage(InputSubmode.QuietEnglish); // 기본 상태 이후 전환
        ime.StubbedState = ImeState.Korean;

        service.ToggleInputLanguage();

        Assert.Contains(VirtualKeyCode.VK_HANGUL, input.KeyPresses);
        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);
    }

    /// 가상키 모드 + 판별 "영어": 내부 서브모드를 조용한 영어로 동기화.
    [Fact]
    public void virtualkey_english_sync_internal_state()
    {
        var (service, module, input, ime) = BuildVirtualKey();
        ime.StubbedState = ImeState.English;

        service.ToggleInputLanguage();

        Assert.Contains(VirtualKeyCode.VK_HANGUL, input.KeyPresses);
        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
    }

    /// 가상키 모드에서는 OS IME 이벤트가 내부 서브모드를 바꿉니다.
    [Fact]
    public void ime_event_sync_applies_in_virtualkey_mode()
    {
        var (service, module, _, _) = BuildVirtualKey();

        service.SyncFromImeStateWhenVirtualKey(ImeState.English);

        Assert.Equal(InputSubmode.QuietEnglish, module.ActiveSubmode);
    }

    /// 유니코드 모드에서는 OS IME 이벤트가 내부 조합 상태를 덮어쓰지 않습니다.
    [Fact]
    public void ime_event_sync_is_ignored_in_unicode_mode()
    {
        var (service, module, _, _) = Build();

        service.SyncFromImeStateWhenVirtualKey(ImeState.English);

        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode);
    }

    /// 가상키 모드 + 판별 "알 수 없음": 전송은 했지만 내부 상태는 유지한다.
    [Fact]
    public void virtualkey_unknown_keeps_internal_state()
    {
        var (service, module, input, ime) = BuildVirtualKey();
        ime.StubbedState = ImeState.Unknown;

        service.ToggleInputLanguage();

        Assert.Contains(VirtualKeyCode.VK_HANGUL, input.KeyPresses);
        Assert.Equal(InputSubmode.HangulJamo, module.ActiveSubmode); // 초기 상태 유지
    }
}

/// <summary>
/// 가상키 모드에서 VK_HANGUL 전송 직후 IME 상태를 폴링해 동기화하는 경로를 검증합니다.
/// </summary>
public class ToggleInputLanguagePollTests
{
    private static (AutoCompleteService service, KoreanInputModule module, SequenceImeStateService ime) BuildVirtualKey(
        SequenceImeStateService ime)
    {
        var input = new FakeInputService { Mode = InputMode.VirtualKey };
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var config = new ConfigService();
        var module = new KoreanInputModule(input, koDict, enDict, config);
        var service = new AutoCompleteService(module, koDict, enDict, config, input, ime);
        return (service, module, ime);
    }

    [Fact]
    public void virtualkey_poll_waits_for_post_toggle_state()
    {
        // pre-send 조회: 영문 → 전송 후 폴링: 한국어(토글 확정)
        var ime = new SequenceImeStateService(ImeState.English, ImeState.Korean);
        var (service, module, _) = BuildVirtualKey(ime);
        module.SetInputLanguage(InputSubmode.QuietEnglish); // 내부를 영어로 둔다

        service.ToggleInputLanguage();

        Assert.Equal(InputSubmode.HangulJamo, service.ActiveSubmode);
    }

    [Fact]
    public void virtualkey_poll_stays_unknown_when_foreground_never_reports()
    {
        // pre-send 조회: 알 수 없음 → 폴링도 알 수 없음 — 내부 상태는 유지한다
        var ime = new SequenceImeStateService(ImeState.Unknown, ImeState.Unknown, ImeState.Unknown);
        var (service, module, _) = BuildVirtualKey(ime);

        service.ToggleInputLanguage();

        Assert.Equal(InputSubmode.HangulJamo, service.ActiveSubmode);
    }

    [Fact]
    public void virtualkey_poll_syncs_english_when_toggle_confirms_english()
    {
        // pre-send 조회: 한국어 → 전송 후 폴링: 영문 — 내부를 조용한 영어로 스냅
        var ime = new SequenceImeStateService(ImeState.Korean, ImeState.English);
        var (service, module, _) = BuildVirtualKey(ime);
        module.SetInputLanguage(InputSubmode.HangulJamo); // 내부를 한국어로 둔다

        service.ToggleInputLanguage();

        Assert.Equal(InputSubmode.QuietEnglish, service.ActiveSubmode);
    }

    /// 토글 직후 캐시가 갱신돼야 자동완성 전환 시 이전 상태로 되돌아가지 않습니다.
    [Fact]
    public void virtualkey_toggle_refreshes_cached_ime_state()
    {
        var ime = new SequenceImeStateService(ImeState.Korean, ImeState.English);
        var (service, _, observedIme) = BuildVirtualKey(ime);

        service.ToggleInputLanguage();

        Assert.Equal(ImeState.English, observedIme.CurrentState);
    }
}
