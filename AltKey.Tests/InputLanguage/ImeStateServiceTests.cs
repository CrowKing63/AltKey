using AltKey.Services;
using AltKey.Services.InputLanguage;

namespace AltKey.Tests.InputLanguage;

/// <summary>
/// ImeStateService의 단일 결론 규칙(DeriveState)과 스레드 언어 판별 경로를 검증합니다.
/// OS IME 읽기가 실패한 상황에서도 내부 상태를 동기화할 수 있는 근거가 이 테스트다.
/// </summary>
public class ImeStateServiceTests
{
    [Fact]
    public void derive_closed_ime_is_english()
    {
        Assert.Equal(ImeState.English, ImeStateService.DeriveState(false, null, null));
    }

    [Fact]
    public void derive_native_conversion_bit_is_korean()
    {
        // 한글 IME의 네이티브 모드(IME_CMODE_NATIVE, bit 0)는 1
        Assert.Equal(ImeState.Korean, ImeStateService.DeriveState(true, 0x00000001, null));
    }

    [Fact]
    public void derive_native_bit_with_other_flags_is_korean()
    {
        // 변환값은 비트마스크 — 네이티브 비트가 있으면 다른 플래그(FULLSHAPE 등)가 붙어도 한글
        Assert.Equal(ImeState.Korean, ImeStateService.DeriveState(true, 0x00000009, null));
    }

    [Fact]
    public void derive_alphanumeric_conversion_is_english()
    {
        // 영문 모드는 네이티브 비트가 0 — 정확히 0 또는 다른 플래그만 있는 경우
        Assert.Equal(ImeState.English, ImeStateService.DeriveState(true, 0x00000000, null));
        Assert.Equal(ImeState.English, ImeStateService.DeriveState(true, 0x00000008, null));
    }

    [Fact]
    public void refresh_fires_state_changed_only_when_state_changes()
    {
        var stub = new StubImeStateService { StubbedState = ImeState.Korean };
        var seen = new List<ImeState>();
        stub.StateChanged += s => seen.Add(s);

        stub.Refresh();
        stub.Refresh();
        stub.StubbedState = ImeState.English;
        stub.Refresh();
        stub.StubbedState = ImeState.Unknown;
        stub.Refresh();

        Assert.Equal(new[] { ImeState.Korean, ImeState.English, ImeState.Unknown }, seen);
        Assert.Equal(ImeState.Unknown, stub.CurrentState);
    }

    [Fact]
    public void sync_from_ime_state_sets_matching_submode_and_ignores_unknown()
    {
        var config = new ConfigService();
        var koDict = new KoreanDictionaryTestable();
        var enDict = new EnglishDictionaryTestable();
        var module = new KoreanInputModule(new FakeInputService(), koDict, enDict, config);
        var autoComplete = new AutoCompleteService(module, koDict, enDict, config,
            new FakeInputService(), new StubImeStateService());

        autoComplete.SyncFromImeState(ImeState.English);
        Assert.Equal(InputSubmode.QuietEnglish, autoComplete.ActiveSubmode);

        autoComplete.SyncFromImeState(ImeState.Korean);
        Assert.Equal(InputSubmode.HangulJamo, autoComplete.ActiveSubmode);

        autoComplete.SyncFromImeState(ImeState.Unknown);
        Assert.Equal(InputSubmode.HangulJamo, autoComplete.ActiveSubmode);
    }

    [Fact]
    public void derive_unknown_conversion_falls_back_to_korean_language_id()
    {
        // 스레드 언어가 한국어면 변환 모드를 못 읽어도 한글로 판별
        Assert.Equal(ImeState.Korean, ImeStateService.DeriveState(true, null, 0x0412));
    }

    [Fact]
    public void derive_unknown_conversion_with_non_korean_language_id_is_unknown()
    {
        Assert.Equal(ImeState.Unknown, ImeStateService.DeriveState(true, null, 0x0409));
    }

    [Fact]
    public void derive_unknown_conversion_without_language_id_is_unknown()
    {
        Assert.Equal(ImeState.Unknown, ImeStateService.DeriveState(true, null, null));
    }
}
