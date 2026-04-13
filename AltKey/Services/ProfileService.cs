using System.Diagnostics;
using AltKey.Platform;

namespace AltKey.Services;

/// T-5.3: WinEventHook으로 포그라운드 앱 전환 이벤트 구독
public class ProfileService : IDisposable
{
    private IntPtr _hook;
    private Win32.WinEventDelegate? _delegateRef; // GC 방지용 참조 보관

    public event Action<string>? ForegroundAppChanged;

    public void Start()
    {
        _delegateRef = OnWinEvent;
        _hook = Win32.SetWinEventHook(
            0x0003,         // EVENT_SYSTEM_FOREGROUND
            0x0003,
            IntPtr.Zero,
            _delegateRef,
            0, 0,
            0x0000);        // WINEVENT_OUTOFCONTEXT
    }

    private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd,
        int idObj, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero) return;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            ForegroundAppChanged?.Invoke(proc.ProcessName.ToLower() + ".exe");
        }
        catch { /* 프로세스가 종료된 경우 무시 */ }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            Win32.UnhookWinEvent(_hook);
    }
}
