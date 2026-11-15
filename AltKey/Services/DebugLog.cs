using System.Diagnostics;

namespace AltKey.Services;

/// <summary>
/// 디버그 빌드에서만 스크래치패드 한글 조합 문제를 조사하기 위한 임시 로그입니다.
/// %TEMP%\altkey-debug.log에 타임스탬프와 함께 기록합니다. 원인을 찾으면 제거합니다.
/// </summary>
internal static class DebugLog
{
    private static readonly object Lock = new();

    [Conditional("DEBUG")]
    public static void Info(string message)
    {
        try
        {
            lock (Lock)
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "altkey-debug.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + Environment.NewLine);
            }
        }
        catch
        {
            // 디버그 기록 실패가 앱 동작에 영향을 주지 않도록 조용히 무시한다.
        }
    }
}
