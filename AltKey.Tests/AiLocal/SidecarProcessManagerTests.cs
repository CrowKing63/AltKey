using System.Net;
using System.Net.Sockets;
using AltKey.Services;

namespace AltKey.Tests.AiLocal;

// SidecarProcessManager의 포트 선택(FindFreePort) 동작을 검증하는 테스트입니다.
public class SidecarProcessManagerTests
{
    [Fact]
    // 시작 포트가 비어 있으면 그대로 반환하는지 확인합니다.
    public void FindFreePort_returnsStartPort_whenFree()
    {
        int port = GetAnOpenPort();
        Assert.Equal(port, SidecarProcessManager.FindFreePort(port));
    }

    [Fact]
    // 시작 포트가 이미 쓰이면 그 포트를 건너뛰어 다른 포트를 반환하는지 확인합니다.
    public void FindFreePort_skipsOccupiedPort()
    {
        // 특정 포트를 바인딩(점유)해서 "쓰임" 상태를 만듭니다.
        var listener = new TcpListener(IPAddress.Loopback, GetAnOpenPort());
        listener.Start();
        int occupied = (listener.Server.LocalEndPoint as IPEndPoint)!.Port;

        try
        {
            int result = SidecarProcessManager.FindFreePort(occupied);
            Assert.NotEqual(occupied, result);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    // llama-server 시작 인자에 모델 경로·포트·GPU 비활성(--gpu-layers 0)이 포함되는지 확인합니다.
    public void BuildStartArguments_includesModel_port_and_gpuDisabled()
    {
        var args = SidecarProcessManager.BuildStartArguments(@"C:\models\EXAONE.gguf", 8096);

        int mIdx = Array.IndexOf(args, "-m");
        Assert.True(mIdx >= 0);
        Assert.Equal(@"C:\models\EXAONE.gguf", args[mIdx + 1]);

        int pIdx = Array.IndexOf(args, "--port");
        Assert.True(pIdx >= 0);
        Assert.Equal("8096", args[pIdx + 1]);

        // CPU 전용: --gpu-layers 0이 반드시 들어갑니다.
        int gIdx = Array.IndexOf(args, "--gpu-layers");
        Assert.True(gIdx >= 0);
        Assert.Equal("0", args[gIdx + 1]);

        // 컨텍스트 크기는 -c(=--ctx-size)로만 전달합니다. (--context-size은 b12262가 인식 못 함.)
        int cIdx = Array.IndexOf(args, "-c");
        Assert.True(cIdx >= 0);
        Assert.Equal("8192", args[cIdx + 1]);

        // Gemma 4 E2B는 reasoning 모델 — thinking 토큰을 끕니다(속도·출력 품질).
        int rIdx = Array.IndexOf(args, "--reasoning");
        Assert.True(rIdx >= 0);
        Assert.Equal("off", args[rIdx + 1]);

        // 멀티모달 mmproj(비전) 자동 로딩을 명시적으로 꺼 RAM을 절약합니다.
        int vIdx = Array.IndexOf(args, "--no-mmproj");
        Assert.True(vIdx >= 0);
    }

    // 바인드해서 실제로 쓸 수 있는 포트를 하나 얻어옵니다. (테스트 헬퍼)
    private static int GetAnOpenPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = (l.Server.LocalEndPoint as IPEndPoint)!.Port;
        l.Stop();
        return p;
    }
}
