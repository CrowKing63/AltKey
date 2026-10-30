using System.IO;

namespace AltKey.Services;

public static class PathResolver
{
    private static readonly string _exeDir =
        Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";
    private static string? _overrideDataDir;

    /// <summary>실행 파일이 있는 폴더 경로입니다. (레이아웃 시드 등 앱 자원 탐색에 사용)</summary>
    public static string ExeDir => _exeDir;

    /// <summary>
    /// exe 옆에 config.json이 있으면 포터블 모드.
    /// [배포] 스토어(MSIX) 설치판은 항상 %AppData% 기반 데이터 폴더를 사용하므로,
    /// 패키지에 번들된 config.json 때문에 포터블 모드로 오인하는 것을 막는다.
    /// </summary>
    public static bool IsPortable => IsPortablePath(_exeDir);

    /// <summary>[테스트] exe 경로 기준으로 포터블 모드 판정 (스토어 설치판 제외).</summary>
    internal static bool IsPortablePath(string exeDir) =>
        !IsMsixPath(exeDir) && File.Exists(Path.Combine(exeDir, "config.json"));

    /// <summary>
    /// MSIX(Microsoft Store) 설치판 감지. 스토어 앱은 C:\Program Files\WindowsApps\ 아래에 설치되므로
    /// 실행 파일 경로에 \WindowsApps\이 있으면 스토어 설치판으로 판정한다.
    /// [업데이트] 스토어 설치판은 Microsoft Store가 자동으로 업데이트하므로 GitHub 자동 업데이트를 생략한다.
    /// </summary>
    public static bool IsMsix => IsMsixPath(_exeDir);

    /// <summary>[테스트] 실행 파일 경로가 스토어(WindowsApps) 아래에 있는지 판정한다.</summary>
    internal static bool IsMsixPath(string exeDir) =>
        exeDir.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 메인 앱이 도구 앱을 열 때 같은 데이터 폴더를 명시적으로 공유해야 할 경우 사용합니다.
    /// 개발 환경의 분리된 bin 폴더뿐 아니라, 실행 주체와 관계없이 동일한 설정/레이아웃을 보장하는 공용 진입점입니다.
    /// </summary>
    public static void OverrideDataDir(string? dataDir)
    {
        _overrideDataDir = string.IsNullOrWhiteSpace(dataDir) ? null : dataDir;
    }

    public static string DataDir => !string.IsNullOrWhiteSpace(_overrideDataDir)
        ? _overrideDataDir
        : IsPortable
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
