using System.IO;

namespace AltKey.Services;

/// <summary>
/// [임시 진단 로그] 클립보드 포커스·붙여넣기·닫힘 이벤트를 파일로 남깁니다.
/// 재현이 어려운 런타임 원인(포커스 소실, 붙여넣기 실패)을 잡기 위해 —
/// DebugLog와 달리 Release/샌드박스 빌드에서도 기록됩니다. 원인을 찾으면 제거합니다.
/// 파일: {DataDir}\clipboard-trace.log (포터블 빌드 = exe 폴더 — 샌드박스에서 밖으로 드래그해 내리면 됨)
/// </summary>
internal static class ClipboardTrace
{
    private static readonly object _lock = new();
    private static string? _path;

    public static void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                // 클립보드 항목 본문은 로그에 기록하지 않습니다.
                if (message.StartsWith("PASTE Ctrl+V sent (text=", StringComparison.Ordinal))
                    message = "PASTE Ctrl+V sent (text=<redacted>)";
                _path ??= Path.Combine(PathResolver.DataDir, "clipboard-trace.log");
                File.AppendAllText(_path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 진단 로그 실패는 무시 — 메인 동작에 영향 없게 한다.
        }
    }
}
