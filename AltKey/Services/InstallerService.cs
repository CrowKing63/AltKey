using System.Diagnostics;
using System.IO;

namespace AltKey.Services;

/// <summary>T-9.5: 업데이트 설치 프로그램 실행 서비스</summary>
public class InstallerService
{
    /// <summary>
    /// 다운로드된 설치 프로그램을 자동 모드로 실행합니다.
    /// </summary>
    /// <param name="installerPath">설치 파일 경로</param>
    /// <param name="autoRestart">설치 후 앱 자동 재시작 여부</param>
    /// <param name="requestElevation">runas를 통한 관리자 권한 요청 여부</param>
    /// <returns>설치 프로그램 종료 코드</returns>
    public async Task<int> RunInstallerAsync(
        string installerPath,
        bool autoRestart = false,
        bool requestElevation = true)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException($"Installer not found: {installerPath}");

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = GetArguments(autoRestart),
            UseShellExecute = true
        };

        // 접근성: 자동 업데이트 시 앱이 관리자 권한으로 재실행되지 않도록
        // 필요할 때만 runas를 명시한다.
        if (requestElevation)
            psi.Verb = "runas";

        using var process = Process.Start(psi);
        if (process == null) return -1;

        await process.WaitForExitAsync();
        var exitCode = process.ExitCode;

        // 설치 후 임시 파일 정리 시도
        try { File.Delete(installerPath); }
        catch { /* 삭제 실패는 무시 */ }

        return exitCode;
    }

    /// <summary>
    /// 설치 프로그램을 실행만 하고 즉시 반환합니다. (즉시 업데이트 시작용)
    /// </summary>
    public void StartInstaller(
        string installerPath,
        bool autoRestart = true,
        bool requestElevation = true)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException($"Installer not found: {installerPath}");

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = GetArguments(autoRestart),
            UseShellExecute = true
        };

        // 접근성: 자동 업데이트 시 앱이 관리자 권한으로 재실행되지 않도록
        // 필요할 때만 runas를 명시한다.
        if (requestElevation)
            psi.Verb = "runas";

        Process.Start(psi);
    }

    private string GetArguments(bool autoRestart)
    {
        // Inno Setup 자동 설치 매개변수
        // /VERYSILENT - UI 없이 설치
        // /SUPPRESSMSGBOXES - 메시지 박스 비활성화
        // /CLOSEAPPLICATIONS - 실행 중인 앱 종료
        // /AUTORESTART - 설치 후 앱 자동 재시작
        return autoRestart
            ? "/VERYSILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /AUTORESTART /LOG"
            : "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";
    }
}
