using System.Diagnostics;
using AltKey.Services;

namespace AltKey.Tests.Services;

/// <summary>
/// 메인 앱과 상주 Tools 프로세스 사이의 명명 파이프 명령 채널을 검증합니다.
/// </summary>
public class ToolsIpcTests
{
    /// <summary>
    /// 테스트마다 고유한 파이프 이름을 만들어, 실제로 떠 있는 상주 Tools 프로세스의
    /// 프로덕션 파이프와 절대 충돌하지 않게 합니다. (충돌하면 명령이 실제 앱에
    /// 전달되어 창이 열리고, 테스트도 실패합니다.)
    /// </summary>
    private static string CreateTestPipeName() =>
        "AltKey.Tests.Ipc." + Guid.NewGuid().ToString("N");

    /// <summary>
    /// 듣는 프로세스가 없으면 빠르게(false) 실패해야 폴백(새 프로세스 시작)이 매끄럽게 동작합니다.
    /// </summary>
    [Fact]
    public void TrySend_ReturnsFalse_WhenNoServerListening()
    {
        var pipeName = CreateTestPipeName();
        var stopwatch = Stopwatch.StartNew();
        var sent = ToolsIpc.TrySend("open layout", pipeName, timeoutMs: 300);
        stopwatch.Stop();

        Assert.False(sent);
        // 아무도 듣지 않는 파이프여도 오래 막히면 안 됩니다.
        Assert.True(stopwatch.ElapsedMilliseconds < 2500,
            $"TrySend가 {stopwatch.ElapsedMilliseconds}ms 동안 막혀 있었습니다.");
    }

    /// <summary>
    /// 서버가 듣고 있으면 명령 한 줄이 그대로 전달됩니다.
    /// </summary>
    [Fact]
    public void TrySend_DeliversMessage_WhenServerListening()
    {
        var pipeName = CreateTestPipeName();
        string? received = null;
        using var server = ToolsIpc.CreateServer(pipeName);
        var thread = new Thread(() =>
        {
            server.WaitForConnection();
            using var reader = new System.IO.StreamReader(server);
            received = reader.ReadLine();
        })
        { IsBackground = true };
        thread.Start();

        // 서버가 경청을 시작할 시간을 준 뒤, 실패하면 잠시 대기하며 재시도합니다.
        var sent = false;
        for (var attempt = 0; attempt < 10 && !sent; attempt++)
        {
            sent = ToolsIpc.TrySend("open layout", pipeName, timeoutMs: 500);
            if (!sent)
            {
                Thread.Sleep(100);
            }
        }

        Assert.True(sent, "서버가 듣고 있음에도 TrySend가 실패했습니다.");
        thread.Join(2000);
        Assert.Equal("open layout", received);
    }

    /// <summary>
    /// 파이프 이름에 사용자 이름이 포함된 특수 문자가 있어도 안전한 이름으로 정리됩니다.
    /// </summary>
    [Fact]
    public void SanitizePipeName_KeepsOnlyAllowedCharacters()
    {
        Assert.Equal("user_name", ToolsIpc.SanitizePipeName("user@name"));
        Assert.Equal("a.b_c", ToolsIpc.SanitizePipeName("a.b_c"));
    }
}
