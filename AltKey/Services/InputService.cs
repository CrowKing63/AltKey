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
    private readonly bool _isElevated;
    private static readonly uint _ownProcessId = (uint)Environment.ProcessId;

    public InputMode Mode { get; private set; }

    public bool IsElevated => _isElevated;

    /// 포그라운드 윈도우가 AltKey 프로세스 소유인지 확인.
    /// true면 한글 조합을 건너뛰고 가상 키만 전송.
    public bool IsForegroundOwnWindow()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == _ownProcessId;
    }

    public event Action<InputMode>? ModeChanged;

    public InputService()
    {
        _isElevated = CheckElevated();
        Mode = _isElevated ? InputMode.VirtualKey : InputMode.Unicode;
    }

    public bool TrySetMode(InputMode target)
    {
        if (_isElevated && target == InputMode.Unicode)
            return false;

        if (Mode == target) return true;
        Mode = target;
        ModeChanged?.Invoke(Mode);
        return true;
    }

    // ── Unicode 모드에서 화면에 전송한 조합 문자열 길이 추적 ──────────────
    public int TrackedOnScreenLength { get; set; }

    /// 조합 완료(공백/엔터 등) 후 추적 길이를 리셋.
    public void ResetTrackedLength() => TrackedOnScreenLength = 0;

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

    /// Shift만 활성된 경우는 false. 한국어 쌍자음/쌍모음 입력과 "조합키"를 구분하기 위해 사용.
    public bool HasActiveModifiersExcludingShift
    {
        get
        {
            foreach (var vk in _stickyKeys)
            {
                if (vk is not VirtualKeyCode.VK_SHIFT and not VirtualKeyCode.VK_LSHIFT and not VirtualKeyCode.VK_RSHIFT)
                    return true;
            }
            foreach (var vk in _lockedKeys)
            {
                if (vk is not VirtualKeyCode.VK_SHIFT and not VirtualKeyCode.VK_LSHIFT and not VirtualKeyCode.VK_RSHIFT)
                    return true;
            }
            return false;
        }
    }

    // ── T-2.4: 단일 키 전송 ──────────────────────────────────────────────────
    public virtual void SendKeyPress(VirtualKeyCode vk)
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

    /// 모든 수식자 키(sticky + locked)를 즉시 해제한다. 창 닫힘/숨김 시 호출.
    public void ReleaseAllModifiers()
    {
        foreach (var mod in _stickyKeys.Union(_lockedKeys))
            SendKeyUp(mod);
        _stickyKeys.Clear();
        _lockedKeys.Clear();
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

    public void SendCombo(List<VirtualKeyCode> keys)
    {
        foreach (var k in keys) SendKeyDown(k);
        foreach (var k in Enumerable.Reverse(keys)) SendKeyUp(k);
        ReleaseTransientModifiers();
    }

    public virtual void SendUnicode(string text)
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
    public virtual void SendAtomicReplace(int prevLen, string newOutput)
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
        ReleaseTransientModifiers();
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
