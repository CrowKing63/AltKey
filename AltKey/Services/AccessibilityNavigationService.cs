using System.Runtime.InteropServices;
using AltKey.Models;
using AltKey.Platform;
using AltKey.ViewModels;
using WpfApp = System.Windows.Application;

namespace AltKey.Services;

/// <summary>
/// [역할] 마우스를 쓰기 어려운 사용자가 물리 키보드(Tab, Enter 등)나 외부 스위치로 AltKey를 조작할 수 있게 돕는 서비스입니다.
/// [기능] 물리 키보드의 Tab 키를 누르면 가상 키보드의 다음 버튼으로 초점이 이동하고, Enter 키를 누르면 해당 버튼이 클릭되도록 연결합니다.
/// </summary>
public sealed class AccessibilityNavigationService : IDisposable
{
    private readonly ConfigService _configService;
    private readonly MainViewModel _mainViewModel;
    private readonly Win32.LowLevelKeyboardProc _proc;
    private readonly HashSet<uint> _pressedKeys = [];

    private IntPtr _hookHandle = IntPtr.Zero;

    public AccessibilityNavigationService(
        ConfigService configService,
        MainViewModel mainViewModel)
    {
        _configService = configService;
        _mainViewModel = mainViewModel;
        _proc = HookProc;
    }

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero) return;
        _hookHandle = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _proc, IntPtr.Zero, 0);
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        int msg = wParam.ToInt32();
        bool isKeyDown = msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
        bool isKeyUp = msg is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;
        if (!isKeyDown && !isKeyUp)
            return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var info = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
        ulong extraInfo = unchecked((ulong)info.dwExtraInfo.ToInt64());
        if (extraInfo == Win32.INPUT_EXTRAINFO_ALTKEY)
            return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        uint vk = info.vkCode;
        bool wasDown = _pressedKeys.Contains(vk);

        if (isKeyDown)
            _pressedKeys.Add(vk);
        else
            _pressedKeys.Remove(vk);

        if (!IsMainWindowVisible())
            return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        // L3: 스위치 스캔 입력 모드가 우선합니다.
        if (_configService.Current.SwitchScanEnabled)
        {
            if (TryMapSwitchScanAction(vk, out var action))
            {
                if (isKeyDown && !wasDown)
                {
                    WpfApp.Current.Dispatcher.Invoke(() =>
                    {
                        switch (action)
                        {
                            case SwitchScanAction.Next:
                                _mainViewModel.Keyboard.AdvanceScan();
                                break;
                            case SwitchScanAction.Previous:
                                _mainViewModel.Keyboard.ReverseScan();
                                break;
                            case SwitchScanAction.Select:
                                _mainViewModel.Keyboard.SelectScanTarget();
                                break;
                            case SwitchScanAction.Pause:
                                _mainViewModel.Keyboard.ToggleScanPaused();
                                break;
                        }
                    });
                }

                // 스캔 모드에서도 해당 키는 외부 앱으로 넘기지 않습니다.
                return (IntPtr)1;
            }
        }

        if (!_configService.Current.KeyboardA11yNavigationEnabled)
            return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        if (IsExitKey(vk) && isKeyDown && !wasDown)
        {
            WpfApp.Current.Dispatcher.Invoke(() => _mainViewModel.Keyboard.ClearA11yFocus());
            return (IntPtr)1;
        }

        if (vk is (uint)VirtualKeyCode.VK_TAB
            or (uint)VirtualKeyCode.VK_RETURN
            or (uint)VirtualKeyCode.VK_SPACE)
        {
            if (isKeyDown)
            {
                bool isRepeat = wasDown;
                if (!isRepeat)
                {
                    WpfApp.Current.Dispatcher.Invoke(() =>
                    {
                        if (vk == (uint)VirtualKeyCode.VK_TAB)
                        {
                            _mainViewModel.Keyboard.MoveA11yFocus(IsShiftPressed());
                        }
                        else
                        {
                            _mainViewModel.Keyboard.ActivateA11yFocused();
                        }
                    });
                }
            }

            // 접근성 탐색 모드에서는 Tab/Enter/Space를 외부 앱으로 넘기지 않는다.
            return (IntPtr)1;
        }

        return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool IsMainWindowVisible()
    {
        return WpfApp.Current?.MainWindow?.IsVisible == true;
    }

    private bool IsShiftPressed()
    {
        return _pressedKeys.Contains((uint)VirtualKeyCode.VK_SHIFT)
            || _pressedKeys.Contains((uint)VirtualKeyCode.VK_LSHIFT)
            || _pressedKeys.Contains((uint)VirtualKeyCode.VK_RSHIFT);
    }

    private enum SwitchScanAction
    {
        Next,
        Previous,
        Select,
        Pause,
    }

    private bool TryMapSwitchScanAction(uint vk, out SwitchScanAction action)
    {
        action = default;
        var c = _configService.Current;

        if (MatchesConfiguredKey(c.SwitchScanNextKey, vk))
        {
            action = SwitchScanAction.Next;
            return true;
        }
        if (MatchesConfiguredKey(c.SwitchScanSelectKey, vk) || MatchesConfiguredKey(c.SwitchScanSecondarySelectKey, vk))
        {
            action = SwitchScanAction.Select;
            return true;
        }
        if (MatchesConfiguredKey(c.SwitchScanPreviousKey, vk))
        {
            action = SwitchScanAction.Previous;
            return true;
        }
        if (MatchesConfiguredKey(c.SwitchScanPauseKey, vk))
        {
            action = SwitchScanAction.Pause;
            return true;
        }
        return false;
    }

    // [접근성] 설정에 입력된 VK 문자열을 안전하게 해석합니다. 빈 값/잘못된 값은 false로 처리해 앱 안정성을 유지합니다.
    private static bool MatchesConfiguredKey(string keyName, uint vk)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return false;
        if (!Enum.TryParse<VirtualKeyCode>(keyName.Trim(), ignoreCase: true, out var parsed))
            return false;
        return (uint)parsed == vk;
    }

    private bool IsExitKey(uint vk)
    {
        string configured = _configService.Current.KeyboardA11yExitKey;
        if (!Enum.TryParse<VirtualKeyCode>(configured, ignoreCase: true, out var exitVk))
            exitVk = VirtualKeyCode.VK_ESCAPE;
        return vk == (uint)exitVk;
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
