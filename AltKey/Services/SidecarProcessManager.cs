using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace AltKey.Services;

/// <summary>
/// [역할] 사이드카(llama-server) 프로세스의 라이프사이클을 추상한 인터페이스입니다.
/// LocalGgufBackend가 이 인터페이스에만 의존해 fake로 단위 테스트할 수 있습니다.
/// </summary>
public interface ISidecarProcess
{
    bool IsRunning { get; }
    string BaseUrl { get; }
    Task StartAsync(string enginePath, string modelPath, CancellationToken ct);
    ValueTask DisposeAsync();
}

/// <summary>
/// [역할] 로컬 AI용 llama-server.exe 사이드카 프로세스를 시작/종료하고 health를 확인하는管理类입니다.
/// GPU는 전혀 쓰지 않고 CPU 전용으로 돌립니다(--gpu-layers 0).
/// [참고] StartAsync/StopAsync/WaitForHealthyAsync는 실제 프로세스와 HTTP를 다루는 통합 레벨이라
/// 단위 테스트로 검증하지 않습니다(단위 테스트는 FindFreePort·BuildStartArguments로 커버).
/// 서버 동작 자체는 PoC(llama-server b12262, Gemma 4 E2B)에서 실측으로 확인했습니다.
/// </summary>
public class SidecarProcessManager : ISidecarProcess
{
    private Process? _process;
    private HttpClient? _healthClient;
    private int _port;
    // 시작 동기화 게이트 — 병행 ✨ 요청이 중복 사이드카를 띄우는 것을 막습니다(플래그).
    private readonly SemaphoreSlim _startGate = new(1, 1);

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
            "-c", "8192",
            // Gemma 4 E2B는 reasoning(추론) 모델 — thinking 토큰을 끄면 속도가 ~20s → ~1.5s로
            // 빨라지고 출력도 깔끔해진다(PoC 실측). 텍스트 전용 ✨에는 thinking이 필요 없다.
            "--reasoning", "off",
            // Gemma는 멀티모달 모델 — mmproj(비전 프로젝터) 자동 로딩을 명시적으로 꺼 RAM을 절약한다.
            "--no-mmproj"
        ];
    }

    /// <summary>
    /// [역할] llama-server.exe를 시작하고, health가 OK가 될 때까지(모델 로드 포함) 대기합니다.
    /// 준비가 120초 안에 안 되면 타임아웃 예외를 던집니다.
    /// </summary>
    public async Task StartAsync(string enginePath, string modelPath, CancellationToken ct)
    {
        // 게이트로 시작을 직렬화합니다 — 첫 요청이 health 대기 중일 때 두 번째 ✨가
        // 중복 프로세스를 띄우지 않고, 이미 실행 중이면 바로 반환합니다.
        await _startGate.WaitAsync(ct);
        try
        {
            if (IsRunning)
                return; // 이미 로드됨 — no-op (중복 시작 방지)

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
                // (게이트를 이미 가진 상태이므로 StopCoreAsync로 직접 정리 — 재진입 방지.)
                await StopCoreAsync();
                throw;
            }
        }
        finally
        {
            _startGate.Release();
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
        // 게이트로 시작과 정리를 직렬화합니다 — 언로드 중 새 ✨가 포트 충돌 없이 대기합니다.
        await _startGate.WaitAsync();
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            _startGate.Release();
        }
    }

    // 게이트를 이미 가진 상태에서 호출하는 내부 정리(StartAsync의 catch·Dispose 등).
    // 재진입 방지용 — public StopAsync는 이 메서드를 감싸게이트를 잡습니다.
    private async Task StopCoreAsync()
    {
        var proc = _process;
        _process = null; // IsRunning이 즉시 정확해집니다 — 상태 플래그가 먼저 갱신됩니다.
        if (proc is { HasExited: false })
        {
            try
            {
                proc.Kill(entireProcessTree: true);
                await proc.WaitForExitAsync();
            }
            catch
            {
                // Kill 실패(프로세스 이미 종료 등) — 자원은 아래 Dispose로 정리합니다.
            }
        }
        proc?.Dispose();
        _healthClient?.Dispose();
        _healthClient = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
