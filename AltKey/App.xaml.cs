using System.Diagnostics;
using System.IO;
using System.Threading;
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

    // 단일 인스턴스 제한용 Mutex (설치형·무설치형 모두 공통)
    private static Mutex? _instanceMutex;

    // 이미 실행 중인 AltKey 창을 다시 보여 달라는 신호를 주고받는 이벤트입니다.
    private static EventWaitHandle? _restoreWindowEvent;

    // 앱이 살아 있는 동안 복원 이벤트 감시를 유지하는 등록 핸들입니다.
    private static RegisteredWaitHandle? _restoreWindowWaitHandle;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 단일 인스턴스 제한: 이미 실행 중이면 즉시 종료
        const string mutexName = "AltKey_SingleInstance_Mutex";
        _instanceMutex = new Mutex(initiallyOwned: true, name: mutexName, out bool createdNew);
        if (!createdNew)
        {
            SignalExistingInstanceToRestore();
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        // 작업표시줄 고정 아이콘으로 다시 실행했을 때 기존 AltKey를 복원시키는 신호 채널입니다.
        _restoreWindowEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: "AltKey_RestoreWindow_Event");
        // T-6.7: 전역 미처리 예외 핸들러 (동일 타입 에러는 한 번만 팝업)
        // [참고] 클립보드 관련 COMException(0x800401D0~0x800401D3)은 0.5초마다 평시 체크되므로 로그/메시지를 남기지 않음
        static bool IsClipboardComError(System.Runtime.InteropServices.COMException ex)
            => ex.ErrorCode >= unchecked((int)0x800401D0) && ex.ErrorCode <= unchecked((int)0x800401D3);

        var _shownErrors = new System.Collections.Generic.HashSet<string>();

        DispatcherUnhandledException += (s, args) =>
        {
            args.Handled = true;

            // 클립보드 관련 에러는 조용히 무시 (다음 평링 주기에 재시도됨)
            if (args.Exception is System.Runtime.InteropServices.COMException comEx
                && IsClipboardComError(comEx))
            {
                return;
            }

            LogError(args.Exception);
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
            {
                // 클립보드 관련 에러는 무시
                if (ex is System.Runtime.InteropServices.COMException comEx
                    && IsClipboardComError(comEx))
                {
                    return;
                }
                LogError(ex);
            }
        };

        try
        {
            var services = new ServiceCollection();

            // 서비스
            services.AddSingleton<ConfigService>();
            services.AddSingleton<LayoutService>();
            services.AddSingleton<ILayoutRepository, LayoutRepository>();
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
            services.AddSingleton<OskLauncherService>();
            // L2: 접근성 TTS 서비스
            services.AddSingleton<AccessibilityService>();
            // T-9.3: 자동 완성 서비스
            services.AddSingleton<Func<string, WordFrequencyStore>>(_ => lang => new WordFrequencyStore(lang));
            services.AddSingleton<Func<string, BigramFrequencyStore>>(_ => lang => new BigramFrequencyStore(lang));
            services.AddSingleton<KoreanDictionary>();
            services.AddSingleton<EnglishDictionary>();
            services.AddSingleton<IUserDictionaryRepository, UserDictionaryRepository>();
            services.AddSingleton<KoreanInputModule>();
            services.AddSingleton<IInputLanguageModule>(sp => sp.GetRequiredService<KoreanInputModule>());
            services.AddSingleton<AutoCompleteService>();
            // 08: 접근성 LiveRegion 서비스
            services.AddSingleton<LiveRegionService>();
            // AI 텍스트 처리 서비스
            services.AddSingleton<AiService>();
            services.AddSingleton<ToolsReloadSignalService>();

            // ViewModel
            services.AddSingleton<KeyboardViewModel>();
            services.AddSingleton<EmojiViewModel>();
            services.AddSingleton<ClipboardViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();
            // T-9.3: 자동 완성 바 ViewModel
            services.AddSingleton<SuggestionBarViewModel>();
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

            // 상주 Tools 프로세스: 백그라운드로 띄워 설정에서 편집기 창이 즉시 열리게 합니다.
            _ = Task.Run(() => TryLaunchResidentTools());

            RegisterRestoreWindowHandler();

            // AltKey.Tools에서 보내는 최소 재로드 알림 수신을 시작합니다.
            _ = Services.GetRequiredService<ToolsReloadSignalService>();

            // L1: 접근성 탭 탐색(물리 Tab/Enter/Space) 훅 시작
            Services.GetRequiredService<AccessibilityNavigationService>().Start();

            // T-9.5: 백그라운드 업데이트 확인 (창 표시 후 비동기 실행)
            _ = Task.Run(async () =>
            {
                // [업데이트] 스토어(MSIX) 설치판은 Microsoft Store가 자동으로 업데이트하므로 GitHub 확인을 생략한다.
                if (PathResolver.IsMsix)
                    return;

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
        // 상주 Tools 프로세스에 종료를 요청합니다. 실패해도 워치독이 메인 앱 종료로 자동 정리합니다.
        ToolsIpc.TrySend("exit", timeoutMs: 300);

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

        _restoreWindowWaitHandle?.Unregister(null);
        _restoreWindowWaitHandle = null;
        _restoreWindowEvent?.Dispose();
        _restoreWindowEvent = null;

        // 단일 인스턴스 Mutex 해제
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        _instanceMutex = null;

        base.OnExit(e);
    }

    /// <summary>
    /// AltKey.Tools 상주 프로세스를 백그라운드로 띄웁니다.
    /// 실패(파일 누락 등)하면 조용히 무시합니다 — 설정 버튼이 직접 실행으로 폴백합니다.
    /// </summary>
    private static void TryLaunchResidentTools()
    {
        try
        {
            var exePath = PathResolver.ToolsExePath;
            if (!File.Exists(exePath))
            {
                return;
            }

            // --main-pid: Tools 워치독이 메인 앱 종료 시 함께 종료되는 데 사용합니다.
            var arguments = $"--resident --main-pid {Environment.ProcessId}";
            // 포터블 모드에선 데이터 폴더가 exe 디렉터리이므로 명시 전달해 config/layouts를 공유합니다.
            if (PathResolver.IsPortable)
            {
                arguments += $" --data-dir \"{PathResolver.DataDir}\"";
            }

            Process.Start(new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = arguments,
            });
        }
        catch
        {
            // Tools 띄우기 실패는 메인 앱을 막지 않습니다 — 설정 버튼이 직접 실행으로 폴백합니다.
        }
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

            // [접근성] 설정 창/편집기 텍스트도 같은 비율로 배율되므로 한 슬라이더로 전체가 조절됩니다.
            // Generic.xaml의 기본값(100% 기준)을 여기서 실제 배율 값으로 덮어씁니다.
            currentApp.Resources["ScaledSettingsSubFontSize"] = 11.0 * scalePercent / 100.0;
            currentApp.Resources["ScaledSettingsBaseFontSize"] = 12.0 * scalePercent / 100.0;
            currentApp.Resources["ScaledSettingsBodyLargeFontSize"] = 14.0 * scalePercent / 100.0;
            currentApp.Resources["ScaledSettingsHeadingFontSize"] = 16.0 * scalePercent / 100.0;
            currentApp.Resources["ScaledToolTitleFontSize"] = 22.0 * scalePercent / 100.0;
        }

        // [접근성] 상주하는 AltKey.Tools 프로세스에도 같은 배율을 알립니다.
        // 배경 스레드로 보내 UI가 연결 대기(최대 수백 ms)에 막히지 않도록 합니다.
        // 듣는 프로세스가 없으면 조용히 실패해 폴백(창 재시작 시 설정값 적용)으로 충분합니다.
        var percent = scalePercent;
        _ = Task.Run(() => ToolsIpc.TrySend($"font-scale {percent}"));
    }

    // T-6.7: 파일 로깅
    // [참고] 경로는 PathResolver.DataDir로 지정되어 설치형(%AppData%\AltKey) 및 포터블 모드 모두 쓰기 가능합니다.
    internal static void LogError(Exception ex)
    {
        try
        {
            var logPath = Path.Combine(
                PathResolver.DataDir, "altkey-error.log");
            File.AppendAllText(logPath,
                $"[{DateTime.Now:u}] {ex}\n\n");
        }
        catch { /* 로그 쓰기 실패 — 무시 */ }
    }

    /// <summary>
    /// 이미 실행 중인 AltKey가 있다면 창을 다시 보여 달라는 신호만 보내고 새 인스턴스는 종료합니다.
    /// </summary>
    private static void SignalExistingInstanceToRestore()
    {
        try
        {
            using var restoreEvent = EventWaitHandle.OpenExisting("AltKey_RestoreWindow_Event");
            restoreEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // 기존 인스턴스가 아직 복원 이벤트를 만들기 전이면 조용히 종료합니다.
        }
    }

    /// <summary>
    /// 다른 실행 요청이 들어오면 기존 AltKey 창을 트레이에서 다시 보이도록 연결합니다.
    /// </summary>
    private void RegisterRestoreWindowHandler()
    {
        if (_restoreWindowEvent is null)
            return;

        _restoreWindowWaitHandle = ThreadPool.RegisterWaitForSingleObject(
            _restoreWindowEvent,
            static (_, _) =>
            {
                Current.Dispatcher.Invoke(() =>
                {
                    if (Services.GetService<TrayService>() is { } trayService)
                    {
                        trayService.ShowWindowFromExternalActivation();
                    }
                });
            },
            state: null,
            millisecondsTimeOutInterval: Timeout.Infinite,
            executeOnlyOnce: false);
    }
}
