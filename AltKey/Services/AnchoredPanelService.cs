using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace AltKey.Services;

/// <summary>
/// [역할] 키보드 창(메인 창)에 앵커되는 패널(빠른 메모·클립보드 등)의 호스트 서비스 — 여러 topmost 보조 창을 한 곳에서 만들고 관리합니다.
/// [기능] 첫 열림 시 생성, Show/Hide(✕는 숨김만), 트레이 동기화(키보드 창과 함께 숨김/복원),
///        투명도 완전 연동, 패널 개폐·키보드 이동/크기 변경 시 재배치(AnchoredPanelsLayout).
/// [참고] 위치 계산은 순수 함수 AnchoredPanelsLayout — 이 서비스는 창 상태와 이벤트 배선만 담당합니다.
/// </summary>
public class AnchoredPanelService
{
    // 패널 등록 정보 — id: 식별자, order: 복수 패널 한 줄 배치 시 표시 순서(왼쪽→오른쪽).
    // [2026-11] Width/Height 필드 제거 — 크기의 SSOT은 각 XAML의 Width/Height(표준 높이 H=400)이고,
    // 레이아웃(RelayoutAll)도 실제 창 치수를 읽는다. 등록 인자가 죽어 있는 중복 데이터였다.
    private sealed record PanelEntry(
        string Id, int Order, Func<Window> Factory, Action? OnBeforeShow, Action? OnShow, Action? OnHide);

    private MainWindow? _host;
    private readonly List<PanelEntry> _panels = [];
    // id → 창 (첫 열림 시 생성)
    private readonly Dictionary<string, Window> _windows = new();
    // id → 트레이 숨김 전 열려 있었는지 — 복원 시 다시 표시하는 데 씁니다.
    private readonly Dictionary<string, bool> _wasOpenBeforeTrayHide = new();

    /// <summary>
    /// 패널을 등록합니다. onBeforeShow: Show() 직전 호출(포커스를 가져가기 전 외부 앱 기록 등),
    /// onShow: 열기 직후 호출(포커스 놓기 등), onHide: 숨기기 직전 호출(빠른 메모는 즉시 저장).
    /// 크기(width/height) 인자는 없다 — 크기는 패널 창 XAML이 소유한다 (표준 높이 H=400).
    /// </summary>
    public void Register(string id, int order, Func<Window> factory,
        Action? onBeforeShow = null, Action? onShow = null, Action? onHide = null)
        => _panels.Add(new PanelEntry(id, order, factory, onBeforeShow, onShow, onHide));

    /// <summary>
    /// 키보드 창(호스트)을 연결합니다 — 이동·크기 변경·숨김/보임·투명도 이벤트를 여기서 구독합니다.
    /// </summary>
    public void AttachHost(MainWindow host)
    {
        _host = host;
        host.LocationChanged += OnHostMovedOrResized;
        host.SizeChanged += OnHostMovedOrResized;
        host.IsVisibleChanged += OnHostVisibilityChanged;

        // 투명도 완전 연동: 키보드 창이 페이드(유휴·설정 변경)하면 열린 패널도 같은 값으로 따라갑니다 (2026-11-15).
        host.OpacityStateApplied += OnHostOpacityStateApplied;
    }

    /// <summary>패널을 토글합니다 — 열려 있으면 닫고, 닫혀 있으면 열기 + 포커스.</summary>
    public void Toggle(string id)
    {
        if (_host is null) return; // 호스트 연결 전 호출은 무시 (생성자에서 등록만 하고 AttachHost가 나중에 옵니다).
        var w = GetOrCreate(id);
        if (w.IsVisible) HidePanel(w, FindEntry(id));
        else ShowPanel(w, FindEntry(id));
    }

    /// <summary>패널을 숨깁니다 (✕ 버튼). 창은 파괴하지 않습니다.</summary>
    public void Hide(string id)
    {
        if (_windows.TryGetValue(id, out var w) && w.IsVisible)
            HidePanel(w, FindEntry(id));
    }

    /// <summary>패널이 현재 열려 있는지 확인합니다.</summary>
    public bool IsVisible(string id) => _windows.TryGetValue(id, out var w) && w.IsVisible;

    // ── 내부 구현 ────────────────────────────────────────────────

    private PanelEntry FindEntry(string id) => _panels.First(p => p.Id == id);

    /// <summary>첫 열림 시 창을 만들고 키보드 창에 연결합니다 (Owner: 작업표시줄에 안 보임, 앱 종료 시 자동 정리).</summary>
    private Window GetOrCreate(string id)
    {
        if (_windows.TryGetValue(id, out var w)) return w;

        var entry = FindEntry(id);
        var window = entry.Factory();
        if (_host is { } host && window.Owner is null)
            window.Owner = host; // Owner는 첫 Show 전에만 설정 가능 — 생성 시점에 연결합니다.

        _windows[id] = window;
        return window;
    }

