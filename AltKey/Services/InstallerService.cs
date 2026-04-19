using System.Diagnostics;
using System.IO;

namespace AltKey.Services;

/// <summary>T-9.5: 설치 프로그램 실행 서비스</summary>
public class InstallerService
{
    /// <summary>
    /// 다운로드한 인스톨러를 자동 모드로 실행합니다.
    /// </summary>
    /// <param name="installerPath">인스톨러 파일 경로</param>
    /// <param name="autoRestart">설치 후 앱 자동 재시작 여부</param>
    /// <returns>설치 프로그램 종료 코드</returns>
    public async Task<int> RunInstallerAsync(string installerPath, bool autoRestart = false)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException($"Installer not found: {installerPath}");

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = GetArguments(autoRestart),
            UseShellExecute = true, // Verb="runas" 사용 시 필수
            Verb = "runas"          // 관리자 권한 요청
        };

        using var process = Process.Start(psi);
        if (process == null) return -1;

        await process.WaitForExitAsync();
        var exitCode = process.ExitCode;

        // 설치 후 임시 파일 정리 시도
        try { File.Delete(installerPath); }
        catch { /* 삭제 실패 시 무시 */ }

        return exitCode;
    }

    /// <summary>
    /// 인스톨러를 실행만 하고 즉시 반환합니다. (자가 업데이트 시 사용)
    /// </summary>
    public void StartInstaller(string installerPath, bool autoRestart = true)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException($"Installer not found: {installerPath}");

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = GetArguments(autoRestart),
            UseShellExecute = true,
            Verb = "runas"
        };

        Process.Start(psi);
    }

    private string GetArguments(bool autoRestart)
    {
        // Inno Setup 자동 설치 매개변수:
        // /SILENT - UI 표시하지만 사용자 입력 불필요
        // /VERYSILENT - 완전 자동 (UI 없음)
        // /SUPPRESSMSGBOXES - 메시지 상자 비활성화
        // /CLOSEAPPLICATIONS - 실행 중인 인스턴 종료
        // /RESTARTAPPLICATIONS - 설치 후 앱 재시작 (autoRestart 시)
        return autoRestart
            ? "/VERYSILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /AUTORESTART /LOG"
            : "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";
    }
}
