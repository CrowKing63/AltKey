using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

using AltKey.Services;
using AltKey.ViewModels;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AltKey.Views;

/// <summary>
/// [역할] 클립보드 히스토리 패널의 독립 창입니다 (D2 — ScratchPadWindow와 같은 차원의 top-level 보조 패널).
/// [기능] 핀 칩 스트립(MRU) + '최근' 리스트 동시 표시, 정렬 순서 전환(최신순↔오래된 순), Esc 닫기, 행 액션(★ 고정·✕ 삭제).
/// [참고] ✕는 창을 닫지 않고 숨김만 시킵니다 — Show/Hide는 AnchoredPanelService가 관리합니다.
/// 드래그 핸들·포그라운드 Win32는 공용 기반 클래스(AnchoredPanelWindowBase)에서 제공합니다.
/// </summary>
public partial class ClipboardPanelWindow : AnchoredPanelWindowBase
{
    private readonly ClipboardViewModel _viewModel;

    // 열려 있는 동안 상대 시간 라벨("N분 전")이 흐르도록 30초마다 갱신 — 목록은 재구성하지 않아 스크롤 위치 유지.
    private System.Windows.Threading.DispatcherTimer? _timeRefreshTimer;

    // [붙여넣기 대상] 이 창이 포커스를 가져가기 직전의 외부 앱 창과 그 안의 입력 컨트롤 HWND를 함께 보관한다.
    // 선택을 마친 뒤 창만 되살리면 Explorer 검색 필드 같은 하위 입력 컨트롤의 포커스가 사라질 수 있어,
    // 붙여넣기 전 대상 앱과 원래 입력 컨트롤을 복원한다.
    private IntPtr _previousForeground = IntPtr.Zero;
    private bool _capturedTargetIsScratchPad;
    private IntPtr _capturedFocusTarget = IntPtr.Zero;
    private IntPtr _capturedFocusWindow = IntPtr.Zero;

    /// <summary>숨김 시 보존해야 하는 내부 입력 대상 창 핸들을 돌려줍니다 (예: 스크래치패드).</summary>
    public Func<IntPtr>? GetInputTargetToPreserveOnHide { get; set; }

    /// <summary>✕ 버튼·Esc 클릭 시 호출됩니다 (숨김 요청).</summary>
    public event Action? OnCloseRequested;

