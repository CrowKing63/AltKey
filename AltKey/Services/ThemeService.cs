using System.Linq;
using System.Windows;
using Microsoft.Win32;
using WpfApp = System.Windows.Application;

namespace AltKey.Services;

/// <summary>
/// [역할] 앱의 시각적 스타일(테마)을 관리하는 서비스입니다.
/// [기능] 라이트 모드, 다크 모드, 고대비 모드를 적용하고 윈도우 시스템 설정에 따라 테마를 자동 변경합니다.
/// </summary>
public class ThemeService
{
    private readonly ConfigService _configService;
    private ResourceDictionary? _currentThemeDict;

    public ThemeService(ConfigService configService)
    {
        _configService = configService;

        // 윈도우의 시스템 테마(고대비 등)가 변경되면 앱에도 즉시 반영하도록 감시합니다.
        SystemParameters.StaticPropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast)
                || e.PropertyName == "WindowGlassColor")
            {
                if (_configService.Current.Theme == "system")
                    Apply("system");
            }
        };
    }

    /// <summary>
    /// 지정된 테마(Light, Dark, HighContrast, system)를 앱 전체에 적용합니다.
    /// </summary>
    public void Apply(string theme)
    {
        // 'system' 설정인 경우 현재 윈도우의 테마 상태를 직접 확인합니다.
        var resolved = theme == "system" ? DetectSystemTheme() : theme;
        var uri = new Uri($"pack://application:,,,/AltKey;component/Themes/{resolved}Theme.xaml",
                          UriKind.Absolute);
        var dict = new ResourceDictionary { Source = uri };

        var merged = WpfApp.Current.Resources.MergedDictionaries;
        // 기존 테마 리소스를 제거하고 새 테마 리소스를 추가합니다.
        if (_currentThemeDict is not null)
            merged.Remove(_currentThemeDict);
        _currentThemeDict = dict;
        merged.Add(dict);
    }

    /// <summary>
    /// 현재 윈도우 운영체제가 어떤 테마 상태인지 레지스트리 등을 통해 확인합니다.
    /// </summary>
    private static string DetectSystemTheme()
    {
        // 1. 고대비 모드 확인
        if (SystemParameters.HighContrast)
            return "HighContrast";
        
        try
        {
            // 2. 윈도우 앱 모드(다크/라이트) 확인
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0 ? "Dark" : "Light";
        }
        catch
        {
            return "Dark"; // 오류 시 기본값은 다크 모드입니다.
        }
    }
}
