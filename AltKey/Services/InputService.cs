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
    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;
    private static readonly IntPtr InputExtraInfoTag =
        unchecked((IntPtr)(long)Win32.INPUT_EXTRAINFO_ALTKEY);
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

    public InputMode Mode { get; private set; }

    public bool IsElevated => _isElevated;

    public int TrackedOnScreenLength { get; set; }

    public IReadOnlySet<VirtualKeyCode> StickyKeys => _stickyKeys;
    public IReadOnlySet<VirtualKeyCode> LockedKeys => _lockedKeys;

    public event Action<InputMode>? ModeChanged;
    public event Action? StickyStateChanged;
    public event Action? ElevatedAppDetected;

    public InputService()
    {
        _isElevated = CheckElevated();
        Mode = _isElevated ? InputMode.VirtualKey : InputMode.Unicode;
    }

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
                WpfApp.Current.Dispatcher.Invoke(() => WpfClipboard.SetText(pasteText));
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
