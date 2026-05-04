using System.IO;

namespace AltKey.Services;

public static class PathResolver
{
    private static readonly string _exeDir =
        Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";

    /// <summary>exe 옆에 config.json이 있으면 포터블 모드</summary>
    public static bool IsPortable =>
        File.Exists(Path.Combine(_exeDir, "config.json"));

    public static string DataDir => IsPortable
        ? _exeDir
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AltKey");

    public static string LayoutsDir => Path.Combine(DataDir, "layouts");
    public static string ConfigPath  => Path.Combine(DataDir, "config.json");

    /// <summary>
    /// AltKey.Tools 실행 파일 경로입니다.
    /// [배포] 우선 메인 앱과 같은 폴더를 찾고, 없으면 Tools 하위 폴더를 확인합니다.
    /// [개발] 로컬 실행 중에는 프로젝트 출력 폴더도 함께 확인해 설정 창에서 바로 테스트할 수 있게 합니다.
    /// </summary>
    public static string ToolsExePath
    {
        get
        {
            var sameDirectory = Path.Combine(_exeDir, "AltKey.Tools.exe");
            if (File.Exists(sameDirectory))
            {
                return sameDirectory;
            }

            var toolsSubDirectory = Path.Combine(_exeDir, "Tools", "AltKey.Tools.exe");
            if (File.Exists(toolsSubDirectory))
            {
                return toolsSubDirectory;
            }

            // 개발 중에는 메인 앱 출력 폴더에서 저장소 기준 도구 출력 폴더를 함께 탐색합니다.
            var projectRoot = Directory.GetParent(_exeDir)?.Parent?.Parent?.Parent?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var configurationName = new DirectoryInfo(_exeDir).Parent?.Parent?.Name;
                var tfmName = new DirectoryInfo(_exeDir).Parent?.Name;
                if (!string.IsNullOrEmpty(configurationName) && !string.IsNullOrEmpty(tfmName))
                {
                    var developmentPath = Path.Combine(
                        projectRoot,
                        "AltKey.Tools",
                        "bin",
                        configurationName,
                        tfmName,
                        "AltKey.Tools.exe");

                    if (File.Exists(developmentPath))
                    {
                        return developmentPath;
                    }
                }
            }

            return sameDirectory;
        }
    }
}
