using System.Net;
using AltKey.Services;

namespace AltKey.Tests.AiLocal;

// ModelStore의 SHA-256 검증과 경로 파생 동작을 검증하는 테스트입니다.
public class ModelStoreTests
{
    [Fact]
    // 파일의 SHA-256이 기대값과 일치하면 true, 다르면 false인지 확인합니다.
    public async Task VerifySha256_returnsTrue_onMatch_false_onMismatch()
    {
        string tempDir = CreateTempDataDir();
        try
        {
            PathResolver.OverrideDataDir(tempDir);
            string path = Path.Combine(tempDir, "sample.bin");
            byte[] data = [1, 2, 3, 4, 5];
            await File.WriteAllBytesAsync(path, data);

            // ground truth: 실제 SHA-256을 계산해 기대값으로 씁니다.
            string expected = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
            var store = new ModelStore();

            Assert.True(await store.VerifySha256Async(path, expected));
            Assert.False(await store.VerifySha256Async(path, "deadbeef"));
        }
        finally
        {
            PathResolver.OverrideDataDir(null);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    // 엔진·모델 경로가 DataDir 기준(engine\, models\)으로 파생되는지 확인합니다.
    public void Paths_deriveFromDataDir()
    {
        string tempDir = CreateTempDataDir();
        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var store = new ModelStore();

            Assert.Equal(Path.Combine(tempDir, "engine", "llama-server.exe"), store.EnginePath);
            Assert.Equal(
                Path.Combine(tempDir, "models", "EXAONE-4.0-1.2B-Q8_0.gguf"),
                store.ModelPath("EXAONE-4.0-1.2B-Q8_0.gguf"));
        }
        finally
        {
            PathResolver.OverrideDataDir(null);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    // 파일을 내려받아 SHA-256이 일치하면 저장하고, 다르면 예외를 던지는지 확인합니다(스텁 서버).
    public async Task DownloadAsync_savesFile_onShaMatch_throws_onMismatch()
    {
        int port = SidecarProcessManager.FindFreePort(8091);
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        byte[] fileBytes = [1, 2, 3, 4, 5, 6, 7, 8];
        string expectedSha = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(fileBytes)).ToLowerInvariant();

        var serverTask = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
                ctx.Response.StatusCode = 200;
                await ctx.Response.OutputStream.WriteAsync(fileBytes);
                ctx.Response.Close();
            }
        });

        string tempDir = CreateTempDataDir();
        try
        {
            PathResolver.OverrideDataDir(tempDir);
            var store = new ModelStore();
            string dest = Path.Combine(tempDir, "models", "test.gguf");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

            // SHA 일치 → 파일 저장.
            await store.DownloadAsync($"http://127.0.0.1:{port}/file", dest, expectedSha);
            Assert.True(File.Exists(dest));
            Assert.True(await store.VerifySha256Async(dest, expectedSha));

            // SHA 불일치 → 예외 (파일이 손상된 것으로 간주).
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.DownloadAsync($"http://127.0.0.1:{port}/file", dest, "deadbeef"));
        }
        finally
        {
            listener.Stop();
            PathResolver.OverrideDataDir(null);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDataDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AltKeyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }
}
