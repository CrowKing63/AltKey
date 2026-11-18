using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace AltKey.Views;

/// <summary>
/// [역할] 키보드 창에 앵커되는 독립 패널 창(빠른 메모·클립보드)의 공통 기반 클래스.
/// [목적] 두 패널이 드래그 핸들·포그라운드 복원 로직을 각자 구현하다 보니 치수가 달라지거나
///        동작이 어긋나는 문제가 반복되어(2026-11 드래그 바 두께·헤더 높이 분기), "창 관리"를 여기서 통합한다.
/// [제공] ① 드래그 핸들: 누르면 이동, 더블클릭은 화면 수평 중앙 — XAML 이벤트 이름(DragHandle_*)을
///        서브클래스 그대로 쓰도록 고정했다. ② Win32 포그라운드 조회·설정(동기 검증 포함),
///        자기 프로세스 창 판별, 강제 활성화 폴백. 서브클래스는 내용·버튼만 구현한다.
/// </summary>
public class AnchoredPanelWindowBase : Window
{
    // ── 드래그 핸들: 누르면 이동, 더블클릭은 수평 중앙 (빠른 메모·클립보드 공용) ────────────────

    // 드래그 시작 여부와 누른 위치 — 5px 이상 움직여야 이동으로 인정(클릭/더블클릭과 구별).
    private bool _isDragHandlePressed;
    private System.Windows.Point _dragHandlePressPoint;

    /// <summary>드래그 진행 상태 — XAML에서 두 번 쓰지 않으므로 코드에 둔다.</summary>
    public bool IsDragging { get; set; }

    public void DragHandle_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            CenterWindowHorizontally(); // 더블 클릭 = 창을 화면 수평 중앙으로 배치
            _isDragHandlePressed = false;
            e.Handled = true;
            return;
        }

        _isDragHandlePressed = true;
        _dragHandlePressPoint = e.GetPosition(this);
    }

    public void DragHandle_MouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragHandlePressed) return;

        // 더블 클릭으로 창이 이동한 뒤 마우스 단추 상태가 어긋난 MouseMove가 올 수 있음 —
        // 그때 DragMove()가 예외를 던지므로(2026-11-15 보고), 단추가 실제로 눌려 있을 때만 호출한다.
        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            _isDragHandlePressed = false; // 상태가 어긋났으니 정리하고 다음 동작을 기다린다
            return;
        }

        System.Windows.Point currentPos = e.GetPosition(this);
        if (!IsDragging && Math.Abs(currentPos.X - _dragHandlePressPoint.X) > 5)
        {
            IsDragging = true;
            DragMove(); // WPF가 마우스 캡처·이동을 알아서 처리한다.
        }
    }

    public void DragHandle_MouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
    {
        _isDragHandlePressed = false;
        IsDragging = false;
    }

    public void DragHandle_LostMouseCapture(object? sender, EventArgs e)
    {
        // 드래그 중 마우스 캡처를 잃으면(창 밖에서 놓음 등) 상태 정리
        _isDragHandlePressed = false;
        IsDragging = false;
    }

    /// <summary>더블클릭 동작: 창을 작업 영역 안에서 좌우 수평 중앙으로 배치합니다. 세로 위치는 그대로 유지.</summary>
    protected void CenterWindowHorizontally()
    {
        var workArea = SystemParameters.WorkArea;
        // SourceInitialized(첫 렌더 전)엔 ActualWidth가 NaN — 그땐 설정된 Width를 쓴다.
        double w = !double.IsNaN(ActualWidth) ? ActualWidth : (Width > 0 ? Width : 1);
        Left = workArea.Left + (workArea.Width - w) / 2;
    }

    // ── Win32: 포그라운드 조회·설정 (패널 간 공통) ────────────────────────────────

    public static IntPtr GetForegroundWindow() => GetForegroundWindowP();

    /// <summary>
    /// 대상 창을 포그라운드로 설정하고, 실제로 포그라운드가 될 때까지 대기한다 (동기 검증).
    /// Windows가 전환을 거부하면(반려 실패) 스레드 첨부으로 강제 전환한다 — 클립보드 매니저들이 쓰는 표준 기법.
    /// 성공 시 true. SetForegroundWindow 직후 바로 키를 보내면 활성화가 완료되기 전이라
    /// 키가 엉뚱한 창에 가는 레이스를 막는다.
    /// </summary>
    public static bool EnsureForeground(IntPtr target)
    {
        if (target == IntPtr.Zero || !IsWindowP(target)) return false;

        bool setOk = SetForegroundWindowP(target);
        if (!setOk)
            ForceForegroundViaThreadAttach(target);

        var deadline = Environment.TickCount64 + 400;
        while (GetForegroundWindowP() != target && Environment.TickCount64 < deadline)
            Thread.Sleep(10);

        return GetForegroundWindowP() == target;
    }

    /// <summary>해당 창이 AltKey(자기 프로세스)의 것인지 확인한다.</summary>
    public static bool IsOwnProcessWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Environment.ProcessId;
    }

    /// <summary>핸들이 아직 유효한 창인지 확인한다 (대상 앱이 종료하면 false).</summary>
    public static bool IsWindowValid(IntPtr hwnd) => hwnd != IntPtr.Zero && IsWindowP(hwnd);

    /// <summary>[진단] 핸들 → "title='...' pid=..." — 포커스 대상이 어떤 창인지 식별한다.</summary>
    public static string DescribeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "(none)";
        var sb = new StringBuilder(GetWindowTextLengthP(hwnd) + 1);
        GetWindowTextP(hwnd, sb, sb.Capacity);
        GetWindowThreadProcessId(hwnd, out var pid);
        return $"title='{sb}' pid={pid}";
    }

    /// <summary>[폴백] SetForegroundWindow가 Windows에 거부된 경우 AttachThreadInput으로 강제 전환 — 클립보드 매니저들이 쓰는 표준 기법.</summary>
    private static void ForceForegroundViaThreadAttach(IntPtr target)
    {
        GetWindowThreadProcessId(target, out var targetThread);
        var ourThread = GetCurrentThreadId();
        if (targetThread == 0 || targetThread == ourThread) return;

        AttachThreadInputP(ourThread, targetThread, true);
        SetForegroundWindowP(target);
        AttachThreadInputP(ourThread, targetThread, false);
    }

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr GetForegroundWindowP();

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindowP(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "IsWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowP(IntPtr hWnd);

    [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", EntryPoint = "AttachThreadInput")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInputP(uint from, uint to, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static extern int GetWindowTextLengthP(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextP(IntPtr hWnd, StringBuilder str, int maxCount);
}
