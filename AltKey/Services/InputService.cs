using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using AltKey.Models;
using AltKey.Platform;
using WpfApp = System.Windows.Application;

namespace AltKey.Services;

/// <summary>
/// Sends virtual keys or Unicode text to the target app and tracks transient keyboard state.
/// Sticky modifiers are handled here, and the Fn layer is also kept here so UI and execution share one source of truth.
/// </summary>
public enum InputMode
{
    Unicode,
    VirtualKey
}

public class InputService
{
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;
    private static readonly IntPtr InputExtraInfoTag =
        unchecked((IntPtr)(long)Win32.INPUT_EXTRAINFO_ALTKEY);

    // Ctrl/Alt/Win can leave the desktop in a risky state if they stay pressed while the window hides.
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
    private readonly HashSet<VirtualKeyCode> _stickyKeys = [];
    private readonly HashSet<VirtualKeyCode> _lockedKeys = [];
    private readonly HashSet<VirtualKeyCode> _heldKeys = [];
    private FunctionLayerState _functionLayerState;
    private VirtualKeyCode? _armedHeldKey;

    public InputMode Mode { get; private set; }
    public bool IsElevated => _isElevated;
    public int TrackedOnScreenLength { get; set; }
    public FunctionLayerState FunctionLayerState => _functionLayerState;
    public bool IsFunctionLayerActive => _functionLayerState != FunctionLayerState.Inactive;

    public IReadOnlySet<VirtualKeyCode> StickyKeys => _stickyKeys;
    public IReadOnlySet<VirtualKeyCode> LockedKeys => _lockedKeys;
    public IReadOnlySet<VirtualKeyCode> HeldKeys => _heldKeys;

    public event Action<InputMode>? ModeChanged;
    public event Action? StickyStateChanged;
    public event Action? ElevatedAppDetected;
    public event Action<KeyAction>? SpecialActionRequested;

    public InputService()
    {
        _isElevated = CheckElevated();
        Mode = _isElevated ? InputMode.VirtualKey : InputMode.Unicode;
    }

    public bool IsForegroundOwnWindow()
    {
        var hwnd = Win32.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        Win32.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == OwnProcessId;
    }

    public bool TrySetMode(InputMode target)
    {
        if (_isElevated && target == InputMode.Unicode)
            return false;

        if (Mode == target)
            return true;

        Mode = target;
        ModeChanged?.Invoke(Mode);
        return true;
    }

    public void ResetTrackedLength() => TrackedOnScreenLength = 0;
    public void NotifyElevatedApp() => ElevatedAppDetected?.Invoke();
    public bool IsCapsLockOn => (Win32.GetKeyState((int)VirtualKeyCode.VK_CAPITAL) & 0x0001) != 0;
    public bool HasActiveModifiers => _stickyKeys.Count > 0 || _lockedKeys.Count > 0;

    /// <summary>
    /// The Fn layer follows the same 3-step cycle users already know from sticky modifiers.
    /// First press arms one-shot, second press locks, third press clears.
    /// </summary>
    public void ToggleFunctionLayer()
    {
        _functionLayerState = _functionLayerState switch
        {
            FunctionLayerState.Inactive => FunctionLayerState.OneShot,
            FunctionLayerState.OneShot => FunctionLayerState.Locked,
            _ => FunctionLayerState.Inactive
        };

        StickyStateChanged?.Invoke();
    }

    /// <summary>
    /// One-shot Fn should disappear after the next non-Fn key finishes.
    /// Locked Fn stays on until the user presses Fn again.
    /// </summary>
    public void ConsumeFunctionLayerAfterAction()
    {
        if (_functionLayerState != FunctionLayerState.OneShot)
            return;

        _functionLayerState = FunctionLayerState.Inactive;
        StickyStateChanged?.Invoke();
    }

    public void ResetFunctionLayer()
    {
        if (_functionLayerState == FunctionLayerState.Inactive)
            return;

        _functionLayerState = FunctionLayerState.Inactive;
        StickyStateChanged?.Invoke();
    }

    /// <summary>
    /// Shift is excluded because Hangul combo logic still allows Shift-only input through the composition path.
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

    /// <summary>
    /// 다음 SendKeyAction 1회를 "눌린 상태 유지"로 해석하도록 예약합니다.
    /// KeyButton이 마우스 누름을 시작할 때 호출하고, 실제 KeyDown은 HandleAction이 담당합니다.
    /// </summary>
    public void ArmHeldKeyGesture(VirtualKeyCode vk)
    {
        _armedHeldKey = vk;
    }

