using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AltKey.Services;
using AltKey.Services.InputLanguage;
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
            services.AddSingleton<StartupService>();
            services.AddSingleton<SoundService>();
            services.AddSingleton<ClipboardService>();
            services.AddSingleton<UpdateService>();
            // T-9.5: 자동 업데이트 서비스
            services.AddSingleton<DownloadService>();
            services.AddSingleton<InstallerService>();
            // T-9.3: 자동 완성 서비스
            services.AddSingleton<Func<string, WordFrequencyStore>>(_ => lang => new WordFrequencyStore(lang));
            services.AddSingleton<Func<string, BigramFrequencyStore>>(_ => lang => new BigramFrequencyStore(lang));
            services.AddSingleton<KoreanDictionary>();
            services.AddSingleton<EnglishDictionary>();
            services.AddSingleton<KoreanInputModule>();
            services.AddSingleton<IInputLanguageModule>(sp => sp.GetRequiredService<KoreanInputModule>());
            services.AddSingleton<AutoCompleteService>();
            // 08: 접근성 LiveRegion 서비스
            services.AddSingleton<LiveRegionService>();

            // ViewModel
            services.AddSingleton<KeyboardViewModel>();
            services.AddSingleton<EmojiViewModel>();
            services.AddSingleton<ClipboardViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();
            // T-9.3: 자동 완성 바 ViewModel
            services.AddSingleton<SuggestionBarViewModel>();
            // T-9.4: 레이아웃 편집기 ViewModel
            services.AddSingleton<LayoutEditorViewModel>();
            // ac-editor 03: 사용자 사전 편집기 ViewModel
            services.AddSingleton<UserDictionaryEditorViewModel>();

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

            // T-2.10b: ProfileService → InputService 관리자 권한 알림 연결
            var inputService = Services.GetRequiredService<InputService>();
            profileService.ElevatedAppDetected += () => inputService.NotifyElevatedApp();

            // 06: 관리자 모드에서 자동완성 강제 OFF
            if (inputService.IsElevated && config.Current.AutoCompleteEnabled)
            {
                config.Current.AutoCompleteEnabled = false;
                config.Save();
            }

            // 일반 모드에서 config에 따라 InputService.Mode 초기화
            if (!inputService.IsElevated)
            {
                var targetMode = config.Current.AutoCompleteEnabled ? InputMode.Unicode : InputMode.VirtualKey;
                inputService.TrySetMode(targetMode);
            }

            var window = Services.GetRequiredService<MainWindow>();
            window.Show();

            // T-9.5: 백그라운드 업데이트 확인 (창 표시 후 비동기 실행)
            _ = Task.Run(async () =>
            {
                var updateSvc = Services.GetRequiredService<UpdateService>();
                var (hasUpdate, version, url, installerUrl) = await updateSvc.CheckAsync();
                if (hasUpdate)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var vm = Services.GetRequiredService<ViewModels.MainViewModel>();
                        vm.UpdateVersion = version;
                        vm.UpdateUrl = url;
                        vm.UpdateInstallerUrl = installerUrl;
                    });
                }
            });

            // T-6.6: 첫 창 표시까지 걸린 시간 (Debug 로그)
            var elapsed = Environment.TickCount64 - _startTick;
            Debug.WriteLine($"[AltKey] Startup time: {elapsed}ms");

            // T-6.6: 메모리 사용량 로그
            var memMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            Debug.WriteLine($"[AltKey] Initial managed memory: {memMb:F1} MB");
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
        // TASK-05: 자동완성 학습 데이터 즉시 저장 (디버운스 타이머 Flush)
        try
        {
            Services.GetService<KoreanDictionary>()?.Flush();
            Services.GetService<EnglishDictionary>()?.Flush();
        }
        catch { /* Flush 실패는 무시 (이미 WordFrequencyStore.Save 내부에서 로깅) */ }

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
