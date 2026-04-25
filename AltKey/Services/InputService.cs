using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using AltKey.Models;
using AltKey.Platform;
using WpfApp = System.Windows.Application;

namespace AltKey.Services;

/// <summary>
/// [역할] 윈도우 시스템에 가상 키 입력이나 유니코드 문자를 실제로 전달하는 서비스입니다.
/// [기능] 키 누름/떼기 송신, 조합키(Ctrl, Alt 등) 상태 관리, 관리자 권한 앱 감지 등을 수행합니다.
/// </summary>
public enum InputMode
{
    Unicode,    // 한글 조합 등에 사용되는 문자 전송 방식
    VirtualKey  // 일반적인 게임이나 특수 프로그램에서 사용하는 키 번호 전송 방식
}

public class InputService
{
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;
    private static readonly IntPtr InputExtraInfoTag =
        unchecked((IntPtr)(long)Win32.INPUT_EXTRAINFO_ALTKEY);
    
    // 이 키들이 눌려있으면 시스템 제어에 큰 영향을 줄 수 있으므로 주의 깊게 관리합니다.
    private static readonly HashSet<VirtualKeyCode> HighRiskModifiers =
    [
        VirtualKeyCode.VK_CONTROL,
        VirtualKeyCode.VK_LCONTROL,
        VirtualKeyCode.VK_RCONTROL,
        VirtualKeyCode.VK_MENU,
        VirtualKeyCode.VK_LMENU,
        VirtualKeyCode.VK_RMENU,
        VirtualKeyCode.VK_LWIN,
        VirtualKeyCode.VK_RWIN,
    ];

    private readonly bool _isElevated;
    private readonly HashSet<VirtualKeyCode> _stickyKeys = []; // 한 번 클릭하면 눌린 상태가 유지되는 키
    private readonly HashSet<VirtualKeyCode> _lockedKeys = []; // 두 번 클릭하여 고정된 상태인 키

    public InputMode Mode { get; private set; }
    public bool IsElevated => _isElevated;
    public int TrackedOnScreenLength { get; set; } // 현재 화면에 조합 중인 글자 수 (지울 때 사용)

    public IReadOnlySet<VirtualKeyCode> StickyKeys => _stickyKeys;
    public IReadOnlySet<VirtualKeyCode> LockedKeys => _lockedKeys;

    public event Action<InputMode>? ModeChanged;
    public event Action? StickyStateChanged;
    public event Action? ElevatedAppDetected;

    public InputService()
    {
        _isElevated = CheckElevated();
        // 관리자 권한으로 실행 중이면 보안을 위해 VirtualKey 모드를 기본으로 사용합니다.
        Mode = _isElevated ? InputMode.VirtualKey : InputMode.Unicode;
    }

    /// 현재 포커스가 AltKey 창 자체에 있는지 확인합니다.
    public bool IsForegroundOwnWindow()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == OwnProcessId;
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

    public void ResetTrackedLength() => TrackedOnScreenLength = 0;
    public void NotifyElevatedApp() => ElevatedAppDetected?.Invoke();
    public bool IsCapsLockOn => (Win32.GetKeyState((int)VirtualKeyCode.VK_CAPITAL) & 0x0001) != 0;
    public bool HasActiveModifiers => _stickyKeys.Count > 0 || _lockedKeys.Count > 0;

    /// <summary>
    /// Shift를 제외한 다른 보조키(Ctrl, Alt 등)가 눌려있는지 확인합니다.
    /// </summary>
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

    /// 가상 키보드에서 키를 한 번 눌렀다 떼는 동작을 시뮬레이션합니다.
    public virtual void SendKeyPress(VirtualKeyCode vk)
    {
        var inputs = new Win32.INPUT[] { MakeKeyDown((ushort)vk), MakeKeyUp((ushort)vk) };
        DispatchInput(inputs);
    }

    public virtual void SendKeyDown(VirtualKeyCode vk)
    {
        DispatchInput([MakeKeyDown((ushort)vk)]);
    }