    /// <summary>
    /// 예약된 홀드 시작 요청을 취소합니다.
    /// 다른 키가 실행되었거나, 사용자가 길게 누름을 끝내기 전에 입력이 무효가 된 경우에 사용합니다.
    /// </summary>
    public void CancelHeldKeyGesture(VirtualKeyCode? vk = null)
    {
        if (vk is null || _armedHeldKey == vk)
            _armedHeldKey = null;
    }

    /// <summary>
    /// 가상 키를 실제 키보드처럼 누른 상태로 유지합니다.
    /// 이미 눌린 키는 중복 KeyDown을 보내지 않아 게임/앱 쪽 상태가 꼬이지 않게 막습니다.
    /// </summary>
    public virtual void BeginHeldKey(VirtualKeyCode vk)
    {
        if (!_heldKeys.Add(vk))
            return;

        SendKeyDown(vk);
    }

    /// <summary>
    /// BeginHeldKey로 유지 중인 키를 해제합니다.
    /// </summary>
    public virtual void EndHeldKey(VirtualKeyCode vk)
    {
        if (!_heldKeys.Remove(vk))
            return;

        SendKeyUp(vk);
    }

    /// <summary>
    /// 홀드 중인 키에 추가 KeyDown 신호만 한 번 더 보냅니다.
    /// 일부 앱은 "눌린 상태"만으로는 반복 입력을 만들지 않으므로, 기존 반복 간격을 보조 pulse로 재활용합니다.
    /// </summary>
    public virtual void PulseHeldKey(VirtualKeyCode vk)
    {
        if (!_heldKeys.Contains(vk))
            return;

        SendKeyDown(vk);
    }

    /// <summary>
    /// 현재 유지 중인 키를 전부 해제합니다.
    /// 창 숨김, 캡처 손실, 앱 종료처럼 KeyUp 누락이 치명적인 경로에서 사용합니다.
    /// </summary>
    public virtual void ReleaseAllHeldKeys(string reason = "manual")
    {
        CancelHeldKeyGesture();

        foreach (var vk in _heldKeys.ToList())
            SendKeyUp(vk);

        _heldKeys.Clear();
    }

    /// <summary>
    /// 특정 키가 홀드 상태인지 확인합니다.
    /// KeyButton이 마우스 업 시점에 자신이 해제할 키를 판단할 때 사용합니다.
    /// </summary>
    public bool IsHeldKey(VirtualKeyCode vk) => _heldKeys.Contains(vk);

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

    public void HandleAction(KeyAction action)
    {
        switch (action)
        {
            case SendKeyAction { Vk: var vkStr }:
                if (Enum.TryParse<VirtualKeyCode>(vkStr, out var vk))
                {
                    if (Mode == InputMode.VirtualKey && _armedHeldKey == vk)
                    {
                        BeginHeldKey(vk);
                        _armedHeldKey = null;
                    }
                    else
                    {
                        SendKeyPress(vk);
                    }

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

            case ToggleFunctionLayerAction:
                ToggleFunctionLayer();
                break;

            case AiAction aiAction:
                SpecialActionRequested?.Invoke(aiAction);
                break;

            case RunAppAction { Path: var path, Args: var args }:
                try
                {
                    Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RunApp] failed: {path} / {ex.Message}");
                }
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

                try
                {
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ShellCommand] failed: {cmd} / {ex.Message}");
                }
                break;

            case VolumeControlAction { Direction: var dir, Step: var step }:
                HandleVolumeControl(dir, step);
                break;

            case ClipboardPasteAction { Text: var pasteText }:
                WpfApp.Current.Dispatcher.Invoke(() => ClipboardHelper.SetTextWithRetry(pasteText));
                SendCombo([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_V]);
                break;
        }
    }

    public void SendCombo(List<VirtualKeyCode> keys)
    {
        foreach (var k in keys)
            SendKeyDown(k);
        foreach (var k in Enumerable.Reverse(keys))
            SendKeyUp(k);
        ReleaseTransientModifiers("SendCombo");
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
        ReleaseTransientModifiers("SendUnicode");
    }

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

    private void DispatchInput(Win32.INPUT[] inputs)
    {
        uint sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>());
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

        if (vk == 0)
            return;

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
