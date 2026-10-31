using System.Diagnostics;
using System.IO.Pipes;
using System.Windows;
using AltKey.Services;
using AltKey.ViewModels;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Tools;

/// <summary>
/// [역할] AltKey 편집 도구 앱의 DI 컨테이너를 구성하고 시작 창을 띄웁니다.
/// [접근성] 편집기는 메인 입력 앱과 다른 프로세스에서 실행되어 한글 조합 입력 안정성을 높입니다.
/// [상주 모드] 메인 앱이 --resident 인자로 띄우면 창 없이 숨어서 IPC 명령을 기다립니다.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    // 상주 모드에서 메인 앱 종료 감시(워치독)에 사용하는 타이머입니다.
    private DispatcherTimer? _watchdogTimer;

    // 단일 인스턴스 뮤텍스는 프로세스 수명 동안 소유해야 합니다.
    // OnStartup 스코프에서 using var로 만들면 시작이 끝나자마자 소유가 풀려
    // 두 번째 Tools 프로세스가 동시에 실행될 수 있습니다(파이프 이름 충돌).
    private Mutex? _instanceMutex;

    public App()
    {
        // 접근성/유지보수: 도구 앱 시작 오류가 조용히 사라지지 않도록 즉시 대화상자로 노출합니다.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            var rawArgs = e.Args ?? Array.Empty<string>();
            var isResident = rawArgs.Contains("--resident", StringComparer.OrdinalIgnoreCase);

            // 단일 인스턴스: 이미 실행 중인 프로세스가 있으면 요청을 전달하고 즉시 종료합니다.
            // 뮤텍스는 프로세스 수명 동안 소유해야 하므로 필드로 유지하고 OnExit에서 해제합니다.
            _instanceMutex = new Mutex(
                initiallyOwned: true,
                name: "AltKey.Tools_SingleInstance_Mutex",
                out var createdNew);
            if (!createdNew)
            {
                // 전달에 성공하면 이번 인스턴스는 역할을 마친 것이므로 종료합니다.
                // 실패(기존 프로세스가 손상된 경우)면 아래 정상 시작 흐름으로 이어갑니다.
                if (ForwardToExistingInstance(rawArgs, isResident))
                {
                    Shutdown();
                    return;
                }
            }

            // 메인 앱이 데이터 폴더를 함께 전달한 경우, 도구 앱도 같은 config/layouts를 사용하도록 먼저 고정합니다.
            PathResolver.OverrideDataDir(GetDataDirArgument(rawArgs));

            var services = new ServiceCollection();

            // [레이아웃 편집기] 파일 기반 레이아웃 읽기/저장을 위해 필요한 최소 서비스만 등록합니다.
            services.AddSingleton<ConfigService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<LayoutService>();
            services.AddSingleton<ILayoutRepository, LayoutRepository>();
            services.AddSingleton<LayoutEditorViewModel>();

            // [사용자 단어 편집기] 한국어/영어 사용자 사전 + bigram 편집을 위한 최소 서비스 등록입니다.
            services.AddSingleton<Func<string, WordFrequencyStore>>(_ => lang => new WordFrequencyStore(lang));
            services.AddSingleton<Func<string, BigramFrequencyStore>>(_ => lang => new BigramFrequencyStore(lang));
            services.AddSingleton<KoreanDictionary>();
            services.AddSingleton<EnglishDictionary>();
            services.AddSingleton<IUserDictionaryRepository, UserDictionaryRepository>();
            services.AddSingleton<UserDictionaryEditorViewModel>();

            Services = services.BuildServiceProvider();
            ApplyInitialTheme();
            // [접근성] 메인 앱의 "글자 크기(%)" 설정으로 편집기 텍스트도 같은 비율로 시작합니다.
            ApplyScaledFontSizes();

            // 상주 모드: 창을 띄우지 않고 메인 앱의 IPC 명령만 기다립니다.
            // 프로세스 수명을 메인 앱에 묶어 모든 창이 닫혀도 종료되지 않습니다.
            if (isResident)
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                StartIpcServer();
                StartMainAppWatchdog(GetArgumentValue(rawArgs, "--main-pid"));
                return;
            }

            // 접근성: 특정 편집기 바로가기 진입(--tool)에서는 허브 창을 띄우지 않고
            // 요청된 편집기 창만 바로 표시해 불필요한 포커스 이동과 화면 깜빡임을 줄입니다.
            var toolName = GetToolArgument(rawArgs);
            if (!string.IsNullOrWhiteSpace(toolName))
            {
                var window = ToolWindowRegistry.GetOrCreate(
                    toolName,
                    BuildHeaderShortcutExtraArgs(rawArgs, toolName));
                MainWindow = window;
                window.Show();
                window.Activate();
                return;
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            ShowFatalStartupError(ex);
            Shutdown(-1);
        }
    }

    /// <summary>
    /// 이미 실행 중인 Tools 프로세스에 이번 시작 요청을 전달합니다.
    /// --resident(메인 앱이 띄운 경우)는 사용자 요청이 없으므로 조용히 종료만 합니다.
    /// 전달 성공 여부(bool)를 반환해, 기존 프로세스가 손상된 경우 호출부에서 정상 시작으로 폴백합니다.
    /// </summary>
    private static bool ForwardToExistingInstance(string[] args, bool isResident)
    {
        if (isResident)
        {
            // 전달할 사용자 요청이 없으므로 성공으로 간주해 종료만 합니다.
            return true;
        }

        var toolName = GetToolArgument(args);
        string message;
        if (!string.IsNullOrWhiteSpace(toolName))
        {
            var extraArgs = BuildHeaderShortcutExtraArgs(args, toolName);
            message = extraArgs is null ? $"open {toolName}" : $"open {toolName} {extraArgs}";
        }
        else
        {
            // 허브를 더블클릭한 경우: 기존 프로세스에 허브 창을 보여 달라고 요청합니다.
            message = "open hub";
        }

        // 기존 프로세스가 아직 시작 중(IPC 경청 전)일 수 있으므로 몇 번 재시도해 레이스를 커버합니다.
        // 모두 실패하면 그 프로세스는 손상된 것으로 간주하고 호출부가 정상 시작으로 폴백합니다.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (ToolsIpc.TrySend(message, timeoutMs: 500))
            {
                return true;
            }

            Thread.Sleep(500);
        }

        return false;
    }

    /// <summary>
    /// 상단바 단축키 편집기만 필요한 추가 인자(버튼 ID, 생성 모드)를 문자열로 조립합니다.
    /// </summary>
    private static string? BuildHeaderShortcutExtraArgs(string[] args, string toolName)
    {
        if (!string.Equals(toolName, "header-shortcut", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var buttonId = GetArgumentValue(args, "--header-button-id");
        var mode = GetArgumentValue(args, "--header-button-mode");
        if (buttonId is null && mode is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (buttonId is not null) parts.Add($"--header-button-id {buttonId}");
        if (mode is not null) parts.Add($"--header-button-mode {mode}");
        return string.Join(" ", parts);
    }

    /// <summary>
    /// 명명 파이프 서버를 백그라운드 스레드에서 돌립니다.
    /// 명령 한 줄을 받아 UI 스레드(디스패처)에서 처리합니다.
    /// </summary>
    private void StartIpcServer()
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = ToolsIpc.CreateServer();
                    // .NET 8의 NamedPipeServerStream은 타임아웃 오버로드가 없어 클라이언트가 연결될 때까지 블록합니다.
                    // 이 스레드는 백그라운드 스레드라 앱 종료(Shutdown) 중에 블록되어 있어도 프로세스는 정상 종료됩니다.
                    server.WaitForConnection();

                    using var reader = new System.IO.StreamReader(server);
                    var line = reader.ReadLine();
                    if (line is null)
                    {
                        continue;
                    }

                    Dispatcher.Invoke(() => HandleIpcMessage(line));
                }
                catch
                {
                    // 종료 중이거나 일시적인 파이프 오류 — 다시 시도합니다.
                }
                finally
                {
                    server?.Dispose();
                }

                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                {
                    break;
                }
            }
        })
        { IsBackground = true };
        thread.Start();
    }

    /// <summary>
    /// IPC 명령을 처리합니다.
    /// 형식: "open {도구명} [추가 인자]" / "open hub" / "exit"
    /// </summary>
    private void HandleIpcMessage(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        var separatorIndex = trimmed.IndexOf(' ');
        var command = separatorIndex < 0 ? trimmed : trimmed.Substring(0, separatorIndex);
        var payload = separatorIndex < 0 ? null : trimmed.Substring(separatorIndex + 1).Trim();

        switch (command.ToLowerInvariant())
        {
            case "exit":
                Shutdown();
                break;

            // [접근성] 메인 앱이 글자 크기 슬라이더 변경을 실시간 전달해,
            // 이미 열린 편집기 창 텍스트도 슬라이더와 함께 바뀝니다.
            case "font-scale":
                if (int.TryParse(payload, out var fontScalePercent))
                {
                    ApplyScaledFontSizes(fontScalePercent);
                }

                break;

            case "open" when string.Equals(payload, "hub", StringComparison.OrdinalIgnoreCase):
                ShowHubWindow();
                break;

            case "open":
                if (string.IsNullOrWhiteSpace(payload))
                {
                    return;
                }

                var toolParts = payload.Split(' ', 2);
                try
                {
                    var window = ToolWindowRegistry.GetOrCreate(
                        toolParts[0],
                        toolParts.Length > 1 ? toolParts[1] : null);
                    window.Show();
                    window.Activate();
                }
                catch (ArgumentException)
                {
                    // 알 수 없는 도구 이름 — 무시합니다. 잘못된 명령 한 건이 상주 프로세스 전체를 종료하게 두지 않습니다.
                }

                break;
        }
    }

    /// <summary>
    /// 허브 창을 표시합니다. 이미 열려 있으면 기존 창을 활성화합니다.
    /// </summary>
    private void ShowHubWindow()
    {
        if (MainWindow is MainWindow { IsLoaded: true })
        {
            MainWindow.Activate();
            return;
        }

        var hub = new MainWindow();
        MainWindow = hub;
        hub.Show();
    }

    /// <summary>
    /// 메인 앱 프로세스가 사라지면(종료/크래시) Tools도 함께 종료되도록 주기적으로 확인합니다.
    /// --main-pid 인자가 없으면 워치독을 켜지 않습니다 (자체 실행 모드).
    /// </summary>
    private void StartMainAppWatchdog(string? mainPidText)
    {
        if (!int.TryParse(mainPidText, out var pid))
        {
            return;
        }

        _watchdogTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(10),
        };
        _watchdogTimer.Tick += (_, _) => CheckMainAppAlive(pid);
        _watchdogTimer.Start();
    }

    private void CheckMainAppAlive(int pid)
    {
        var alive = false;
        try
        {
            alive = !Process.GetProcessById(pid).HasExited;
        }
        catch (ArgumentException)
        {
            // 프로세스 ID가 더 이상 존재하지 않음 — 메인 앱이 종료된 것입니다.
            alive = false;
        }

        if (!alive)
        {
            Shutdown();
        }
    }

    /// <summary>
    /// 도구 앱 시작 시 메인 앱과 같은 사용자 테마를 즉시 적용해 창 간 시각적 괴리와 명도 대비 차이를 줄입니다.
    /// </summary>
    private static void ApplyInitialTheme()
    {
        var configService = Services.GetRequiredService<ConfigService>();
        var themeService = Services.GetRequiredService<ThemeService>();
        themeService.Apply(configService.Current.Theme);
    }

    /// <summary>
    /// [접근성] 메인 앱의 "글자 크기(%)" 설정값으로 공용 배율 리소스(Generic.xaml)를 채웁니다.
    /// 시작 시에는 저장된 값, 상주 모드에서는 IPC font-scale 명령으로 갱신됩니다.
    /// </summary>
    private static void ApplyScaledFontSizes(int? scalePercent = null)
    {
        var configService = Services.GetRequiredService<ConfigService>();
        var percent = scalePercent ?? configService.Current.KeyFontScalePercent;
        // 메인 앱과 동일한 클램프 범위(80~220)를 유지해 두 앱 간 배율이 어긋나지 않습니다.
        percent = Math.Clamp(percent, 80, 220);

        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        app.Resources["ScaledSettingsSubFontSize"] = 11.0 * percent / 100.0;
        app.Resources["ScaledSettingsBaseFontSize"] = 12.0 * percent / 100.0;
        app.Resources["ScaledSettingsBodyLargeFontSize"] = 14.0 * percent / 100.0;
        app.Resources["ScaledSettingsHeadingFontSize"] = 16.0 * percent / 100.0;
        app.Resources["ScaledToolTitleFontSize"] = 22.0 * percent / 100.0;
    }

    /// <summary>
    /// "--tool {name}" 형태의 시작 인자에서 도구 이름을 추출합니다.
    /// </summary>
    private static string? GetToolArgument(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--tool", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// "--data-dir {absolute-path}" 형태의 시작 인자에서 공용 데이터 폴더 경로를 추출합니다.
    /// 메인 앱과 도구 앱이 같은 레이아웃/설정 파일을 공유하도록 개발 모드 격리를 해소합니다.
    /// </summary>
    private static string? GetDataDirArgument(string[] args)
    {
        return GetArgumentValue(args, "--data-dir");
    }

    private static string? GetArgumentValue(string[] args, string optionName)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 편집기가 남긴 디바운스 저장을 종료 시점에 강제 반영합니다.
        Services.GetService<IUserDictionaryRepository>()?.Flush();

        _watchdogTimer?.Stop();
        _watchdogTimer = null;

        // 단일 인스턴스 뮤텍스 해제 — 다음 Tools 프로세스가 새로 시작할 수 있게 합니다.
        _instanceMutex?.Dispose();
        _instanceMutex = null;

        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowFatalStartupError(e.Exception);
        e.Handled = true;
        Shutdown(-1);
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFatalStartupError(ex);
        }
    }

    private static void ShowFatalStartupError(Exception ex)
    {
        MessageBox.Show(
            "AltKey.Tools를 여는 중 오류가 발생했습니다.\n\n" + ex,
            "AltKey.Tools 실행 오류",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
