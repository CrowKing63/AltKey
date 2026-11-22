using System.Linq;
using System.Windows;
using Microsoft.Win32;
using WpfApp = System.Windows.Application;

namespace AltKey.Services;

/// <summary>
/// [역할] 앱의 시각적 스타일(테마)을 관리하는 서비스입니다.
/// [기능] 라이트/다크/고대비 테마에 재질(플라스틱/메탈/우드/패브릭) 오버라이드를 이어서 적용하고,
///        윈도우 시스템 설정(고대비 등)이 바뀌면 즉시 반영합니다.
/// </summary>
public class ThemeService
{
    private readonly ConfigService _configService;
    // MergedDictionaries에 추가한 테마·재질 사전을 추적해 다음 Apply 시 둘 다 제거합니다.
    private ResourceDictionary? _currentThemeDict;
    private ResourceDictionary? _currentMaterialDict;

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
    /// 재질은 매번 설정에서 읽어 오므로 호출 사이트는 테마 이름만 전달하면 됩니다.
    /// </summary>
    public void Apply(string theme)
    {
        // 'system' 설정인 경우 현재 윈도우의 테마 상태를 직접 확인합니다.
        var resolved = theme == "system" ? DetectSystemTheme() : theme;
        // 재질은 항상 최신 설정값을 사용합니다 (설정 UI에서는 저장→Apply 순서를 보장).
        var material = _configService.Current.Material;

        var merged = WpfApp.Current.Resources.MergedDictionaries;
        // 기존 테마·재질 리소스를 모두 제거한 뒤 새 순서(테마 → 재질)로 추가합니다.
        if (_currentThemeDict is not null)
            merged.Remove(_currentThemeDict);
        if (_currentMaterialDict is not null)
            merged.Remove(_currentMaterialDict);
        _currentThemeDict = null;
        _currentMaterialDict = null;

        foreach (var file in ResolveResourceFiles(resolved, material))
        {
            var dict = LoadDictionary(file);
            if (dict is null) continue; // 파일이 없으면 건너뛰고 나머지 리소스로 동작합니다.
            merged.Add(dict);
            if (file.EndsWith("Material.xaml"))
                _currentMaterialDict = dict;
            else
                _currentThemeDict = dict;
        }
    }

    /// <summary>
    /// 테마·재질 조합에 따라 MergedDictionaries에 추가할 리소스 파일 목록을 계산합니다.
    /// 순수 함수라 WPF 없이 단위 테스트할 수 있습니다.
    /// - 플라스틱/메탈/우드: 테마 파일 + 재질 오버라이드 파일 (같은 키는 나중에 추가된 쪽이 이김).
    ///   (플라스틱 = 라이트 유리/세라믹, 다크 탄소섬유 — 합성 소재 계열)
    /// - 알 수 없는 값: 테마 파일 1개 — 기본.
    /// - 고대비: 재질을 무조건 무시하고 테마 파일만 — 접근성 안전망입니다.
    /// </summary>
    public static IReadOnlyList<string> ResolveResourceFiles(string resolvedTheme, string material)
    {
        var themeFile = $"{resolvedTheme}Theme.xaml";

        if (resolvedTheme == "HighContrast")
            return [themeFile]; // 고대비에서는 재질 적용을 항상 무시합니다.

        return material switch
        {
            "plastic" => [themeFile, $"{resolvedTheme}PlasticMaterial.xaml"],
            "metal"   => [themeFile, $"{resolvedTheme}MetalMaterial.xaml"],
            "wood"    => [themeFile, $"{resolvedTheme}WoodMaterial.xaml"],
            _         => [themeFile] // 알 수 없는 값은 기본(테마만)으로 유지합니다.
        };
    }

    /// <summary>
    /// Themes 폴더의 리소스 사전을 로드합니다.
    /// 파일이 없으면(빌드 누락 등) 예외를 잡아서 null을 돌려 앱이 깨지지 않게 합니다.
    /// </summary>
    private static ResourceDictionary? LoadDictionary(string fileName)
    {
        try
        {
            return new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/AltKey;component/Themes/{fileName}",
                                UriKind.Absolute)
            };
        }
        catch (Exception)
        {
            return null;
        }
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
