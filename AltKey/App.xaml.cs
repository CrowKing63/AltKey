using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // T-6.6: 앱 시작 시간 측정
    private static readonly long _startTick = Environment.TickCount64;

    protected override void OnStartup(StartupEventArgs e)
    {
        // T-6.7: 전역 미처리 예외 핸들러 (동일 타입 에러는 한 번만 팝업)
        var _shownErrors = new System.Collections.Generic.HashSet<string>();
        DispatcherUnhandledException += (s, args) =>
        {
            LogError(args.Exception);
            args.Handled = true;
            var key = args.Exception.GetType().FullName + args.Exception.Message;
            if (_shownErrors.Add(key))
            {
                System.Windows.MessageBox.Show(
                    $"예기치 않은 오류가 발생했습니다:\n{args.Exception.Message}\n\n로그: altkey-error.log",
                    "AltKey 오류",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogError(ex);
        };

        try
        {
            var services = new ServiceCollection();

            // 서비스
            services.AddSingleton<ConfigService>();
            services.AddSingleton<LayoutService>();
            services.AddSingleton<InputService>();
            services.AddSingleton<WindowService>();
            services.AddSingleton<ProfileService>();
            services.AddSingleton<TrayService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<HotkeyService>();

            // ViewModel
            services.AddSingleton<KeyboardViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();

            // 창
            services.AddSingleton<MainWindow>();

            Services = services.BuildServiceProvider();

            // 시스템 테마 적용
            var themeService = Services.GetRequiredService<ThemeService>();
            var config       = Services.GetRequiredService<ConfigService>();
            themeService.Apply(config.Current.Theme);

            // T-5.3: 포그라운드 앱 감지 시작
            var profileService = Services.GetRequiredService<ProfileService>();
            profileService.Start();

            var window = Services.GetRequiredService<MainWindow>();
            window.Show();

            // T-6.6: 첫 창 표시까지 걸린 시간 (Debug 로그)
            var elapsed = Environment.TickCount64 - _startTick;
            Debug.WriteLine($"[AltKey] Startup time: {elapsed}ms");
        }
        catch (Exception ex)
        {
            LogError(ex);
            System.Windows.MessageBox.Show(
                ex.ToString(), "AltKey Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 서비스 정리
        if (Services is IDisposable d) d.Dispose();
        base.OnExit(e);
    }

    // T-6.7: 파일 로깅
    internal static void LogError(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "altkey-error.log");
            File.AppendAllText(logPath,
                $"[{DateTime.Now:u}] {ex}\n\n");
        }
        catch { /* 로그 쓰기 실패 — 무시 */ }
    }
}
