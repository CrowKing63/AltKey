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

        if (!_configService.Current.KeyboardA11yNavigationEnabled)
            return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        if (!IsMainWindowVisible())
            return Win32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

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

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
