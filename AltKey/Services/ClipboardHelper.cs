using System.Runtime.InteropServices;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;

namespace AltKey.Services;

/// <summary>
/// [역할] 윈도우 클립보드 접근 시 발생할 수 있는 충돌을 안전하게 처리하는 유틸리티입니다.
/// [기능] 다른 프로그램이 클립보드를 점유하고 있거나 데이터가 손상된 경우, 지수 백오프(Exponential Backoff) 방식으로 재시도합니다.
/// [참고] 멀티 프로세스 환경에서 클립보드 접근 충돌은 흔히 발생하며, 재시도 로직은 필수입니다.
/// </summary>
public static class ClipboardHelper
{
    // ── 클립보드 관련 HRESULT 오류 코드 ───────────────────────────────────
    private const int CLIPBRD_E_CANT_OPEN  = unchecked((int)0x800401D0); // 다른 프로세스가 점유 중
    private const int CLIPBRD_E_NOT_OPEN   = unchecked((int)0x800401D1);
    private const int CLIPBRD_E_CANT_EMPTY = unchecked((int)0x800401D2);
    private const int CLIPBRD_E_BAD_DATA   = unchecked((int)0x800401D3); // 클립보드 데이터 손상

    // 재시도 설정
    private const int DefaultMaxRetries = 3;
    private const int InitialDelayMs = 10;

    /// <summary>
    /// 주어진 COMException이 클립보드 관련 오류인지 확인합니다.
    /// </summary>
    private static bool IsClipboardError(COMException ex)
    {
        return ex.ErrorCode == CLIPBRD_E_CANT_OPEN
            || ex.ErrorCode == CLIPBRD_E_NOT_OPEN
            || ex.ErrorCode == CLIPBRD_E_CANT_EMPTY
            || ex.ErrorCode == CLIPBRD_E_BAD_DATA;
    }

    /// <summary>
    /// 클립보드에서 텍스트를 읽습니다. 실패 시 재시도합니다.
    /// </summary>
    /// <param name="maxRetries">최대 재시도 횟수 (기본값: 3)</param>
    /// <returns>클립보드 텍스트 또는 null</returns>
    public static string? GetTextWithRetry(int maxRetries = DefaultMaxRetries)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (!WpfClipboard.ContainsText()) return null;
                return WpfClipboard.GetText();
            }
            catch (COMException ex) when (IsClipboardError(ex))
            {
                // CANT_OPEN은 재시도 의미가 있지만, BAD_DATA 등은 재시도필도 소용없음
                // 그러나 안전을 위해 모두 재시도 (짧은 간격이므로 부담 없음)
                if (attempt < maxRetries - 1)
                    Thread.Sleep(InitialDelayMs << attempt); // 10ms → 20ms → 40ms
            }
        }
        return null;
    }

    /// <summary>
    /// 클립보드에 텍스트를 설정합니다. 실패 시 재시도합니다.
    /// </summary>
    /// <param name="text">설정할 텍스트</param>
    /// <param name="maxRetries">최대 재시도 횟수 (기본값: 3)</param>
    public static void SetTextWithRetry(string text, int maxRetries = DefaultMaxRetries)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                WpfClipboard.SetText(text);
                return;
            }
            catch (COMException ex) when (IsClipboardError(ex))
            {
                if (attempt < maxRetries - 1)
                    Thread.Sleep(InitialDelayMs << attempt); // 10ms → 20ms → 40ms
            }
        }
    }

    /// <summary>
    /// 클립보드에 텍스트가 있는지 확인합니다. 실패 시 재시도합니다.
    /// </summary>
    /// <param name="maxRetries">최대 재시도 횟수 (기본값: 3)</param>
    /// <returns>텍스트 포함 여부 또는 false</returns>
    public static bool ContainsTextWithRetry(int maxRetries = DefaultMaxRetries)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return WpfClipboard.ContainsText();
            }
            catch (COMException ex) when (IsClipboardError(ex))
            {
                if (attempt < maxRetries - 1)
                    Thread.Sleep(InitialDelayMs << attempt); // 10ms → 20ms → 40ms
            }
        }
        return false;
    }
}