    public virtual void SendKeyUp(VirtualKeyCode vk)
    {
        DispatchInput([MakeKeyUp((ushort)vk)]);
    }

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
        }
        else
        {
            _stickyKeys.Add(vk);
            SendKeyDown(vk);
        }
        StickyStateChanged?.Invoke();
    }

    /// <summary>
    /// 일시적으로 눌려있던(고정되지 않은) 보조키들을 모두 뗍니다.
    /// 보통 글자 하나를 입력한 직후에 호출됩니다.
    /// </summary>
    internal void ReleaseTransientModifiers(string reason = "input-complete")
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

    /// 모든 보조키 상태를 강제로 해제합니다.
    public virtual void ReleaseAllModifiers(string reason = "manual")
    {
        var active = _stickyKeys.Union(_lockedKeys).Distinct().ToList();
        foreach (var mod in active)
            SendKeyUp(mod);

        _stickyKeys.Clear();
        _lockedKeys.Clear();
        StickyStateChanged?.Invoke();
    }

    public virtual void ReleaseHighRiskModifiers(string reason)
    {
        var released = _stickyKeys
            .Union(_lockedKeys)
            .Where(IsHighRiskModifier)
            .Distinct()
            .ToList();

        foreach (var mod in released)
        {
            SendKeyUp(mod);
            _stickyKeys.Remove(mod);
            _lockedKeys.Remove(mod);
        }

        if (released.Count > 0)
            StickyStateChanged?.Invoke();
    }

    /// <summary>
    /// 레이아웃에 정의된 다양한 액션(키 입력, 조합키, 앱 실행, 쉘 명령 등)을 실행합니다.
    /// </summary>
    public void HandleAction(KeyAction action)
    {
        switch (action)
        {
            case SendKeyAction { Vk: var vkStr }:
                if (Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
                {
                    SendKeyPress(vk);
                    ReleaseTransientModifiers("SendKeyAction");
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

            case RunAppAction { Path: var path, Args: var args }:
                try { Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = true }); }
                catch (Exception ex) { Debug.WriteLine($"[RunApp] 실패: {path} / {ex.Message}"); }
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
                    CreateNoWindow = hidden
                };
                try { Process.Start(psi); }
                catch (Exception ex) { Debug.WriteLine($"[ShellCommand] 실패: {cmd} / {ex.Message}"); }
                break;

            case VolumeControlAction { Direction: var dir, Step: var step }:
                HandleVolumeControl(dir, step);
                break;

            case ClipboardPasteAction { Text: var pasteText }:
                // 재시도 로직이 포함된 헬퍼 사용 (다른 프로그램이 클립보드를 점유하고 있어도 안전)
                WpfApp.Current.Dispatcher.Invoke(() => ClipboardHelper.SetTextWithRetry(pasteText));
                SendCombo([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_V]);
                break;
        }
    }

    public void SendCombo(List<VirtualKeyCode> keys)
    {
        foreach (var k in keys) SendKeyDown(k);
        foreach (var k in Enumerable.Reverse(keys)) SendKeyUp(k);
        ReleaseTransientModifiers("SendCombo");
    }

    /// <summary>
    /// 일반 텍스트(유니코드)를 시스템에 직접 보냅니다. 한글 입력의 최종 단계에서 주로 사용됩니다.
    /// </summary>
    public virtual void SendUnicode(string text)
    {
        var inputs = new List<Win32.INPUT>();
        foreach (var ch in text)
        {
            inputs.Add(MakeUnicodeKeyDown(ch));
            inputs.Add(MakeUnicodeKeyUp(ch));
        }
        DispatchInput(inputs.ToArray());
        ReleaseTransientModifiers("SendUnicode");
    }

    /// <summary>
    /// [중요] 한글 조합 중 글자가 바뀔 때 사용합니다. 
    /// 이전 글자를 백스페이스로 지우고 새 글자를 즉시 입력하여 하나의 글자가 변하는 것처럼 보이게 합니다.
    /// </summary>
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
        ReleaseTransientModifiers("SendAtomicReplace");
    }

    private static bool CheckElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// 실제 윈도우 API를 호출하여 키보드 이벤트를 시스템 큐에 넣습니다.
    private void DispatchInput(Win32.INPUT[] inputs)
    {
        uint sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
        // 관리자 권한 앱에 키를 보내려다 거부당한 경우 사용자에게 알립니다.
        if (sent == 0 && Marshal.GetLastWin32Error() == Win32.ERROR_ACCESS_DENIED)
            ElevatedAppDetected?.Invoke();
    }

    private void HandleVolumeControl(string direction, int step)
    {
        var vk = direction switch
        {
            "up" => (ushort)0xAF,
            "down" => (ushort)0xAE,
            "mute" => (ushort)0xAD,
            _ => (ushort)0
        };
        if (vk == 0) return;
        int repeat = Math.Max(1, step / 2);
        for (int i = 0; i < repeat; i++)
            SendKeyPress((VirtualKeyCode)vk);
    }

    private static bool IsHighRiskModifier(VirtualKeyCode vk) => HighRiskModifiers.Contains(vk);

    private static Win32.INPUT MakeUnicodeKeyDown(char ch) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = 0, WScan = ch, DwFlags = Win32.KEYEVENTF_UNICODE, DwExtraInfo = InputExtraInfoTag } }
    };

    private static Win32.INPUT MakeUnicodeKeyUp(char ch) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = 0, WScan = ch, DwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP, DwExtraInfo = InputExtraInfoTag } }
    };

    private static Win32.INPUT MakeKeyDown(ushort vk) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = vk, DwExtraInfo = InputExtraInfoTag } }
    };

    private static Win32.INPUT MakeKeyUp(ushort vk) => new()
    {
        Type = Win32.INPUT_KEYBOARD,
        U = new() { Ki = new() { WVk = vk, DwFlags = Win32.KEYEVENTF_KEYUP, DwExtraInfo = InputExtraInfoTag } }
    };
}
