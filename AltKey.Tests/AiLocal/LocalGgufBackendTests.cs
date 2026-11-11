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
    // chat completion 요청에 max_tokens(출력 상한)가 포함되고, 응답 content를 추출하는지 확인합니다(스텁 서버).
    public async Task SendChatCompletion_includesMaxTokens_and_returnsContent()
    {
        int port = SidecarProcessManager.FindFreePort(8090);
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        const string expectedContent = "안녕하세요";
        string? capturedBody = null;

        // 스텁 서버: 요청 본문을 캡처하고 /v1/chat/completions에 고정 응답을 반환합니다.
        var serverTask = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
                using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                capturedBody = await reader.ReadToEndAsync();
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

            // 요청 본문에 max_tokens가 4096으로 설정됐는지 확인합니다.
            var req = JsonDocument.Parse(capturedBody!);
            Assert.Equal(4096, req.RootElement.GetProperty("max_tokens").GetInt32());
        }
        finally
        {
            listener.Stop(); // serverTask도 함께 종료됩니다.
        }
    }

    [Fact]
    // 컨텍스트 초과(HTTP 400) 에러를 받으면 한국어 actionable 안내가 throw되는지 확인합니다(스텁 서버).
    public async Task SendChatCompletion_ContextOverflow_ThrowsKoreanMessage()
    {
        int port = SidecarProcessManager.FindFreePort(8091);
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        // 서버가 보내는 컨텍스트 초과 에러 본문 (실측 llama-server 응답 형식).
        const string overflowBody = """{"error":{"code":400,"message":"request (15019 tokens) exceeds the available context size (8192 tokens), try increasing it","type":"exceed_context_size_error"}}""";

        var serverTask = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
                using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                await reader.ReadToEndAsync(); // 요청 본문 드레인
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "application/json";
                var buf = Encoding.UTF8.GetBytes(overflowBody);
                await ctx.Response.OutputStream.WriteAsync(buf);
                ctx.Response.Close();
            }
        });

        try
        {
            var ex = await Assert.ThrowsAsync<AiServiceException>(() =>
                LocalGgufBackend.SendChatCompletionAsync($"http://127.0.0.1:{port}", "hi", "sys", CancellationToken.None));
            Assert.Contains("너무 길어", ex.Message);
        }
        finally
        {
            listener.Stop();
        }
    }

    // ── idle 언로드 / UnloadAsync (사이드카 라이프사이클) ───────────────────────

    /// <summary>
    /// [역할] ISidecarProcess 스텁 — StartAsync/DisposeAsync 호출을 추적하고
    /// IsRunning 상태를 조작할 수 있습니다.
    /// </summary>
    private sealed class FakeSidecar : ISidecarProcess
    {
        public bool IsRunning { get; set; }
        public int StartCalls { get; private set; }
        public int DisposeCalls { get; private set; }

        public string BaseUrl => "http://127.0.0.1:9";

        public Task StartAsync(string enginePath, string modelPath, CancellationToken ct)
        {
            StartCalls++;
            IsRunning = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task UnloadAsync_StopsSidecar_And_FiresUnloaded()
    {
        var sidecar = new FakeSidecar { IsRunning = true };
        await using var backend = new LocalGgufBackend(sidecar, null!, null!);

        var unloadedFired = false;
        backend.Unloaded += () => unloadedFired = true;

        await backend.UnloadAsync();

        Assert.False(sidecar.IsRunning);
        Assert.Equal(1, sidecar.DisposeCalls);
        Assert.True(unloadedFired);
    }

    [Fact]
    public async Task UnloadAsync_WhenNotRunning_IsNoOp()
    {
        var sidecar = new FakeSidecar { IsRunning = false };
        await using var backend = new LocalGgufBackend(sidecar, null!, null!);

        await backend.UnloadAsync();

        Assert.Equal(0, sidecar.DisposeCalls); // 미로드 — 사이드카 안 건드림
    }

    [Fact]
    public async Task IdleTimeout_UnloadsSidecar()
    {
        var sidecar = new FakeSidecar { IsRunning = true };
        // 200ms idle 타임아웃 — 추론 완료 후 그 이상 새 요청 없으면 언로드.
        await using var backend = new LocalGgufBackend(sidecar, null!, null!, TimeSpan.FromMilliseconds(200));

        // 추론 완료 시점과 동일하게 idle 타이머 시작.
        backend.RestartIdleTimer();

        await Task.Delay(600); // 200ms 타이머 + 여유

        Assert.False(sidecar.IsRunning);
        Assert.Equal(1, sidecar.DisposeCalls);
    }

    [Fact]
    public async Task NewRequest_CancelsIdleTimer()
    {
        var sidecar = new FakeSidecar { IsRunning = true };
        await using var backend = new LocalGgufBackend(sidecar, null!, null!, TimeSpan.FromMilliseconds(200));

        backend.RestartIdleTimer();
        // 100ms 후 새 요청 → 타이머 취소 (언로드 안 함).
        await Task.Delay(100);
        backend.CancelIdleTimer();

        await Task.Delay(400); // 타이머가 취소됐으므로 언로드 안 됨.

        Assert.True(sidecar.IsRunning);
        Assert.Equal(0, sidecar.DisposeCalls);
    }

    [Fact]
    public async Task IdleZero_UnloadsImmediately()
    {
        var sidecar = new FakeSidecar { IsRunning = true };
        // 0분 idle — 타이머 즉시 만료 → 언로드 (추론 직후 즉시 회수).
        await using var backend = new LocalGgufBackend(sidecar, null!, null!, TimeSpan.Zero);

        backend.RestartIdleTimer();

        await Task.Delay(100); // 0ms 타이머 + 여유

        Assert.False(sidecar.IsRunning);
        Assert.Equal(1, sidecar.DisposeCalls);
    }
}
