using System.Linq;
using System.Windows;
using Microsoft.Win32;
using WpfApp = System.Windows.Application;

namespace AltKey.Services;

public class ThemeService
{
    private readonly ConfigService _configService;

    public ThemeService(ConfigService configService)
    {
        _configService = configService;

        // T-4.5: Windows 시스템 테마 변경 자동 감지
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

    public void Apply(string theme)
    {
        var resolved = theme == "system" ? DetectSystemTheme() : theme;
        var uri = new Uri($"pack://application:,,,/Themes/{resolved}Theme.xaml",
                          UriKind.Absolute);
        var dict = new ResourceDictionary { Source = uri };

        var merged = WpfApp.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(
            d => d.Source?.ToString().Contains("Theme") == true);
        if (existing is not null)
            merged.Remove(existing);
        merged.Add(dict);
    }

    private static string DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            return val is int i && i == 0 ? "Dark" : "Light";
        }
        catch
        {
            return "Dark";
        }
    }
}
