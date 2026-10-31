using System.IO;
using System.IO.Pipes;

namespace AltKey.Services;

/// <summary>
/// [역할] 메인 앱과 상주하는 AltKey.Tools 프로세스 사이의 명령 채널입니다.
/// [기능] 메인 앱이 명명 파이프를 통해 텍스트 명령(편집기 창 열기, 종료)을 Tools에 보내
/// 새 프로세스를 띄우지 않고도 편집기 창을 즉시 여게 합니다.
/// [한계] 명령 하나당 연결을 한 번씩만 열고 닫는 단순 구조입니다. 지속 세션은 유지하지 않습니다.
/// </summary>
public static class ToolsIpc
{
    /// <summary>
    /// 명명 파이프 이름입니다. Windows 사용자 이름을 포함해 사용자 간 공유를 막습니다.
    /// </summary>
    public static string PipeName => "AltKey.Tools.Ipc." + SanitizePipeName(Environment.UserName);

    /// <summary>
    /// 파이프 이름에는 영숫자, 밑줄, 마침표만 허용되므로 나머지는 밑줄로 바꿉니다.
    /// </summary>
    internal static string SanitizePipeName(string raw) =>
        new(raw.Select(c => char.IsLetterOrDigit(c) || c is '_' or '.' ? c : '_').ToArray());

    /// <summary>
    /// 상주 Tools 프로세스에 명령 한 줄을 보냅니다.
    /// 성공하면 true, 듣는 프로세스가 없으면 false를 반환합니다 (호출부는 새 프로세스 시작으로 폴백).
    /// </summary>
    public static bool TrySend(string message, int timeoutMs = 800) =>
        TrySend(message, PipeName, timeoutMs);

    /// <summary>
    /// 지정된 파이프 이름으로 명령을 보냅니다. 테스트가 프로덕션 파이프와 충돌하지 않게 합니다.
    /// </summary>
    public static bool TrySend(string message, string pipeName, int timeoutMs)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName);
            client.Connect(timeoutMs);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(message);
            return true;
        }
        catch
        {
            // 상주 프로세스가 없음(연결 거부/시간 초과) — 호출부에서 폴백 처리합니다.
            return false;
        }
    }

    /// <summary>
    /// 연결 하나를 받는 서버 스트림을 만듭니다. Tools 프로세스 쪽과 테스트에서 사용합니다.
    /// </summary>
    public static NamedPipeServerStream CreateServer() => CreateServer(PipeName);

    /// <summary>
    /// 지정된 파이프 이름으로 서버 스트림을 만듭니다. 테스트 격리용입니다.
    /// </summary>
    public static NamedPipeServerStream CreateServer(string pipeName) =>
        new(pipeName, PipeDirection.InOut);
}
