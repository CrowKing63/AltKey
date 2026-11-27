using System;
using System.Runtime.InteropServices;
using System.Threading;
using AltKey.Platform;

namespace AltKey.Services;

/// <summary>
/// OS IME의 현재 상태를 판별해 알려주는 서비스입니다.
/// 창 스레드의 GUITHREADINFO(포커스/캐럿/활성 창), 기본 IME 윈도우, ImmGetContext 순서로 응답해
/// "실제로 지금 한글 또는 영문 입력이 일어나고 있는" 상태를 추적합니다.
/// 이 판별은 AltKey가 내부 서브모드를 동기화하는 데 쓰이는 참고용이며, IME를 제어하지는 않습니다.
/// 설계 문서: docs/feature-ime-detection/plan.md
/// </summary>
public enum ImeState
{
    /// <summary>판별 불가(안전한 기본값). 내부 상태를 바꾸지 않는다.</summary>
    Unknown,
    /// <summary>OS IME가 한글(조합) 모드.</summary>
    Korean,
    /// <summary>OS IME가 영문자/영어 모드.</summary>
    English
}

/// <summary>
/// OS IME 판별 서비스. 순서대로 후보 창을 노리고, 단일 DeriveState 진리원으로 결론을 낸다.
/// </summary>
public class ImeStateService
{
    // 외부 조회 상한 — IME가 응답하지 않아도 AltKey는 걸리지 않는다.
    private const uint QueryTimeoutMs = 50;

    // VK_HANGUL 전송 후 OS IME 상태가 반영될 때까지 대기하는 최대 시간(폴링).
    // 샌드박스/VM에서는 전환(특히 영문→한글, 즉 IME 오픈)이 SendInput 반환 직후 50ms 안에
    // 끝나지 않을 수 있어 넉넉하게 잡는다. 상태가 바뀌는 즉시 조기 반환하므로
    // 빠른 전환에는 지연이 생기지 않는다.
    private const int ToggleSettleTimeoutMs = 300;

    // ImmGetLanguageID는 LANGID(고유 언어 식별자)를 반환한다. 한국어(ko-KR) = 0x0412.
    // 일본어(ja-JP) 등 다른 국가별 조합 모드는 "NATIVE"에서 Korean으로 처리하려는 의도로는 쓰지 않는다 —
    // 문서의 "언어(하위 10비트)" 표는 한글 판별 전용으로 한정했다.
    private const int LangIdKoKr = 0x0412;

    // ── 현재 상태 / 변경 알림 ──────────────────────────────────────────────

    private ImeState _currentState = ImeState.Unknown;

    /// <summary>마지막 Refresh에서 확인한 OS IME 상태.</summary>
    public ImeState CurrentState => _currentState;

    /// <summary>OS IME 상태가 실제로 바뀔 때만 발화한다.</summary>
    public event Action<ImeState>? StateChanged;

    // 400ms 타이머와 WinEvent가 거의 동시에 Refresh를 부를 수 있다.
    // 아주 짧은 간격을 두면 창 후보 순회·SendMessageTimeout 중복을 줄일 수 있다.
    // 값을 키우면 OS 조회는 줄지만 라벨 반영이 그만큼 늦어진다.
    private const int RefreshThrottleMs = 100;
    private DateTime _lastRefreshUtc = DateTime.MinValue;

    /// <summary>테스트처럼 즉시 갱신이 필요하면 0으로 재정의한다.</summary>
    protected virtual int RefreshThrottleMilliseconds => RefreshThrottleMs;

    /// <summary>
    /// 현재 OS IME 상태를 다시 조회하고, 이전과 다를 때만 StateChanged를 발화한다.
    /// 주기적 폴링에서 호출한다. 판별 실패(Unknown)도 상태 변화로 알리지만,
    /// 수신자가 Unknown이면 내부 상태를 바꾸지 않는다.
    /// 같은 순간의 중복 호출은 위 간격 안에서 한 번만 실제 조회한다.
    /// </summary>
    public void Refresh()
    {
        var now = DateTime.UtcNow;
        if ((now - _lastRefreshUtc).TotalMilliseconds < RefreshThrottleMilliseconds) return;
        _lastRefreshUtc = now;

        var next = QueryForegroundImeState();
        if (next == _currentState) return;
        _currentState = next;
        StateChanged?.Invoke(next);
    }

    /// <summary>
    /// 이미 조회한 상태를 캐시에만 반영한다. 한/영 토글 폴링처럼 다시 OS를 묻지 않아도 될 때 쓴다.
    /// StateChanged는 발화하지 않는다 — 호출자가 내부 서브모드를 직접 동기화하기 때문이다.
    /// </summary>
    public void ObserveState(ImeState state) => _currentState = state;

