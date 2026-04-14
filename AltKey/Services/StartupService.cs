using Microsoft.Win32;
using System.Diagnostics;

namespace AltKey.Services;

public class StartupService
{
    private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AltKey";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: false);
            return key?.GetValue(AppName) is string path
                && path.Equals(ExePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true)
            ?? throw new InvalidOperationException("레지스트리 키를 열 수 없습니다.");
        key.SetValue(AppName, $"\"{ExePath}\"");
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static string ExePath
    {
        get
        {
            // single-file publish 시 AppContext.BaseDirectory가 임시 디렉터리일 수 있음
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
                return processPath;

            // 폴백: 프로세스 메인 모듈
            try
            {
                return Process.GetCurrentProcess().MainModule?.FileName
                    ?? throw new InvalidOperationException("실행 파일 경로를 확인할 수 없습니다.");
            }
            catch
            {
                throw new InvalidOperationException("실행 파일 경로를 확인할 수 없습니다.");
            }
        }
    }
}
