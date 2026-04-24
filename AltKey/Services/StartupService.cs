using Microsoft.Win32;
using System.Diagnostics;

namespace AltKey.Services;

/// <summary>
/// [역할] 윈도우가 부팅될 때 AltKey가 자동으로 실행되도록 설정하는 서비스입니다.
/// [기능] 윈도우 레지스트리에 실행 파일 경로를 등록하거나 삭제하여 자동 실행 여부를 제어합니다.
/// </summary>
public class StartupService
{
    private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Run"; // 자동 실행 정보가 저장되는 레지스트리 경로입니다.
    private const string AppName = "AltKey";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegPath, writable: false);
            if (key?.GetValue(AppName) is not string rawValue) return false;
            // 인스톨러 및 Enable()이 따옴표 포함 경로("path")를 저장하므로 비교 전 제거
            var normalizedValue = rawValue.Trim('"');
            return normalizedValue.Equals(ExePath, StringComparison.OrdinalIgnoreCase);
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
