using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace AltKey.Services;

/// <summary>
/// [역할] 로컬 AI용 llama-server.exe 사이드카 프로세스를 시작/종료하고 health를 확인하는管理类입니다.
/// GPU는 전혀 쓰지 않고 CPU 전용으로 돌립니다(--gpu-layers 0).
/// [참고] StartAsync/StopAsync/WaitForHealthyAsync는 실제 프로세스와 HTTP를 다루는 통합 레벨이라
/// 단위 테스트로 검증하지 않습니다(단위 테스트는 FindFreePort·BuildStartArguments로 커버).
/// 서버 동작 자체는 PoC(llama-server b12262, EXAONE Q8_0)에서 실측으로 확인했습니다.
/// </summary>
public class SidecarProcessManager : IAsyncDisposable
{
    private Process? _process;
    private HttpClient? _healthClient;
    private int _port;

    // 현재 서버가 실행 중인지.
    public bool IsRunning => _process is { HasExited: false };

    // 서버의 기본 URL — LocalGgufBackend가 같은 host:port로 추론 요청을 보냅니다.
    public string BaseUrl => $"http://127.0.0.1:{_port}";

    /// <summary>
    /// [역할] startPort부터 시작해 실제로 바인드 가능한(비어 있는) 포트 하나를 찾습니다.
    /// maxCandidates까지 시도해도 안 있으면 예외를 던집니다.
    /// </summary>
    public static int FindFreePort(int startPort, int maxCandidates = 50)
    {
        for (int i = 0; i < maxCandidates; i++)
        {
            int port = startPort + i;
            try
            {
                // 바인드 성공하면 그 포트는 비어 있다는 뜻입니다. 바로 닫고 반환합니다.
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
                // 이미 점유 중이므로 다음 포트를 시도합니다.
            }
        }
        throw new InvalidOperationException($"사용 가능한 포트를 찾지 못했습니다 (시작: {startPort}).");
    }

    /// <summary>
    /// [역할] llama-server.exe 시작 인자 목록을 구성합니다.
    /// GPU는 --gpu-layers 0으로 완전 비활성(CPU 전용)이고, loopback(127.0.0.1)에만 바인드합니다.
    /// </summary>
    public static string[] BuildStartArguments(string modelPath, int port)
    {
        return
        [
            "-m", modelPath,
            "--host", "127.0.0.1",
            "--port", port.ToString(),
            "--gpu-layers", "0",
            // 컨텍스트 크기는 -c/--ctx-size로만 받음. (--context-size은 b12262가 인식 못 해 즉시 종료.)
            "-c", "8192"
        ];
    }

    /// <summary>
    /// [역할] llama-server.exe를 시작하고, health가 OK가 될 때까지(모델 로드 포함) 대기합니다.
    /// 준비가 120초 안에 안 되면 타임아웃 예외를 던집니다.
    /// </summary>
    public async Task StartAsync(string enginePath, string modelPath, CancellationToken ct)
    {
        if (IsRunning)
            throw new InvalidOperationException("llama-server가 이미 실행 중입니다.");

        _port = FindFreePort(8080);

        var psi = new ProcessStartInfo
        {
            FileName = enginePath,
            UseShellExecute = false,
            CreateNoWindow = true // 콘솔 창 없이 백그라운드 실행
        };
        foreach (var a in BuildStartArguments(modelPath, _port))
            psi.ArgumentList.Add(a);

        _process = new Process { StartInfo = psi };
        _process.Start();

        try
        {
            await WaitForHealthyAsync(ct);
        }
        catch
        {
            // health가 안 되는 경우(타임아웃·취소·프로세스 즉시 사망) 프로세스를 반드시 정리합니다.
            // 그대로 두면 포트·RAM을 점유한 좀비 프로세스가 남습니다.
            await StopAsync();
            throw;
        }
    }

    /// <summary>
    /// [역할] health 엔드포인트가 200 OK를 반환할 때까지 폴링합니다(500ms 간격, 120초 한도).
    /// </summary>
    private async Task WaitForHealthyAsync(CancellationToken ct)
    {
        _healthClient ??= new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await _healthClient.GetAsync($"{BaseUrl}/health", ct);
                if (resp.StatusCode == HttpStatusCode.OK)
                    return;
            }
            catch (HttpRequestException)
            {
                // 서버가 아직 안 띄어 있는 상태 → 폴링을 계속합니다.
            }
            await Task.Delay(500, ct);
        }
        throw new TimeoutException("llama-server가 준비되지 않았습니다 (health 타임아웃).");
    }

    /// <summary>
    /// [역할] llama-server.exe를 종료하고 자원을 정리합니다. 이미 종료됐으면 아무것도 안 합니다.
    /// </summary>
    public async Task StopAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process?.Dispose();
        _process = null;
        _healthClient?.Dispose();
        _healthClient = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
