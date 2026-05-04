using System.Windows;
using AltKey.Services;
using AltKey.ViewModels;
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

        var mainWindow = new MainWindow();
        // 메인 앱에서 "--tool layout|dictionary"로 진입할 수 있게 시작 인자를 전달합니다.
        mainWindow.ApplyStartupArguments(e.Args);
        MainWindow = mainWindow;
        mainWindow.Show();
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
