namespace AltKey.Models;

public enum VirtualKeyCode : ushort
{
    // 알파벳 (0x41~0x5A)
    VK_A = 0x41, VK_B, VK_C, VK_D, VK_E, VK_F, VK_G,
    VK_H, VK_I, VK_J, VK_K, VK_L, VK_M, VK_N,
    VK_O, VK_P, VK_Q, VK_R, VK_S, VK_T, VK_U,
    VK_V, VK_W, VK_X, VK_Y, VK_Z,
    // 숫자 (0x30~0x39)
    VK_0 = 0x30, VK_1, VK_2, VK_3, VK_4,
    VK_5, VK_6, VK_7, VK_8, VK_9,
    // 제어 키
    VK_BACK    = 0x08, VK_TAB    = 0x09, VK_RETURN  = 0x0D,
    VK_SHIFT   = 0x10, VK_CONTROL = 0x11, VK_MENU   = 0x12, // Alt
    VK_PAUSE   = 0x13, VK_CAPITAL = 0x14,
    VK_ESCAPE  = 0x1B, VK_SPACE   = 0x20,
    VK_PRIOR   = 0x21, VK_NEXT    = 0x22,  // Page Up / Down
    VK_END     = 0x23, VK_HOME    = 0x24,
    VK_LEFT    = 0x25, VK_UP      = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28,
    VK_INSERT  = 0x2D, VK_DELETE  = 0x2E,
    VK_LWIN    = 0x5B, VK_RWIN    = 0x5C,
    VK_LSHIFT  = 0xA0, VK_RSHIFT  = 0xA1,
    VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3,
    VK_LMENU   = 0xA4, VK_RMENU   = 0xA5,
    // 기능 키
    VK_F1 = 0x70, VK_F2, VK_F3, VK_F4, VK_F5, VK_F6,
    VK_F7, VK_F8, VK_F9, VK_F10, VK_F11, VK_F12,
    // 한국어
    VK_HANGUL = 0x15, VK_HANJA = 0x19,
    // OEM 기호
    VK_OEM_1      = 0xBA, VK_OEM_PLUS   = 0xBB, VK_OEM_COMMA  = 0xBC,
    VK_OEM_MINUS  = 0xBD, VK_OEM_PERIOD = 0xBE, VK_OEM_2      = 0xBF,
    VK_OEM_3      = 0xC0, VK_OEM_4      = 0xDB, VK_OEM_5      = 0xDC,
    VK_OEM_6      = 0xDD, VK_OEM_7      = 0xDE,
    VK_PRINT   = 0x2A, VK_NUMLOCK    = 0x90, VK_SCROLL     = 0x91,
}

public static class VirtualKeyCodeExtensions
{
    public static bool IsModifier(this VirtualKeyCode vk) =>
        vk is VirtualKeyCode.VK_SHIFT   or VirtualKeyCode.VK_LSHIFT   or VirtualKeyCode.VK_RSHIFT
           or VirtualKeyCode.VK_CONTROL or VirtualKeyCode.VK_LCONTROL or VirtualKeyCode.VK_RCONTROL
           or VirtualKeyCode.VK_MENU    or VirtualKeyCode.VK_LMENU    or VirtualKeyCode.VK_RMENU
           or VirtualKeyCode.VK_LWIN   or VirtualKeyCode.VK_RWIN;
}
