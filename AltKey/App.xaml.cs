using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using AltKey.ViewModels;
using AltKey.Models;

namespace AltKey;

/// <summary>
/// [역할] AltKey 애플리케이션 그 자체를 정의하며, 프로그램이 처음 켜질 때와 꺼질 때의 모든 동작을 총괄합니다.
/// [기능] 필요한 서비스(사전, 입력기 등)를 준비(의존성 주입)하고, 첫 창을 띄우며, 프로그램 종료 시 데이터를 안전하게 저장합니다.
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!; // 앱 전체에서 사용할 서비스들을 담고 있는 저장소입니다.
    
    // L1: 큰 텍스트 모드
    private ConfigService? _configService;

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
            services.AddSingleton<AccessibilityNavigationService>();
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

            // 글꼴 배율 적용
            _configService = config;
            _configService.ConfigChanged += OnConfigChanged;
            UpdateScaledFontSize();

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

            // L1: 접근성 탭 탐색(물리 Tab/Enter/Space) 훅 시작
            Services.GetRequiredService<AccessibilityNavigationService>().Start();

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
        try
        {
            if (Services.GetService<InputService>() is { } inputService)
                ModifierSafety.PrepareForAppExit(inputService, "App.OnExit");
        }
        catch { /* 종료 방어 실패는 앱 종료를 막지 않는다. */ }

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

    // L1: 큰 텍스트 모드
    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName == nameof(AppConfig.KeyFontScalePercent))
        {
            UpdateScaledFontSize();
        }
    }

    private void UpdateScaledFontSize()
    {
        if (_configService == null) return;

        int scalePercent = _configService.Current.KeyFontScalePercent;
        double baseSize = 13.0; // KeyFontSize in Generic.xaml
        double scaled = baseSize * scalePercent / 100.0;

        // Apply the scaled font size to the application resource.
        // Direct assignment works with merged dictionaries as well.
        var currentApp = System.Windows.Application.Current;
        if (currentApp != null)
        {
            currentApp.Resources["ScaledKeyFontSize"] = scaled;
            currentApp.Resources["ScaledSubLabelFontSize"] = 8.0 * scalePercent / 100.0;
        }
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