    public ClipboardPanelWindow(ClipboardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // "패널 닫기" 요청 수렴점: ✕ / Esc / '붙여넣기 후 닫기' 설정이 모두 여기로 모입니다.
        _viewModel.RequestClose = () => OnCloseRequested?.Invoke();

        _timeRefreshTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timeRefreshTimer.Tick += (_, _) => _viewModel.RefreshTimes();

        // WPF Window에는 Shown/Hidden 이벤트가 없어 IsVisibleChanged로 열림/숨김을 감지합니다.
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue)
            {
                _timeRefreshTimer.Start();
                // [2026-11] 열 때마다 스크롤 위치 초기화 — Hide/Show 사이 시각 트리는 살아서 칩 스트립 가로·
                // 최근 리스트 세로 오프셋이 그대로 남는다. 레이아웃(배치)이 끝난 후(Loaded)에 0으로 되돌린다.
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                {
                    ChipStripScroll.ScrollToHorizontalOffset(0);
                    HistoryListScroll.ScrollToVerticalOffset(0);
                });
            }
            else
            {
                _timeRefreshTimer.Stop();
                var currentForeground = GetForegroundWindow();
                var preservedTarget = GetInputTargetToPreserveOnHide?.Invoke() ?? IntPtr.Zero;
                if (ShouldRestoreInputForegroundOnHide(currentForeground, preservedTarget))
                    RestoreInputForeground(); // 붙여넣기 대상이 따로 없으면 열기 전 외부 앱으로 되돌린다
            }
        };
    }

    /// <summary>Show() 전에 호출 — 이 창이 포커스를 가져가기 직전의 포그라운드(외부 앱)을 기억합니다.</summary>
    public void CapturePreviousForeground(IntPtr preferredOwnTarget = default)
    {
        var fg = GetForegroundWindow();
        bool capturedPreferredTarget = fg != IntPtr.Zero && fg == preferredOwnTarget;
        // 화상 키보드 상단 버튼·Win+. 등 AltKey 자체에서 열면 일반적으로 외부 앱이 직전 입력 대상입니다.
        // 다만 빠른 메모 창이 실제 포그라운드라면 그 창은 명시적 붙여넣기 대상이므로 보존합니다.
        if (fg != IntPtr.Zero && IsOwnProcessWindow(fg) && !capturedPreferredTarget)
            fg = App.Services?.GetService<ProfileService>()?.LastExternalForegroundHwnd ?? IntPtr.Zero;

        _previousForeground = fg;
        _capturedTargetIsScratchPad = capturedPreferredTarget;
        _capturedFocusTarget = capturedPreferredTarget ? IntPtr.Zero : fg;
        _capturedFocusWindow = capturedPreferredTarget ? IntPtr.Zero : GetThreadFocusedWindow(fg);
    }

    /// <summary>붙여넣기(Ctrl+V) 전송 전 호출 — 포그라운드를 외부 앱으로 복원합니다.
    /// 포커스가 이미 외부 앱에 있으면 그쪽이 붙여넣기 대상이라 건드리지 않습니다.</summary>
    internal bool RestoreInputForeground()
    {
        if (_previousForeground == IntPtr.Zero)
            return false;

        var fg = GetForegroundWindow();
        bool currentIsOwn = fg != IntPtr.Zero && IsOwnProcessWindow(fg);
        var latestExternal = App.Services?.GetService<ProfileService>()?.LastExternalForegroundHwnd ?? IntPtr.Zero;
        var target = SelectRestoreTarget(
            fg,
            _previousForeground,
            latestExternal,
            _capturedTargetIsScratchPad,
            currentIsOwn,
            IsWindowValid);

        // 외부 앱이 이미 포그라운드면 그 앱의 현재 포커스를 보존하고, 앱이 AltKey로 바뀐 경우에만 이전 포커스를 복원합니다.
        if (target == IntPtr.Zero)
            return false;

        bool restored = EnsureForeground(target);
        if (!restored)
            return false;

        var focusTarget = currentIsOwn
            ? SelectCapturedFocusTarget(
                target,
                _capturedFocusTarget,
                _capturedFocusWindow,
                _capturedTargetIsScratchPad,
                hwnd => IsWindowValid(hwnd) && IsChildWindow(target, hwnd))
            : IntPtr.Zero;
        bool focusRestored = focusTarget == IntPtr.Zero || RestoreChildFocus(target, focusTarget);
        _previousForeground = target;
        return focusRestored;
    }

    /// <summary>포그라운드와 마지막 외부 창을 기준으로 붙여넣기 대상을 고릅니다.</summary>
    internal static IntPtr SelectRestoreTarget(
        IntPtr currentForeground,
        IntPtr capturedForeground,
        IntPtr latestExternalForeground,
        bool capturedIsScratchPad,
        bool isCurrentForegroundOwn,
        Func<IntPtr, bool> isValidWindow)
    {
        if (!isCurrentForegroundOwn && isValidWindow(currentForeground))
            return currentForeground;
        if (capturedIsScratchPad && isValidWindow(capturedForeground))
            return capturedForeground;
        if (isValidWindow(latestExternalForeground))
            return latestExternalForeground;
        return isValidWindow(capturedForeground) ? capturedForeground : IntPtr.Zero;
    }

    internal static IntPtr SelectCapturedFocusTarget(
        IntPtr restoreTarget,
        IntPtr capturedTarget,
        IntPtr capturedFocus,
        bool capturedIsScratchPad,
        Func<IntPtr, bool> isValidChild)
    {
        if (capturedIsScratchPad || restoreTarget == IntPtr.Zero || restoreTarget != capturedTarget)
            return IntPtr.Zero;
        if (capturedFocus == IntPtr.Zero || capturedFocus == capturedTarget || !isValidChild(capturedFocus))
            return IntPtr.Zero;
        return capturedFocus;
    }

    internal static bool ShouldRestoreInputForegroundOnHide(IntPtr currentForeground, IntPtr scratchPadWindow) =>
        scratchPadWindow == IntPtr.Zero || currentForeground != scratchPadWindow;

    private static IntPtr GetThreadFocusedWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        var info = new GuiThreadInfo { cbSize = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        return threadId != 0 && GetGUIThreadInfo(threadId, ref info) ? info.hwndFocus : IntPtr.Zero;
    }

    private static bool RestoreChildFocus(IntPtr target, IntPtr focusWindow)
    {
        uint targetThread = GetWindowThreadProcessId(target, out _);
        uint currentThread = GetCurrentThreadId();
        if (targetThread == 0 || !IsChildWindow(target, focusWindow)) return false;

        bool attached = targetThread == currentThread || AttachThreadInput(currentThread, targetThread, true);
        if (!attached) return false;
        try
        {
            SetFocus(focusWindow);
            return GetThreadFocusedWindow(target) == focusWindow;
        }
        finally
        {
            if (targetThread != currentThread)
                AttachThreadInput(currentThread, targetThread, false);
        }
    }

    private static bool IsChildWindow(IntPtr parent, IntPtr child) =>
        child != IntPtr.Zero && IsChild(parent, child);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public int caretLeft;
        public int caretTop;
        public int caretRight;
        public int caretBottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetGUIThreadInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", EntryPoint = "SetFocus")]
    private static extern IntPtr SetFocus(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "IsChild")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsChild(IntPtr parent, IntPtr child);

    [DllImport("user32.dll", EntryPoint = "AttachThreadInput")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint fromThread, uint toThread, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    // [Win32 헬퍼] ForceForegroundViaThreadAttach·포그라운드 P/Invoke는
    // 공용 기반 클래스(AnchoredPanelWindowBase)로 이관 — 두 패널 간 분기를 막기 위함.

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // XAML의 Topmost="True"를 SetWindowPos로 한 번 더 강화합니다 — MainWindow·ScratchPad와 같은 패턴.
        App.Services?.GetService<WindowService>()?.SetTopmost(this, true);

        // 포커스 추적기에 현재 창을 등록해 보조 기능 모듈이 활성 창을 알 수 있게 한다 (다른 AltKey 창과 같은 패턴).
        FocusTracker.Register(this);
    }

    // ── 닫기 단축키 (D5 축소) ──────────────────────────────────────────
    // [2026-11] 키보드 내비게이션(↑↓ 이동·Enter 붙여넣기·열림 시 자동 선택)은 핀 칩 UI로 대체되어 제거했고,
    // 남은 것은 Esc = 패널 닫기뿐이다. (이 핸들러는 XAML 루트의 PreviewKeyDown으로 배선됨.)

    private void Window_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        OnCloseRequested?.Invoke();
        e.Handled = true;
    }

    // ── 헤더 버튼 (코드비핸드: 파라미터가 없어 유지 — 행 액션은 XAML에서 커맨드로 바인딩) ────────────

    private void ToggleSort_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleSortOrderCommand.Execute(null);
        PlayButtonSound(); // [사운드] 동작 실행 후에만 재생 — 중간에 끼어든 문제가 있어도 버튼 동작은 보장된다
    }

    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearHistoryCommand.Execute(null);
        PlayButtonSound(); // [사운드] 동작 실행 후에만 재생 — 중간에 끼어든 문제가 있어도 버튼 동작은 보장된다
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        OnCloseRequested?.Invoke();
        PlayButtonSound();
    }

    /// <summary>키보드 버튼과 같은 효과음을 재생합니다.</summary>
    private static void PlayButtonSound() => App.Services?.GetService<SoundService>()?.Play();

    // ── 헤더 드래그 핸들 — 공용 기반 클래스(AnchoredPanelWindowBase)에서 제공합니다:
    //    더블클릭 = 수평 중앙, 누르면 이동. XAML의 DragHandle_* 이벤트가 그대로 베이스 메서드에 바인딩됩니다.
}

/// <summary>정렬 순서 → 버튼 라벨: 내림차순(최신순) → "최신순", 오름차순(오래된 순) → "오래순". 현재 순서를 표시합니다.</summary>
public class SortOrderToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "최신순" : "오래순";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
