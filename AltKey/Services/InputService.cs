using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using AltKey.Models;
using AltKey.Platform;
using WpfApp = System.Windows.Application;
using WpfClipboard = System.Windows.Clipboard;

namespace AltKey.Services;

public enum InputMode
{
    Unicode,
    VirtualKey
}

public class InputService
{
    // ── 입력 모드: 관리자 권한이면 VirtualKey, 아니면 Unicode ──────────────
    public InputMode Mode { get; } = CheckElevated() ? InputMode.VirtualKey : InputMode.Unicode;

    // ── Unicode 모드에서 화면에 전송한 조합 문자열 길이 추적 ──────────────
    public int TrackedOnScreenLength { get; set; }

    // ── T-9.3: 자동 완성 서비스 (옵셔널) ────────────────────────────────────
    private AutoCompleteService? _autoComplete;

    /// 자동 완성 서비스를 연결한다 (App.xaml.cs 초기화 이후 DI 에서 주입).
    public void SetAutoComplete(AutoCompleteService svc) => _autoComplete = svc;

    private static bool CheckElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    // ── Sticky / Lock 상태 ────────────────────────────────────────────────────
    private readonly HashSet<VirtualKeyCode> _stickyKeys = [];
    private readonly HashSet<VirtualKeyCode> _lockedKeys = [];

    public IReadOnlySet<VirtualKeyCode> StickyKeys => _stickyKeys;
    public IReadOnlySet<VirtualKeyCode> LockedKeys => _lockedKeys;

    // ── 이벤트 ───────────────────────────────────────────────────────────────
    /// Sticky / Lock 상태 변경 시 발생 (UI 갱신용)
    public event Action? StickyStateChanged;

    /// SendInput이 ERROR_ACCESS_DENIED를 반환했을 때 발생 (T-2.10)
    public event Action? ElevatedAppDetected;

    /// T-2.10b: 외부에서 관리자 권한 앱 감지를 알릴 때 호출
    public void NotifyElevatedApp() => ElevatedAppDetected?.Invoke();

    // ── T-2.7: Caps Lock 상태 조회 ──────────────────────────────────────────
    public bool IsCapsLockOn => (Win32.GetKeyState((int)VirtualKeyCode.VK_CAPITAL) & 0x0001) != 0;

    // Unicode 모드에서 조합키(Ctrl+C 등) 판별용
    public bool HasActiveModifiers =>
        _stickyKeys.Count > 0 || _lockedKeys.Count > 0;

