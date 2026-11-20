using System.Diagnostics;
using System.IO;

namespace AltKey.Services;

/// <summary>
/// [역할] Windows 기본 화상 키보드(OSK)를 AltKey에서 직접 실행하고, 열기/닫기 토글을 담당하는 서비스입니다.
/// [기능] 포커스된 앱이 Win+Ctrl+O 단축키를 가로채더라도, 실행 파일을 직접 호출해 접근성 동작이 막히지 않도록 우선 시도합니다.
/// [참고] OSK가 이미 열려 있을 때는 닫기 위해 Win+Ctrl+O 단축키를 보냅니다.
///        OSK는 관리자 권한으로 자동 승격되어 실행되므로, 프로세스 종료나 창 닫기 메시지로는 닫을 수 없기 때문입니다.
/// </summary>
public class OskLauncherService
{
    /// <summary>
    /// OSK 창이 현재 화면(가상 데스크톱)에 열려 있는지 확인합니다.
    /// "OSKMainClass"는 Windows 화상 키보드 창의 고유 클래스 이름입니다.
    /// 테스트에서는 이 메서드를 재정의해 열림/닫힘 상태를 흉내 낼 수 있습니다.
    /// </summary>
    public virtual bool IsRunning()
        => Platform.Win32.FindWindow("OSKMainClass", null) != IntPtr.Zero;

    /// <summary>
    /// OSK를 토글합니다: 열려 있으면 닫기 단축키를 보내고, 닫혀 있으면 직접 실행을 시도합니다.
    /// 직접 실행이 실패하면 같은 단축키로 한 번 더 시도합니다(열기 목적).
    /// </summary>
    /// <param name="sendToggleHotkey">Win+Ctrl+O 단축키를 보내는 동작 (호출부가 InputService로 배선)</param>
    /// <returns>수행한 동작 — Open: 열기 시도, Close: 닫기 요청</returns>
    public virtual OskToggleResult Toggle(Action sendToggleHotkey)
    {
        if (IsRunning())
        {
            // 이미 열려 있음 → OS 단축키로 닫기 (재실행은 아무 효과가 없으므로 실행하지 않습니다)
            sendToggleHotkey();
            return OskToggleResult.Close;
        }

        // 닫혀 있음 → 직접 실행 우선, 실패 시 단축키 폴백
        if (!TryLaunch())
            sendToggleHotkey();
        return OskToggleResult.Open;
    }

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

/// <summary>
/// OSK 토글 버튼이 수행한 동작의 종류입니다. 음성 안내 문구를 고르는 데 사용합니다.
/// </summary>
public enum OskToggleResult
{
    /// <summary>열기 시도 (직접 실행 또는 단축키 폴백)</summary>
    Open,
    /// <summary>닫기 요청 (Win+Ctrl+O 전송)</summary>
    Close
}
