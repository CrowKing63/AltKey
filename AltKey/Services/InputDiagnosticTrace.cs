using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace AltKey.Services;

/// <summary>임시 진단 로그입니다. 키의 실제 문자나 클립보드 본문은 기록하지 않습니다.</summary>
internal static class InputDiagnosticTrace
{
    private static readonly object Sync = new();
    private static string? _path;

    public static void Log(string message)
    {
        try
        {
            lock (Sync)
            {
                _path ??= Path.Combine(PathResolver.DataDir, "input-diagnostic.log");
                File.AppendAllText(_path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 진단 파일 쓰기 실패는 입력 동작에 영향을 주지 않습니다.
        }
    }

    public static void LogScratchPadKey(string mode, string keyAction, System.Windows.Controls.TextBox textBox, bool isKeyboardFocused)
    {
        var window = Window.GetWindow(textBox);
        var hwnd = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        uint pid = 0;
        uint threadId = hwnd == IntPtr.Zero ? 0 : GetWindowThreadProcessId(hwnd, out pid);
        var hkl = threadId == 0 ? IntPtr.Zero : GetKeyboardLayout(threadId);
        var imeContext = hwnd == IntPtr.Zero ? IntPtr.Zero : ImmGetContext(hwnd);
        var imeOpen = imeContext == IntPtr.Zero ? "unavailable" : ImmGetOpenStatus(imeContext).ToString();
        if (imeContext != IntPtr.Zero)
            ImmReleaseContext(hwnd, imeContext);

        bool imeEnabled = InputMethod.GetIsInputMethodEnabled(textBox);
        string preferredImeState = InputMethod.GetPreferredImeState(textBox).ToString();
        Log($"IME_KEY mode={mode} action={keyAction} hwnd={hwnd} pid={pid} hkl=0x{hkl.ToInt64():X} imeOpen={imeOpen} wpfImeEnabled={imeEnabled} wpfPreferredImeState={preferredImeState} textBoxFocused={isKeyboardFocused} {DescribeWindow(hwnd)}");
    }

    public static void LogPasteStage(string stage, bool preparedScratchPadTarget)
    {
        var hwnd = GetForegroundWindow();
        var wpfFocus = Keyboard.FocusedElement;
        string wpfFocusType = wpfFocus?.GetType().FullName ?? "none";
        bool noteBoxFocused = wpfFocus is System.Windows.Controls.TextBox textBox && textBox.IsKeyboardFocused;
        Log($"PASTE_{stage} preparedScratchPadTarget={preparedScratchPadTarget} wpfFocus={wpfFocusType} noteBoxFocused={noteBoxFocused} {DescribeWindow(hwnd)}");
    }

    /// <summary>키 포커스 복원 이후와 조합기 처리 결과를 입력 본문 없이 기록합니다.</summary>
    public static void LogScratchPadStage(string stage, string mode, System.Windows.Controls.TextBox textBox, string result = "")
    {
        var focused = Keyboard.FocusedElement;
        string focusedType = focused?.GetType().FullName ?? "none";
        var window = Window.GetWindow(textBox);
        var hwnd = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        bool imeEnabled = InputMethod.GetIsInputMethodEnabled(textBox);
        string preferredState = InputMethod.GetPreferredImeState(textBox).ToString();
        Log($"IME_{stage} mode={mode} result={result} textBoxFocused={textBox.IsKeyboardFocused} focusWithin={textBox.IsKeyboardFocusWithin} wpfFocus={focusedType} wpfImeEnabled={imeEnabled} wpfPreferredImeState={preferredState} {DescribeWindow(hwnd)}");
    }

    /// <summary>메모 내용은 남기지 않고 입력 후 길이와 선택 상태만 기록합니다.</summary>
    public static void LogScratchPadTextState(System.Windows.Controls.TextBox textBox)
    {
        var window = Window.GetWindow(textBox);
        var hwnd = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        Log($"SCRATCHPAD_TEXT_CHANGED length={textBox.Text.Length} selectionStart={textBox.SelectionStart} selectionLength={textBox.SelectionLength} {DescribeWindow(hwnd)}");
    }

    public static string DescribeWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "fg=0";
        uint threadId = GetWindowThreadProcessId(hwnd, out var pid);
        var hkl = GetKeyboardLayout(threadId);
        var imeContext = ImmGetContext(hwnd);
        var imeOpen = imeContext == IntPtr.Zero ? "unavailable" : ImmGetOpenStatus(imeContext).ToString();
        string imeConversion = "unavailable";
        if (imeContext != IntPtr.Zero && ImmGetConversionStatus(imeContext, out uint conversionMode, out _))
            imeConversion = $"0x{conversionMode:X} native={(conversionMode & 0x1) != 0}";
        if (imeContext != IntPtr.Zero)
            ImmReleaseContext(hwnd, imeContext);

        var guiInfo = new GuiThreadInfo { cbSize = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        bool hasGuiInfo = GetGUIThreadInfo(threadId, ref guiInfo);
        var focusHwnd = hasGuiInfo ? guiInfo.hwndFocus : IntPtr.Zero;
        var className = new StringBuilder(128);
        if (focusHwnd != IntPtr.Zero)
            GetClassName(focusHwnd, className, className.Capacity);
        return $"fg={hwnd} pid={pid} hkl=0x{hkl.ToInt64():X} imeOpen={imeOpen} imeConversion={imeConversion} focusHwnd={focusHwnd} focusClass='{className}'";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", EntryPoint = "GetKeyboardLayout")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextLengthW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("imm32.dll", EntryPoint = "ImmGetContext")]
    private static extern IntPtr ImmGetContext(IntPtr hwnd);
    [DllImport("imm32.dll", EntryPoint = "ImmGetOpenStatus")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmGetOpenStatus(IntPtr context);
    [DllImport("imm32.dll", EntryPoint = "ImmGetConversionStatus")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmGetConversionStatus(IntPtr context, out uint conversionMode, out uint sentenceMode);
    [DllImport("imm32.dll", EntryPoint = "ImmReleaseContext")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr context);
}
