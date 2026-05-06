using System.Diagnostics;
using System.IO;

namespace AltKey.Services;

/// <summary>
/// [역할] Windows 기본 화상 키보드(OSK)를 AltKey에서 직접 실행하는 서비스입니다.
/// [기능] 포커스된 앱이 Win+Ctrl+O 단축키를 가로채더라도, 실행 파일을 직접 호출해 접근성 동작이 막히지 않도록 우선 시도합니다.
/// </summary>
public class OskLauncherService
{
    /// <summary>
    /// OSK 실행을 시도합니다.
    /// </summary>
    /// <returns>실행 요청을 정상적으로 보냈으면 true, 모든 경로가 실패하면 false</returns>
    public virtual bool TryLaunch()
    {
        foreach (var candidate in EnumerateCandidates())
        {
            if (TryLaunchCandidate(candidate))
                return true;
        }

        return false;
    }

    /// <summary>
    /// OSK 실행 후보 경로를 순서대로 제공합니다.
    /// 경로를 수정할 때에는 첫 번째 후보가 실패했을 때만 다음 후보로 넘어간다는 점을 함께 고려해야 합니다.
    /// </summary>
    protected virtual IEnumerable<string> EnumerateCandidates()
    {
        var systemDirectory = Environment.SystemDirectory;
        if (!string.IsNullOrWhiteSpace(systemDirectory))
            yield return Path.Combine(systemDirectory, "osk.exe");

        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDirectory))
            yield return Path.Combine(windowsDirectory, "System32", "osk.exe");

        // 마지막 후보는 PATH 해석에 맡겨, 특수한 Windows 환경에서도 한 번 더 시도합니다.
        yield return "osk.exe";
    }

    /// <summary>
    /// 후보 경로 하나를 실제로 실행합니다.
    /// 테스트에서는 이 메서드를 재정의해 어떤 경로가 먼저 시도되는지 검증할 수 있습니다.
    /// </summary>
    protected virtual bool TryLaunchCandidate(string candidate)
    {
        try
        {
            Process.Start(new ProcessStartInfo(candidate)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OskLauncher] 실행 실패: {candidate} / {ex.Message}");
            return false;
        }
    }
}
