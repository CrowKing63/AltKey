using System.Net;
using System.Text;
using System.Text.Json;
using AltKey.Services;

namespace AltKey.Tests.AiLocal;

// LocalGgufBackend의 chat-completion HTTP 로직을 스텁 서버로 검증하는 테스트입니다.
// (실제 llama-server 없이 요청 구성·응답 파싱을 확인)
public class LocalGgufBackendTests
{
    [Fact]
    // chat completion 요청을 보내고 응답의 content를 추출하는지 확인합니다(스텁 서버).
    public async Task SendChatCompletion_returnsContent_fromStubServer()
    {
        int port = SidecarProcessManager.FindFreePort(8090);
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        const string expectedContent = "안녕하세요";

        // 스텁 서버: /v1/chat/completions에 고정 응답을 반환합니다.
        var serverTask = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
                var resp = new
                {
                    choices = new[] { new { message = new { role = "assistant", content = expectedContent } } }
                };
                var buf = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp));
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.OutputStream.WriteAsync(buf);
                ctx.Response.Close();
            }
        });

        try
        {
            string result = await LocalGgufBackend.SendChatCompletionAsync(
                $"http://127.0.0.1:{port}", "test input", "system prompt", CancellationToken.None);
            Assert.Equal(expectedContent, result);
        }
        finally
        {
            listener.Stop(); // serverTask도 함께 종료됩니다.
        }
    }
}
