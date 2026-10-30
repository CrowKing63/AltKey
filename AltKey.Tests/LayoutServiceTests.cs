using System.IO;
using System.Text.Json;
using AltKey.Models;
using AltKey.Services;

namespace AltKey.Tests;

public class LayoutServiceTests
{
    [Fact]
    public void Delete_rejects_builtin_layout_and_keeps_file()
    {
        var dir = CreateTempLayoutsDir();
        try
        {
            var service = new LayoutService(dir);
            File.WriteAllText(Path.Combine(dir, "Basic.json"),
                JsonSerializer.Serialize(new LayoutConfig("Basic", null, []), JsonOptions.Default));

            Assert.False(service.Delete("Basic"));
            Assert.True(File.Exists(Path.Combine(dir, "Basic.json")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Delete_allows_user_layout()
    {
        var dir = CreateTempLayoutsDir();
        try
        {
            var service = new LayoutService(dir);
            File.WriteAllText(Path.Combine(dir, "My.json"),
                JsonSerializer.Serialize(new LayoutConfig("My", null, []), JsonOptions.Default));

            Assert.True(service.Delete("My"));
            Assert.False(File.Exists(Path.Combine(dir, "My.json")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("basic")]
    [InlineData("BASIC PLUS")]
    [InlineData("Basic Plus")]
    public void IsBuiltin_recognizes_preset_names(string name)
    {
        Assert.True(LayoutService.IsBuiltin(name));
    }

    [Theory]
    [InlineData("My")]
    [InlineData("")]
    [InlineData("Basic Extra")]
    public void IsBuiltin_rejects_non_preset_names(string name)
    {
        Assert.False(LayoutService.IsBuiltin(name));
    }

    [Fact]
    public void Constructor_seeds_builtin_layouts_from_source_dir()
    {
        var source = CreateTempLayoutsDir();
        var target = CreateTempLayoutsDir();
        try
        {
            // 소스 폴더에 내장 레이아웃 두 개를 준비한다.
            File.WriteAllText(Path.Combine(source, "Basic.json"),
                JsonSerializer.Serialize(new LayoutConfig("Basic", null, []), JsonOptions.Default));
            File.WriteAllText(Path.Combine(source, "Basic Plus.json"),
                JsonSerializer.Serialize(new LayoutConfig("Basic Plus", null, []), JsonOptions.Default));

            var service = new LayoutService(target, source);

            // 빈 데이터 폴더에 내장 레이아웃이 복사되어야 한다.
            Assert.Contains("Basic", service.GetAvailableLayouts());
            Assert.Contains("Basic Plus", service.GetAvailableLayouts());
        }
        finally
        {
            Directory.Delete(source, recursive: true);
            Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void Constructor_does_not_overwrite_existing_builtin_layout()
    {
        var source = CreateTempLayoutsDir();
        var target = CreateTempLayoutsDir();
        try
        {
            File.WriteAllText(Path.Combine(source, "Basic.json"),
                JsonSerializer.Serialize(new LayoutConfig("Basic", null, []), JsonOptions.Default));
            // 데이터 폴더에 이미 있는 파일은 덮어써서 안 된다.
            File.WriteAllText(Path.Combine(target, "Basic.json"), "{\"existing\":true}");

            new LayoutService(target, source);

            Assert.Equal("{\"existing\":true}", File.ReadAllText(Path.Combine(target, "Basic.json")));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
            Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void Constructor_missing_source_dir_is_harmless()
    {
        var target = CreateTempLayoutsDir();
        try
        {
            // 소스 폴더가 없어도 생성자는 정상 동작한다.
            var service = new LayoutService(target, Path.Combine(target, "no-such-dir"));
            Assert.Empty(service.GetAvailableLayouts());
        }
        finally
        {
            Directory.Delete(target, recursive: true);
        }
    }

    private static string CreateTempLayoutsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "altkey-layout-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
