using System.Runtime.InteropServices;

namespace AltKey.Platform;

internal static class Win32
{
    // 윈도우 스타일 상수
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOPMOST_VAL = 0x00000008;

    // SetWindowPos 플래그
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(
        IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    // SendInput (Phase 2에서 사용)
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // 가상 키(VK_*)를 키보드의 물리 위치 번호(스캔코드)로 바꿉니다.
    // SendKey 기반 입력을 더 물리 키보드에 가깝게 보내기 위해 사용합니다.
    [DllImport("user32.dll")]
    public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    // RegisterHotKey (Phase 5에서 사용)
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // WinEventHook (Phase 5에서 사용)
    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    // IME 상태/포그라운드 변경 감지용 이벤트 상수 (winuser.h)
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_INPUTLANGCHANGE  = 0x0011;
    public const uint WINEVENT_OUTOFCONTEXT  = 0x0000;

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // 프로세스 이름 조회 (Phase 5에서 사용)
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 클래스 이름으로 최상위 창을 찾습니다. 창이 없으면 IntPtr.Zero를 돌려줍니다.
    /// OSK(화면 키보드)가 열려 있는지 확인할 때 사용합니다 — OSK 창 클래스는 "OSKMainClass"입니다.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    /// <summary>포커스를 지정 창으로 옮깁니다. (AI 복사/붙여넣기 전에 대상 앱으로 되돌릴 때 사용)</summary>
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    // T-2.10b: 프로세스 핸들 및 무결성 수준 조회
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll")]
    public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetTokenInformation(
        IntPtr TokenHandle,
        TOKEN_INFORMATION_CLASS TokenInformationClass,
        IntPtr TokenInformation,
        uint TokenInformationLength,
        out uint ReturnLength);

    [DllImport("advapi32.dll")]
    public static extern IntPtr GetSidSubAuthority(IntPtr pSid, uint subAuthorityIndex);

    [DllImport("advapi32.dll")]
    public static extern IntPtr GetSidSubAuthorityCount(IntPtr pSid);

    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint TOKEN_QUERY = 0x0008;
    public const int SECURITY_MANDATORY_MEDIUM_RID = 0x2000; // 중간 무결성 수준

    public enum TOKEN_INFORMATION_CLASS
    {
        TokenUser = 1,
        TokenGroups,
        TokenPrivileges,
        TokenOwner,
        TokenPrimaryGroup,
        TokenDefaultDacl,
        TokenSource,
        TokenType,
        TokenImpersonationLevel,
        TokenStatistics,
        TokenRestrictedSids,
        TokenSessionId,
        TokenGroupsAndPrivileges,
        TokenSessionReference,
        TokenSandBoxInert,
        TokenAuditPolicy,
        TokenIntegrityLevel = 25
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_MANDATORY_LABEL
    {
        public SID_AND_ATTRIBUTES Label;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    // ── 전역 저수준 키보드 훅 (WH_KEYBOARD_LL) ────────────────────────────
    public const int WH_KEYBOARD_LL  = 13;
    public const int WM_KEYDOWN      = 0x0100;
    public const int WM_KEYUP        = 0x0101;
    public const int WM_SYSKEYDOWN   = 0x0104;
    public const int WM_SYSKEYUP     = 0x0105;

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelKeyboardProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint   vkCode;
        public uint   scanCode;
        public uint   flags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    // Acrylic 효과 (T-1.3 / T-1.4에서 사용)
    [DllImport("user32.dll")]
    public static extern int SetWindowCompositionAttribute(
        IntPtr hwnd, ref WindowCompositionAttribData data);

    // INPUT 구조체 (Phase 2에서 사용)
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mi;
        [FieldOffset(0)] public KEYBDINPUT Ki;
        [FieldOffset(0)] public HARDWAREINPUT Hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint UMsg;
        public ushort WParamL;
        public ushort WParamH;
    }

    // WindowCompositionAttribData (Acrylic)
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowCompositionAttribData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    public enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_ENABLE_HOSTBACKDROP = 5,
        ACCENT_INVALID_STATE = 6,
    }

    public enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19,
    }

    public const uint LWA_ALPHA = 0x00000002;
    public const uint LWA_COLORKEY = 0x00000001;

    // 상수 (Phase 2)
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_KEYUP       = 0x0002;
    public const uint KEYEVENTF_UNICODE     = 0x0004;
    public const uint KEYEVENTF_SCANCODE    = 0x0008;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint MAPVK_VK_TO_VSC_EX    = 0x0004;

    public const int ERROR_ACCESS_DENIED = 5;

    // AltKey가 SendInput으로 생성한 이벤트 식별용 태그
    public const ulong INPUT_EXTRAINFO_ALTKEY = 0xA17A11UL;

    // ── OS IME 상태 판별용 Win32 ──────────────────────────────────────────
    // docs/feature-ime-detection/plan.md 참조. TASK-02가 제거한 선언을 복구한다.
    public const uint WM_IME_CONTROL = 0x0283;
    public const int IMC_GETOPENSTATUS = 0x0005;      // 열림 상태 조회
    public const int IMC_GETCONVERSIONMODE = 0x0001;  // 변환 모드 조회
    public const uint SMTO_BLOCK = 0x0001;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    // 변환 모드 상수 (IME conversion mode) — 비트마스크. Windows SDK 기준.
    // 네이티브(한글 조합) 비트는 bit 0. 알파누메릭(영문)은 이 비트가 0인 상태다.
    public const int IME_CMODE_ALPHANUMERIC = 0x00000000;  // 영문자 모드 (네이티브 비트 없음)
    public const int IME_CMODE_NATIVE = 0x00000001;       // 한국어/일본어 등 조합 모드

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public uint cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public int caretLeft;
        public int caretTop;
        public int caretRight;
        public int caretBottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetGUIThreadInfo")]
    public static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

    [DllImport("user32.dll")]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    public const uint GA_ROOT = 0x00000001;
    public const uint GA_ROOTOWNER = 0x00000002;

    // 자손 창 열거 콜백. EnumChildWindows가 콜백을 동기 호출하므로 로컬 람다로 전달해도 안전하다.
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeout")]
    public static extern bool SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("imm32.dll", ExactSpelling = true)]
    public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    // IMM32 선언은 Windows SDK 시그니처를 따른다.
    // 예전 2인자 bool 선언은 실제 API와 어긋나 ImmGetContext가 항상 실패했다.
    // 실제: HIMC ImmGetContext(HWND) — 컨텍스트를 반환값으로 받는다.
    [DllImport("imm32.dll", ExactSpelling = true)]
    public static extern IntPtr ImmGetContext(IntPtr hWnd);

    // 실제: BOOL ImmGetOpenStatus(HIMC) — 반환값 자체가 열림 상태.
    [DllImport("imm32.dll", ExactSpelling = true)]
    public static extern bool ImmGetOpenStatus(IntPtr contextId);

    // 실제: BOOL ImmGetConversionStatus(HIMC, LPDWORD conversion, LPDWORD sentence).
    [DllImport("imm32.dll", ExactSpelling = true)]
    public static extern bool ImmGetConversionStatus(IntPtr contextId, out int conversion, out int sentence);

    [DllImport("imm32.dll", ExactSpelling = true)]
    public static extern int ImmGetLanguageID(IntPtr contextId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetKeyboardLayout(uint threadId);

    // 실제: BOOL ImmReleaseContext(HWND, HIMC) — 창 핸들도 함께 넘겨야 한다.
    [DllImport("imm32.dll", ExactSpelling = true)]
    public static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr contextId);
}
