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
    /// <returns>설치 프로그램 종료 코드</returns>
    public Task<int> RunInstallerAsync(string installerPath)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException($"Installer not found: {installerPath}");

        var tcs = new TaskCompletionSource<int>();

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            // Inno Setup 자동 설치 매개변수:
            // /SILENT - UI 표시하지만 사용자 입력 불필요
            // /VERYSILENT - 완전 자동 (UI 없음)
            // /SUPPRESSMSGBOXES - 메시지 상자 비활성화
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
            UseShellExecute = false,
            Verb = "runas" // 관리자 권한 요청
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.Exited += (sender, args) =>
        {
            var exitCode = process.ExitCode;
            process.Dispose();

            // 설치 후 임시 파일 정리 시도
            try { File.Delete(installerPath); }
            catch { /* 삭제 실패 시 무시 */ }

            tcs.SetResult(exitCode);
        };

        process.Start();
        return tcs.Task;
    }

    /// <summary>
    /// 앱을 종료하고 인스톨러를 실행합니다.
    /// 설치 후 앱이 자동으로 재시작되도록 합니다.
    /// </summary>
    public async Task InstallAndRestartAsync(string installerPath)
    {
        // 현재 앱 종료
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            System.Windows.Application.Current.Shutdown();
        });

        // 인스톨러 실행 (앱 종료 후)
        await Task.Delay(1000); // 앱이 완전히 종료될 때까지 대기
        await RunInstallerAsync(installerPath);
    }
}
