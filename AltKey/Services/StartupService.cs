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

    /// <summary>
    /// [기능] 스토어(MSIX) 패키지로 실행 중인지 판별합니다.
    /// 스토어 앱은 실행 파일이 WindowsApps 폴더 안에 설치되며, 그 안의 파일을
    /// 로그인 시 직접 실행하는 것은 윈도우가 차단합니다. 그래서 스토어 버전은
    /// 레지스트리 Run 키 대신 매니페스트의 시작 프로그램 등록을 사용해야 합니다.
    /// </summary>
    public static bool IsPackaged
    {
        get
        {
            // 실행 파일 경로에 WindowsApps가 포함되면 스토어(MSIX) 설치로 간주합니다.
            return Environment.ProcessPath?.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase) == true;
        }
    }

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
