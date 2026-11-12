using System.IO;
using AltKey.Services;

namespace AltKey.Tests;

/// <summary>
/// App.LogError의 크기 기반 로테이션(T-6.7)을 검증한다.
/// </summary>
public class AppErrorLogTests : IDisposable
{
    private readonly string _tempDir;

    public AppErrorLogTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("altkey-log-test").FullName;
        PathResolver.OverrideDataDir(_tempDir);
    }

    public void Dispose()
    {
        PathResolver.OverrideDataDir(null);
        TryDeleteDirectory(_tempDir);
    }

    [Fact]
    public void LogError_AppendsEntry_ToLogFile()
    {
        // 로테이션 도입 후에도 기본 추가(append) 동작이 유지되는지 검증한다.
        App.LogError(new InvalidOperationException("테스트 오류"));

        var logPath = Path.Combine(_tempDir, "altkey-error.log");
        Assert.True(File.Exists(logPath));
        var content = File.ReadAllText(logPath);
        Assert.Contains("테스트 오류", content);
    }

    [Fact]
    public void LogError_RotatesWhenLogExceedsSizeLimit()
    {
        // 5MB 한도를 넘긴 로그 상태에서 LogError를 호출하면
        // 기존 파일이 .old로 이동하고 새 파일에 최신 항목만 남는지 검증한다.
        var logPath = Path.Combine(_tempDir, "altkey-error.log");
        File.WriteAllText(logPath, new string('x', 6 * 1024 * 1024));

        App.LogError(new InvalidOperationException("회전 테스트"));

        Assert.True(File.Exists(logPath + ".old"));
        var content = File.ReadAllText(logPath);
        Assert.Contains("회전 테스트", content);
        Assert.True(content.Length < 1024 * 1024);
    }

    [Fact]
    public void LogError_KeepsOnlyOneOldGeneration()
    {
        // .old가 이미 존재해도 이전 세대는 하나만 유지되는지 검증한다.
        var logPath = Path.Combine(_tempDir, "altkey-error.log");
        var oldPath = logPath + ".old";
        File.WriteAllText(logPath, new string('x', 6 * 1024 * 1024));
        File.WriteAllText(oldPath, new string('y', 6 * 1024 * 1024));

        App.LogError(new InvalidOperationException("세대 테스트"));

        // .old가 현재 로그(큰 x 파일)로 교체되어야 한다.
        Assert.Equal(new string('x', 6 * 1024 * 1024), File.ReadAllText(oldPath));
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); }
        catch { /* 테스트 정리 실패는 무시 */ }
    }
}
