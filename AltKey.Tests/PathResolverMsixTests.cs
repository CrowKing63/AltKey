using AltKey.Services;

namespace AltKey.Tests;

/// <summary>
/// PathResolver.IsMsixPath — 스토어(MSIX) 설치판 감지 판정 로직 테스트.
/// 스토어 앱은 C:\Program Files\WindowsApps\ 아래에 설치되므로,
/// 실행 파일 경로에 \WindowsApps\ 디렉터리가 포함되면 스토어 설치판으로 판정한다.
/// </summary>
public class PathResolverMsixTests
{
    [Fact]
    public void IsMsixPath_store_path_returns_true()
    {
        var storeExeDir = @"C:\Program Files\WindowsApps\CrowKing63.AltKey_1.1.12.0_x64__pqr5k4m8wqz7j";
        Assert.True(PathResolver.IsMsixPath(storeExeDir));
    }

    [Fact]
    public void IsMsixPath_regular_program_files_path_returns_false()
    {
        var installedExeDir = @"C:\Program Files\AltKey";
        Assert.False(PathResolver.IsMsixPath(installedExeDir));
    }

    [Fact]
    public void IsMsixPath_portable_path_returns_false()
    {
        var portableExeDir = @"C:\Users\uitaek\AltKey-Portable";
        Assert.False(PathResolver.IsMsixPath(portableExeDir));
    }

    [Fact]
    public void IsMsixPath_is_case_insensitive()
    {
        var lowerCaseStoreExeDir = @"c:\program files\windowsapps\CrowKing63.AltKey_1.0_x64__abc";
        Assert.True(PathResolver.IsMsixPath(lowerCaseStoreExeDir));
    }

    [Fact]
    public void IsMsixPath_bare_windowsapps_substring_is_not_enough()
    {
        // 'WindowsApps'가 단순 부분문자일 뿐 \WindowsApps\ 디렉터리 형태가 아니면 스토어가 아니다.
        var path = @"C:\Users\uitaek\MyWindowsAppsBackup";
        Assert.False(PathResolver.IsMsixPath(path));
    }

    [Fact]
    public void IsPortablePath_store_like_path_with_config_json_returns_false()
    {
        // 스토어 설치판은 config.json이 exe 옆에 있어도 포터블 모드가 아니다.
        // (테스트 머신에는 실제 WindowsApps 경로를 만들 수 없어, 경로 형태만 흉내낸다.)
        var dir = Path.Combine(Path.GetTempPath(), "WindowsApps", "altkey-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), "{}");
        try
        {
            Assert.True(PathResolver.IsMsixPath(dir));
            Assert.False(PathResolver.IsPortablePath(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void IsPortablePath_regular_path_with_config_json_returns_true()
    {
        var dir = CreateTempDirWithConfig();
        try
        {
            Assert.True(PathResolver.IsPortablePath(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void IsPortablePath_regular_path_without_config_json_returns_false()
    {
        var dir = Path.Combine(Path.GetTempPath(), "altkey-portable-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.False(PathResolver.IsPortablePath(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static string CreateTempDirWithConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "altkey-portable-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), "{}");
        return dir;
    }
}
