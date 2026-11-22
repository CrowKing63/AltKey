using AltKey.Services;

namespace AltKey.Tests.Services;

/// <summary>
/// ThemeService — 테마·재질 조합에 따라 로드할 리소스 파일 목록(ResolveResourceFiles)을 검증합니다.
/// 순수 함수라 WPF 없이 실행됩니다.
/// </summary>
public class ThemeServiceTests
{
    [Fact]
    public void Plastic_Light_AppendsMaterialOverride()
        => Assert.Equal(["LightTheme.xaml", "LightPlasticMaterial.xaml"],
                       ThemeService.ResolveResourceFiles("Light", "plastic"));

    [Fact]
    public void Plastic_Dark_AppendsMaterialOverride()
        => Assert.Equal(["DarkTheme.xaml", "DarkPlasticMaterial.xaml"],
                       ThemeService.ResolveResourceFiles("Dark", "plastic"));

    [Fact]
    public void Metal_AppendsMaterialOverride_AfterTheme()
        => Assert.Equal(["DarkTheme.xaml", "DarkMetalMaterial.xaml"],
                       ThemeService.ResolveResourceFiles("Dark", "metal"));

    [Fact]
    public void Wood_AppendsMaterialOverride_AfterTheme()
        => Assert.Equal(["LightTheme.xaml", "LightWoodMaterial.xaml"],
                       ThemeService.ResolveResourceFiles("Light", "wood"));

    [Theory]
    [InlineData("plastic")]
    [InlineData("metal")]
    [InlineData("wood")]
        public void HighContrast_IgnoresMaterial(string material)
        => Assert.Equal(["HighContrastTheme.xaml"],
                       ThemeService.ResolveResourceFiles("HighContrast", material));

    [Fact]
    public void UnknownMaterial_FallsBackToThemeFileOnly()
        => Assert.Equal(["DarkTheme.xaml"],
                       ThemeService.ResolveResourceFiles("Dark", "marble"));
}
