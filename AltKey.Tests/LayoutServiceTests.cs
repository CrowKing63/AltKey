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

    private static string CreateTempLayoutsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "altkey-layout-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