    // ── 외부 진입점 ────────────────────────────────────────────────────────

    /// <summary>
    /// 한/영 키 전후에 OS IME 상태가 실제로 바뀌는지 확인하는 폴링 경로입니다.
    /// 전송 직후 한 번만 조회하면 토글 처리 전의 이전 상태를 읽으므로(가상키 모드에서 라벨이 안 바뀌는 원인),
    /// preState와 달라지는 순간까지 timeoutMs 안에서 반복 조회합니다.
    /// 기본값은 ToggleSettleTimeoutMs — 샌드박스/VM에서 전환(특히 IME 오픈)이 50ms를 넘길 수 있어 넉넉하게 잡는다.
    /// </summary>
    public virtual ImeState QueryForegroundImeStateAfterToggle(ImeState preState, int timeoutMs = ToggleSettleTimeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            var s = QueryForegroundImeState();
            if (s != preState)
                return s;
            if (DateTime.UtcNow >= deadline)
                return s;
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// 현재 포그라운드 창에서 OS IME 상태를 판별한다. AltKey 자기 창은 대상으로 삼지 않는다.
    /// </summary>
    public virtual ImeState QueryForegroundImeState()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return ImeState.Unknown;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == (uint)Environment.ProcessId) return ImeState.Unknown;

        foreach (var candidate in BuildCandidates(hwnd))
        {
            var state = QueryCandidate(candidate);
            if (state != ImeState.Unknown) return state;
        }
        return ImeState.Unknown;
    }

    // ── 후보 창 구성 ──────────────────────────────────────────────────────

    /// <summary>
    /// "지금 텍스트 입력이 일어나는 창"을 여러 방식으로 노리는 후보 목록을 만든다(중복 제거, 신뢰도 순).
    /// </summary>
    internal IReadOnlyList<IntPtr> BuildCandidates(IntPtr foregroundHwnd)
    {
        var set = new HashSet<IntPtr>();
        var list = new List<IntPtr>();

        void Add(IntPtr h)
        {
            if (h == IntPtr.Zero) return;
            if (set.Add(h)) list.Add(h);
        }

        // 1) GUITHREADINFO — 스레드의 포커스/캐럿/활성 창
        var threadId = Win32.GetWindowThreadProcessId(foregroundHwnd, out _);
        var info = new Win32.GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<Win32.GUITHREADINFO>() };
        if (Win32.GetGUIThreadInfo(threadId, ref info))
        {
            Add(info.hwndFocus);
            Add(info.hwndCaret);
            Add(info.hwndActive);
        }

        // 1.5) 포커스/캐럿/활성/포그라운드의 자손 중 "기본 IME 윈도우를 가진 창"을 후보로 — 실제 텍스트
        //      입력이 일어나는 Edit 컨트롤은 보통 fg의 자식이라, 위로만 오르는 후보(2·3번)로는 못 찾는다.
        //      GetGUIThreadInfo가 편집 컨트롤을 안 돌려주는 앱(Notepad 등)에서 Unknown/잘못된 상태가
        //      나던 원인을 여기서 해결한다. 자식은 동기 열거라 한 번의 전환당 비용만 든다.
        foreach (var anchor in new[] { info.hwndFocus, info.hwndCaret, info.hwndActive, foregroundHwnd })
        {
            if (anchor == IntPtr.Zero) continue;
            Win32.EnumChildWindows(anchor, (child, _) =>
            {
                if (child != IntPtr.Zero && Win32.ImmGetDefaultIMEWnd(child) != IntPtr.Zero)
                    Add(child);
                return true;
            }, IntPtr.Zero);
        }

        // 2) 포그라운드 창 자체
        Add(foregroundHwnd);

        // 3) 부모 체인 (상위 3단계 + 루트/소유자)
        var cur = foregroundHwnd;
        for (var depth = 0; depth < 3 && cur != IntPtr.Zero; depth++)
        {
            cur = Win32.GetParent(cur);
            Add(cur);
        }
        var root = Win32.GetAncestor(foregroundHwnd, Win32.GA_ROOT);
        Add(root);
        var owner = Win32.GetAncestor(foregroundHwnd, Win32.GA_ROOTOWNER);
        Add(owner);

        return list;
    }

    // ── 개별 창 상태 읽기 ──────────────────────────────────────────────────

    /// <summary>
    /// 후보 창에서 두 단계로 묻는다: ① 기본 IME 윈도우에 WM_IME_CONTROL로 묻기,
    /// ② 실패하면 ImmGetContext 후 ImmGetOpenStatus/ImmGetConversionStatus/ImmGetLanguageID.
    /// </summary>
    /// <summary>
    /// 창 스레드의 키보드 레이아웃에서 LANGID를 읽는다. 호출 실패·데드 스레드면 null.
    /// 테스트가 이 값을 가짜로 주입할 수 있도록 protected virtual로 둔다.
    /// </summary>
    protected virtual int? QueryThreadLanguageId(IntPtr window)
    {
        var threadId = Win32.GetWindowThreadProcessId(window, out _);
        var layout = Win32.GetKeyboardLayout(threadId);
        if (layout == IntPtr.Zero) return null;
        return (int)((ulong)layout.ToInt64() & 0xFFFF);
    }

    private ImeState QueryCandidate(IntPtr window)
    {
        if (window == IntPtr.Zero) return ImeState.Unknown;

        // ① 기본 IME 윈도우에 묻기
        var imeWnd = Win32.ImmGetDefaultIMEWnd(window);
        if (imeWnd != IntPtr.Zero)
        {
            // 열림 상태 조회 — 닫힘이면 영문
            if (SendImeControl(imeWnd, Win32.IMC_GETOPENSTATUS, out var open))
            {
                if (open == 0) return DeriveState(false, null, null);
                if (SendImeControl(imeWnd, Win32.IMC_GETCONVERSIONMODE, out var conv))
                    return DeriveState(true, conv, null);
                // 열림은 확인했는데 변환 모드 읽기가 실패 — 창 스레드 언어로 한글 여부를 판별한다.
                // 매뉴얼 IME 상태가 단순화되는 구형 IME 대응.
                var threadLang = QueryThreadLanguageId(window);
                if (threadLang is int lang && lang == LangIdKoKr)
                    return DeriveState(true, null, lang);
                // 스레드 언어로 결론 지을 수 없으면 ②로 물러난다
            }
        }

        // ② 컨텍스트에 묻기 (실제 Win32 시그니처: 반환값이 HIMC)
        var context = Win32.ImmGetContext(window);
        if (context == IntPtr.Zero)
            return ImeState.Unknown;

        try
        {
            // ImmGetOpenStatus는 반환값 자체가 열림 여부다.
            if (!Win32.ImmGetOpenStatus(context))
                return DeriveState(false, null, null);
            // 변환 모드는 비트마스크 — 읽기 실패 시에만 null로 전달
            int conv = 0;
            int sentence = 0;
            bool convRead = Win32.ImmGetConversionStatus(context, out conv, out sentence);
            int? conversion = convRead ? (int?)conv : null;
            var langId = Win32.ImmGetLanguageID(context);
            return DeriveState(true, conversion, langId);
        }
        finally
        {
            Win32.ImmReleaseContext(window, context);
        }
    }

    private static bool SendImeControl(IntPtr imeWnd, int msg, out int result)
    {
        result = 0;
        var hWnd = imeWnd;
        if (!Win32.SendMessageTimeout(hWnd, Win32.WM_IME_CONTROL, (IntPtr)msg, IntPtr.Zero,
                Win32.SMTO_BLOCK | Win32.SMTO_ABORTIFHUNG, QueryTimeoutMs, out var lpdwResult))
            return false;
        result = lpdwResult.ToInt32();
        return true;
    }

    // ── 단일 진리원 ────────────────────────────────────────────────────────

    /// <summary>
    /// 모든 판별에서 공유하는 유일한 결론 규칙.
    /// </summary>
    /// <param name="openKnown">열림 상태(true)/닫힘(false) 여부. false면 영문으로 판별.</param>
    /// <param name="conversion">변환 모드 값(없으면 null).</param>
    /// <param name="languageId">ImmGetLanguageID 반환값(없으면 null).</param>
    public static ImeState DeriveState(bool openKnown, int? conversion, int? languageId)
    {
        // 닫힘 → 입력 자체가 영문
        if (!openKnown) return ImeState.English;

        if (conversion is int mode)
        {
            // 변환 모드는 비트마스크다. 네이티브(한글) 비트만 확인한다.
            // mode & 1 != 0 → Korean, 아니면 English.
            // (예전 == 비교는 한글 값 1을 ALPHANUMERIC(0x1)로 오해해 영어로 뒤집었다.)
            return (mode & Win32.IME_CMODE_NATIVE) != 0 ? ImeState.Korean : ImeState.English;
        }

        // 변환 모드를 못 읽는 구형/현대 IME — 언어로 한글 판별
        if (languageId is int lang && lang == LangIdKoKr)
            return ImeState.Korean;

        return ImeState.Unknown;
    }
}
