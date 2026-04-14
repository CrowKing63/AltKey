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
}