    /// 포그라운드 창의 IME 한/영 상태를 IMM32 API로 조회한다.
    /// AttachThreadInput 없이 GetGUIThreadInfo + ImmGetDefaultIMEWnd로
    /// 타겟 프로그램 IME 상태를 읽어온다 (포커스 탈취 방지).
    public bool IsImeKorean()
    {
        try
        {
            var hwnd = Win32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return true;

            uint fgThreadId = Win32.GetWindowThreadProcessId(hwnd, out _);

            // 포커스된 컨트롤 HWND 획득 (타겟 프로그램의 실제 텍스트 입력 창)
            IntPtr targetHwnd = IntPtr.Zero;

            if (fgThreadId != 0)
            {
                var guiInfo = new Win32.GUITHREADINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.GUITHREADINFO>() };
                if (Win32.GetGUIThreadInfo(fgThreadId, ref guiInfo))
                {
                    if (guiInfo.hwndFocus != IntPtr.Zero)
                        targetHwnd = guiInfo.hwndFocus;
                    else if (guiInfo.hwndActive != IntPtr.Zero)
                        targetHwnd = guiInfo.hwndActive;
                }
            }

            if (targetHwnd == IntPtr.Zero)
                targetHwnd = hwnd;

            // IMM32 API로 IME 상태 조회 (AttachThreadInput 없이)
            IntPtr hIMEWnd = Win32.ImmGetDefaultIMEWnd(targetHwnd);
            if (hIMEWnd != IntPtr.Zero)
            {
                IntPtr hIMC = Win32.ImmGetContext(hIMEWnd);
                if (hIMC != IntPtr.Zero)
                {
                    Win32.ImmGetConversionStatus(hIMC, out uint conversion, out _);
                    Win32.ImmReleaseContext(hIMEWnd, hIMC);
                    return (conversion & Win32.IME_CMODE_NATIVE) != 0;
                }
            }

            return true;
        }
        catch
        {
            return true;
        }
    }

    // ── T-2.4: 단일 키 전송 ──────────────────────────────────────────────────
    public void SendKeyPress(VirtualKeyCode vk)
    {
        var inputs = new Win32.INPUT[] { MakeKeyDown((ushort)vk), MakeKeyUp((ushort)vk) };
        DispatchInput(inputs);
    }

    public virtual void SendKeyDown(VirtualKeyCode vk)
        => DispatchInput([MakeKeyDown((ushort)vk)]);

    public virtual void SendKeyUp(VirtualKeyCode vk)
        => DispatchInput([MakeKeyUp((ushort)vk)]);

    // ── T-2.10: Win32 SendInput 래퍼 (권한 오류 감지) ────────────────────────
    private void DispatchInput(Win32.INPUT[] inputs)
    {
        uint sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        if (sent == 0 && Marshal.GetLastWin32Error() == Win32.ERROR_ACCESS_DENIED)
            ElevatedAppDetected?.Invoke();
    }

    // ── T-2.5: 고정 키(Sticky Keys) 상태 관리 ────────────────────────────────
    /// 수식자 키 토글: 미고정 → 일회성 고정 → 영구 잠금 → 해제 순환
    public void ToggleModifier(VirtualKeyCode vk)
    {
        if (_lockedKeys.Contains(vk))
        {
            _lockedKeys.Remove(vk);
            _stickyKeys.Remove(vk);
            SendKeyUp(vk);
        }
        else if (_stickyKeys.Contains(vk))
        {
            _lockedKeys.Add(vk);
            // 영구 잠금 진입 — KeyDown 이미 유지 중
        }
        else
        {
            _stickyKeys.Add(vk);
            SendKeyDown(vk);
        }

        StickyStateChanged?.Invoke();
    }

    /// 일반 키 입력 후 일회성 고정 수식자를 해제한다 (잠금 키는 유지).
    internal void ReleaseTransientModifiers()
    {
        var transient = _stickyKeys.Except(_lockedKeys).ToList();
        foreach (var mod in transient)
        {
            SendKeyUp(mod);
            _stickyKeys.Remove(mod);
        }
        if (transient.Count > 0)
            StickyStateChanged?.Invoke();
    }

    // ── T-2.6: KeyAction 디스패처 ────────────────────────────────────────────
    public void HandleAction(KeyAction action)
    {
        switch (action)
        {
            case SendKeyAction { Vk: var vkStr }:
                if (Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
                {
                    SendKeyPress(vk);
                    ReleaseTransientModifiers();
                }
                break;

            case SendComboAction { Keys: var keys }:
                var vkList = keys
                    .Select(k => Enum.TryParse<VirtualKeyCode>(k, out var v) ? (VirtualKeyCode?)v : null)
                    .Where(v => v.HasValue)
                    .Select(v => v!.Value)
                    .ToList();
                SendCombo(vkList);
                break;

            case ToggleStickyAction { Vk: var vkStr2 }:
                if (Enum.TryParse<VirtualKeyCode>(vkStr2, out var modVk))
                    ToggleModifier(modVk);
                break;

            // ── T-9.1 신규 액션 핸들러 ───────────────────────────────────────

            case RunAppAction { Path: var path, Args: var args }:
                try { Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = true }); }
                catch (Exception ex) { Debug.WriteLine($"[RunApp] 실패: {path} — {ex.Message}"); }
                break;

            case BoilerplateAction { Text: var bText }:
                SendUnicode(bText);
                break;

            case ShellCommandAction { Command: var cmd, Shell: var shell, Hidden: var hidden }:
                var shellExe = shell == "powershell" ? "powershell.exe" : "cmd.exe";
                var shellArg = shell == "powershell" ? $"-Command \"{cmd}\"" : $"/c \"{cmd}\"";
                var psi = new ProcessStartInfo(shellExe, shellArg)
                {
                    UseShellExecute = false,
                    CreateNoWindow  = hidden
                };
                try { Process.Start(psi); }
                catch (Exception ex) { Debug.WriteLine($"[ShellCommand] 실패: {cmd} — {ex.Message}"); }
                break;

            case VolumeControlAction { Direction: var dir, Step: var step }:
                HandleVolumeControl(dir, step);
                break;

            case ClipboardPasteAction { Text: var pasteText }:
                WpfApp.Current.Dispatcher.Invoke(() => WpfClipboard.SetText(pasteText));
                SendCombo([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_V]);
                break;
        }
    }

    // ── T-9.1: 볼륨 제어 (VK_VOLUME_UP / DOWN / MUTE 반복 전송) ────────────
    private void HandleVolumeControl(string direction, int step)
    {
        var vk = direction switch
        {
            "up"   => (ushort)0xAF, // VK_VOLUME_UP
            "down" => (ushort)0xAE, // VK_VOLUME_DOWN
            "mute" => (ushort)0xAD, // VK_VOLUME_MUTE
            _      => (ushort)0
        };
        if (vk == 0) return;

        // step/2 횟수만큼 반복 전송 (볼륨 키는 보통 2씩 변화)
        int repeat = Math.Max(1, step / 2);
        for (int i = 0; i < repeat; i++)
            SendKeyPress((VirtualKeyCode)vk);
    }

    private void SendCombo(List<VirtualKeyCode> keys)
    {
        foreach (var k in keys) SendKeyDown(k);
        foreach (var k in Enumerable.Reverse(keys)) SendKeyUp(k);
        ReleaseTransientModifiers();
    }

    // ── T-8.3: 유니코드 문자 전송 (이모지 지원) ─────────────────────────────
    public void SendUnicode(string text)
    {
        var inputs = new List<Win32.INPUT>();
        foreach (var ch in text)
        {
            inputs.Add(MakeUnicodeKeyDown(ch));
            inputs.Add(MakeUnicodeKeyUp(ch));
        }
        DispatchInput(inputs.ToArray());
        ReleaseTransientModifiers();
    }

    // ── Unicode 모드: 이전 출력을 백스페이스로 지우고 새 출력을 원자적 전송 ──
    public void SendAtomicReplace(int prevLen, string newOutput)
    {
        var inputs = new List<Win32.INPUT>();
        for (int i = 0; i < prevLen; i++)
        {
            inputs.Add(MakeKeyDown((ushort)VirtualKeyCode.VK_BACK));
            inputs.Add(MakeKeyUp((ushort)VirtualKeyCode.VK_BACK));
        }
        foreach (var ch in newOutput)
        {
            inputs.Add(MakeUnicodeKeyDown(ch));
            inputs.Add(MakeUnicodeKeyUp(ch));
        }
        if (inputs.Count > 0)
            DispatchInput(inputs.ToArray());
        TrackedOnScreenLength = newOutput.Length;
    }

    private static Win32.INPUT MakeUnicodeKeyDown(char ch) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = 0, WScan = ch, DwFlags = Win32.KEYEVENTF_UNICODE } }
    };

    private static Win32.INPUT MakeUnicodeKeyUp(char ch) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = 0, WScan = ch, DwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP } }
    };

    // ── 내부 헬퍼 ────────────────────────────────────────────────────────────
    private static Win32.INPUT MakeKeyDown(ushort vk) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = vk } }
    };

    private static Win32.INPUT MakeKeyUp(ushort vk) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = vk, DwFlags = Win32.KEYEVENTF_KEYUP } }
    };
}
