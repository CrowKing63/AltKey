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

public class ImeStateServiceStabilizationTests
{
    // 임계값을 1로 끌어올려 안정화 대기 로직을 검사하는 가짜.
    private sealed class DelayedStubImeStateService : ImeStateService
    {
        public ImeState StubbedState { get; set; } = ImeState.Unknown;
        protected override int RefreshThrottleMilliseconds => 0;
        protected override int StabilityMatchesRequired => 2;
        public override ImeState QueryForegroundImeState() => StubbedState;
    }

    [Fact]
    public void refresh_waits_for_consecutive_matches_before_firing()
    {
        var stub = new DelayedStubImeStateService { StubbedState = ImeState.Korean };
        var seen = new List<ImeState>();
        stub.StateChanged += s => seen.Add(s);

        stub.Refresh(); // 1차 — 아직 인정 안 함
        Assert.Empty(seen);
        stub.Refresh(); // 2차 — 같은 상태가 연속으로 나왔으므로 인정
        Assert.Equal(new[] { ImeState.Korean }, seen);

        stub.Refresh(); // 이미 반영된 상태 — 다시 발화하지 않음
        Assert.Single(seen); // 이미 반영된 상태 — 다시 발화하지 않음
        Assert.Equal(ImeState.Korean, seen[0]);
    }

    [Fact]
    public void refresh_does_not_fire_while_flapping_and_fires_after_agreement()
    {
        var stub = new DelayedStubImeStateService { StubbedState = ImeState.Korean };
        var seen = new List<ImeState>();
        stub.StateChanged += s => seen.Add(s);

        stub.Refresh(); // Korean 1차
        stub.StubbedState = ImeState.English;
        stub.Refresh(); // English 1차 — pending 초기화
        stub.StubbedState = ImeState.Korean;
        stub.Refresh(); // Korean 1차 — pending 초기화
        Assert.Empty(seen);

        stub.StubbedState = ImeState.English; // English로 안정 전환
        stub.Refresh();
        stub.Refresh();
        Assert.Equal(new[] { ImeState.English }, seen);
    }

    [Fact]
    public void refresh_fires_unknown_immediately_when_transitions_to_unknown()
    {
        var stub = new DelayedStubImeStateService { StubbedState = ImeState.Korean };
        var seen = new List<ImeState>();
        stub.StateChanged += s => seen.Add(s);

        stub.Refresh(); // pending Korean 1차
        stub.Refresh(); // 인정 → Korean 발화
        Assert.Equal(new[] { ImeState.Korean }, seen);

        stub.StubbedState = ImeState.Unknown;
        stub.Refresh(); // 안정화 대기 없이 즉시 발화
        Assert.Equal(new[] { ImeState.Korean, ImeState.Unknown }, seen);
    }

    [Fact]
    public void observe_state_clears_pending_so_next_stable_state_fires_after_stabilization()
    {
        var stub = new DelayedStubImeStateService { StubbedState = ImeState.Korean };
        var seen = new List<ImeState>();
        stub.StateChanged += s => seen.Add(s);

        stub.Refresh(); // pending Korean 1차
        Assert.Empty(seen);

        // 토글 경로는 캐시만 반영하고 추적을 비운다 — 이전 pending이 남아 찌그러지지 않게 한다.
        stub.ObserveState(ImeState.English);
        // 포그라운드가 한글 상태로 안정화되면 안정화 대기(1차 인정까지 필요하므로) 끝에 발화한다.
        stub.StubbedState = ImeState.Korean;
        stub.Refresh();
        Assert.Empty(seen);
        stub.Refresh();
        Assert.Equal(new[] { ImeState.Korean }, seen);
    }
}
