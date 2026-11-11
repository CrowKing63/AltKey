using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace AltKey.Services;

/// <summary>
/// [역할] 로컬 AI 엔진·모델 파일의 저장 경로, SHA-256 검증, manifest 관리를 담당하는 클래스입니다.
/// 경로는 PathResolver.DataDir 기준이라 포터블/설치판이 자동 분기됩니다.
/// </summary>
public class ModelStore
{
    // 엔진 저장 경로 (DataDir\engine\llama-server.exe).
    public string EnginePath => Path.Combine(PathResolver.DataDir, "engine", "llama-server.exe");

    // 모델 저장 경로 (DataDir\models\<fileName>).
    public string ModelPath(string fileName) => Path.Combine(PathResolver.DataDir, "models", fileName);

    // manifest.json 경로 (DataDir\manifest.json).
    public string ManifestPath => Path.Combine(PathResolver.DataDir, "manifest.json");

    /// <summary>
    /// [역할] 파일의 SHA-256을 계산해 기대값과 일치하는지 확인합니다(대소문자 구분 없음).
    /// </summary>
    public async Task<bool> VerifySha256Async(string filePath, string expectedSha256)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);
        string actual = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// [역할] url에서 파일을 내려받아 destinationPath에 저장합니다.
    /// .part 임시 파일로 받아 SHA-256을 검증한 뒤 최종 이름으로 이동합니다.
    /// 재개(resume) 지원: 기존 .part가 있으면 Range 헤더로 이어서 받습니다.
    /// </summary>
    public async Task DownloadAsync(string url, string destinationPath, string expectedSha256,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string partPath = destinationPath + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        using var httpClient = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // 재개: 기존 .part 크기가 있으면 그 위치부터 이어서 받습니다.
        long resumeFrom = 0;
        if (File.Exists(partPath))
        {
            resumeFrom = new FileInfo(partPath).Length;
            request.Headers.Add("Range", $"bytes={resumeFrom}-");
        }

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        // 200(새 시작) 또는 206(Partial Content, 재개) 둘 다 허용.
        if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent)
            throw new InvalidOperationException($"다운로드 실패 (HTTP {(int)response.StatusCode}).");

        bool isResume = response.StatusCode == HttpStatusCode.PartialContent;
        long totalBytes = resumeFrom + (response.Content.Headers.ContentLength ?? 0);

        var fileStream = new FileStream(
            partPath, isResume ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);
        try
        {
            var contentStream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[81920];
            long received = resumeFrom;
            int read;
            while ((read = await contentStream.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                received += read;
                if (totalBytes > 0)
                    progress?.Report(Math.Min(100.0, received * 100.0 / totalBytes));
            }
            await fileStream.FlushAsync(ct);
        }
        finally
        {
            // SHA 검증 전에 반드시 닫습니다(Windows 파일 공유 충돌 방지).
            await fileStream.DisposeAsync();
        }

        // SHA-256 검증 — 일치하지 않으면 손상된 파일로 간주하고 임시 파일을 삭제합니다.
        if (!await VerifySha256Async(partPath, expectedSha256))
        {
            File.Delete(partPath);
            throw new InvalidOperationException("다운로드된 파일의 SHA-256이 일치하지 않습니다. (파일이 손상되었거나 잘못되었을 수 있음)");
        }

        // 최종 이름으로 이동(덮어쓰기).
        File.Move(partPath, destinationPath, overwrite: true);
    }
}
