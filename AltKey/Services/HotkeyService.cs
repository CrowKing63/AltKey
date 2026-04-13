using System.Windows.Interop;
using AltKey.Platform;

namespace AltKey.Services;

/// T-5.7 / T-5.8: 전역 단축키 등록 및 사용자 커스텀 파싱
public class HotkeyService : IDisposable
{
    private const int HOTKEY_ID = 9001;
    private HwndSource? _source;

    public event Action? HotkeyPressed;

    // ── 등록 ────────────────────────────────────────────────────────────────

    public void Register(IntPtr hwnd, uint modifiers, uint vk)
    {
        _source ??= HwndSource.FromHwnd(hwnd);
        _source.AddHook(HwndHook);
        Win32.RegisterHotKey(hwnd, HOTKEY_ID, modifiers, vk);
    }

    public void Reregister(IntPtr hwnd, string hotkeyString)
    {
        if (_source is not null)
            Win32.UnregisterHotKey(_source.Handle, HOTKEY_ID);

        var (mods, vk) = ParseHotkey(hotkeyString);
        Register(hwnd, mods, vk);
    }

    // ── WndProc 훅 ──────────────────────────────────────────────────────────

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HOTKEY_ID) // WM_HOTKEY
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ── T-5.8: "Ctrl+Alt+K" 형식 파싱 ──────────────────────────────────────

    public static (uint modifiers, uint vk) ParseHotkey(string hotkey)
    {
        uint mods = 0;
        var parts = hotkey.Split('+').Select(s => s.Trim()).ToList();

        if (parts.Contains("Ctrl",  StringComparer.OrdinalIgnoreCase)) mods |= 0x0002; // MOD_CONTROL
        if (parts.Contains("Alt",   StringComparer.OrdinalIgnoreCase)) mods |= 0x0001; // MOD_ALT
        if (parts.Contains("Shift", StringComparer.OrdinalIgnoreCase)) mods |= 0x0004; // MOD_SHIFT
        if (parts.Contains("Win",   StringComparer.OrdinalIgnoreCase)) mods |= 0x0008; // MOD_WIN

        var keyStr = parts.Last();
        uint vk = keyStr.Length == 1 ? (uint)char.ToUpper(keyStr[0]) : 0;
        return (mods, vk);
    }

    // ── 해제 ────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_source is not null)
        {
            Win32.UnregisterHotKey(_source.Handle, HOTKEY_ID);
            _source.RemoveHook(HwndHook);
        }
    }
}
