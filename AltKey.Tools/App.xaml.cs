using System.Windows;
using AltKey.Services;
using AltKey.ViewModels;
using AltKey.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Tools;

/// <summary>
/// [역할] AltKey 편집 도구 앱의 DI 컨테이너를 구성하고 시작 창을 띄웁니다.
/// [접근성] 편집기는 메인 입력 앱과 다른 프로세스에서 실행되어 한글 조합 입력 안정성을 높입니다.
/// </summary>
public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        // 접근성: 특정 편집기 바로가기 진입(--tool)에서는 허브 창을 띄우지 않고
        // 요청된 편집기 창만 바로 표시해 불필요한 포커스 이동과 화면 깜빡임을 줄입니다.
        var directToolWindow = CreateDirectToolWindow(e.Args);
        if (directToolWindow is not null)
        {
            MainWindow = directToolWindow;
            directToolWindow.Show();
            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
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
    /// 시작 인자에서 직접 열 편집기를 판별해 해당 창 인스턴스를 반환합니다.
    /// 지원 값: layout, dictionary, profile, ai-prompt
    /// </summary>
    private static Window? CreateDirectToolWindow(string[] args)
    {
        var toolName = GetToolArgument(args);
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return null;
        }

        if (string.Equals(toolName, "layout", StringComparison.OrdinalIgnoreCase))
        {
            var vm = Services.GetRequiredService<LayoutEditorViewModel>();
            return new LayoutEditorWindow(vm);
        }

        if (string.Equals(toolName, "dictionary", StringComparison.OrdinalIgnoreCase))
        {
            var vm = Services.GetRequiredService<UserDictionaryEditorViewModel>();
            return new UserDictionaryEditorWindow(vm);
        }

        if (string.Equals(toolName, "profile", StringComparison.OrdinalIgnoreCase))
        {
            return new ProfileMappingEditorWindow();
        }

        if (string.Equals(toolName, "ai-prompt", StringComparison.OrdinalIgnoreCase))
        {
            return new AiPromptEditorWindow();
        }

        return null;
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

    protected override void OnExit(ExitEventArgs e)
    {
        // 편집기가 남긴 디바운스 저장을 종료 시점에 강제 반영합니다.
        Services.GetService<IUserDictionaryRepository>()?.Flush();

        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}