    private void ShowPanel(Window w, PanelEntry entry)
    {
        // 키보드 창의 현재 투명도에서 시작합니다 — 유휴 페이딩 중인 동안에 패널이 갑자기 100%로 튀는 것을 방지 (2026-11-15).
        if (_host is { } host)
            w.Opacity = host.Opacity;

        entry.OnBeforeShow?.Invoke(); // Show() 전 — 이 창이 포커스를 가져가기 직전의 포그라운드(외부 앱)을 기록하는 등.
        w.Show();
        // [포커스 뺏김 방지] 열릴 때 Activate()를 하지 않는다 — 입력창 커서가 사라지지 않는다.
        // 터치 클릭은 백그라운드 창에도 전달되므로(Windows는 포그라운드가 아닌 창에도 마우스 이벤트를 준다)
        // 패널 안에서 항목을 탭하면 Windows가 자동으로 활성화해(클릭 활성화) 붙여넣기 복원 로직이 그대로 동작한다.
        entry.OnShow?.Invoke();
        RelayoutAll();
    }

    private void HidePanel(Window w, PanelEntry entry)
    {
        entry.OnHide?.Invoke(); // 빠른 메모: 500ms 디바운스 창을 넘어 안 저장된 내용이 없도록 즉시 저장
        w.Hide();
        RelayoutAll(); // 남은 패널(2개 이상일 땐 나란히)을 재배치합니다.
    }

    // 키보드 창이 이동·크기 변경(드래그든 상하 이동 버튼이든)되면 열린 패널을 재배치합니다.
    private void OnHostMovedOrResized(object? sender, EventArgs e) => RelayoutAll();

    // 키보드 창이 트레이로 숨겨지면 열린 패널도 함께 숨기고, 복원 시 다시 표시합니다.
    // (owned 창은 owner Hide에 자동 숨기지 않으므로 여기서 명시 처리.)
    private void OnHostVisibilityChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (_host is not { } host) return;

        if (!host.IsVisible)
        {
            foreach (var entry in _panels)
            {
                if (_windows.TryGetValue(entry.Id, out var w) && w.IsVisible)
                {
                    _wasOpenBeforeTrayHide[entry.Id] = true;
                    HidePanel(w, entry);
                }
            }
        }
        else
        {
            foreach (var entry in _panels)
            {
                if (_wasOpenBeforeTrayHide.GetValueOrDefault(entry.Id))
                {
                    _wasOpenBeforeTrayHide[entry.Id] = false;
                    ShowPanel(GetOrCreate(entry.Id), entry); // 포커스는 뺏지 않는다 — ShowPanel이 Activate()를 하지 않음
                }
            }
        }
    }

    // 투명도 완전 연동 — 키보드 창의 투명도 목표가 바뀌면(유휴 페이딩·설정 변경 등) 열린 패널도 같은 값으로 동기화합니다.
    private void OnHostOpacityStateApplied(double targetOpacity, int durationMs)
    {
        foreach (var entry in _panels)
        {
            if (!_windows.TryGetValue(entry.Id, out var w) || !w.IsVisible) continue;

            if (durationMs <= 0)
            {
                // 즉시 적용 — 이전 애니메이션을 제거하고 값만 씁니다.
                w.BeginAnimation(Window.OpacityProperty, null);
                w.Opacity = targetOpacity;
            }
            else
            {
                // 같은 시간으로 함께 페이드합니다 (BeginAnimation은 이전 애니메이션을 자동으로 대체).
                w.BeginAnimation(Window.OpacityProperty, new DoubleAnimation
                {
                    From = w.Opacity,
                    To = targetOpacity,
                    Duration = new Duration(TimeSpan.FromMilliseconds(durationMs))
                });
            }
        }
    }

    // 열려 있는 패널 전체의 위치를 다시 계산·적용합니다 — 매번 계산만 하고 저장하지 않습니다.
    // (패널 드래그로 만든 임시 오프셋은 다음 재계산 시 계산 위치로 돌아갑니다.)
    private void RelayoutAll()
    {
        if (_host is not { } host) return;

        var visible = _panels
            .Where(p => _windows.TryGetValue(p.Id, out var w) && w.IsVisible)
            .Select(p => new AnchoredPanelsLayout.PanelSpec(
                p.Id, p.Order, _windows[p.Id].Width, _windows[p.Id].Height))
            .ToList();
        if (visible.Count == 0) return;

        var placements = AnchoredPanelsLayout.PlacePanels(
            new Rect(host.Left, host.Top, host.ActualWidth, host.ActualHeight),
            GetWorkAreaFor(host),
            visible);

        foreach (var (id, pos) in placements)
        {
            _windows[id].Left = pos.left;
            _windows[id].Top = pos.top;
        }
    }

    // [멀티모니터] 키보드 창이 있는 모니터의 작업 영역을 구합니다 —
    // SystemParameters.WorkArea는 1번 모니터만 다루므로, 부 모니터에서 패널이 잘못 뜨는 문제를 막습니다 (2026-11).
    private static Rect GetWorkAreaFor(Window host)
    {
        var hwnd = new WindowInteropHelper(host).Handle;
        if (hwnd == IntPtr.Zero || !MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST, out var monitor))
            return SystemParameters.WorkArea;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, out info))
            return SystemParameters.WorkArea;

        return new Rect(
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right - info.rcWork.Left,
            info.rcWork.Bottom - info.rcWork.Top);
    }

    // ── Win32: 모니터 작업 영역 조회 ────────────────────────────────

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MonitorFromWindow(IntPtr hwnd, uint dwFlags, out IntPtr lphMonitor);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, out MONITORINFO lpmi);
}
