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

    [Fact]
    public void Load_migrates_legacy_toggle_korean_submode_to_sendkey_hangul()
    {
        var dir = CreateTempLayoutsDir();
        try
        {
            var legacy = @"{
  ""name"": ""My"",
  ""language"": null,
  ""columns"": [
    {
      ""gap"": 0.5,
      ""rows"": [
        {
          ""keys"": [
            {
              ""label"": ""가/A"",
              ""action"": { ""type"": ""ToggleKoreanSubmode"" }
            }
          ]
        }
      ]
    }
  ]
}";
            File.WriteAllText(Path.Combine(dir, "My.json"), legacy);

            var service = new LayoutService(dir);
            var layout = service.Load("My");

            var key = layout.Columns!.Single().Rows!.Single().Keys!.Single();
            var action = Assert.IsType<SendKeyAction>(key.Action);
            Assert.Equal("VK_HANGUL", action.Vk);
            Assert.Equal("가/A", key.Label); // 라벨/그리드 등 나머지는 유지
            Assert.True(service.IsLayoutMigrated("My"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_marks_only_layouts_that_were_actually_migrated()
    {
        var dir = CreateTempLayoutsDir();
        try
        {
            var clean = JsonSerializer.Serialize(new LayoutConfig(
                "Clean", null,
                [new KeyColumn(0.5, [new KeyRow([new KeySlot("A", null, new SendKeyAction("VK_A"))])])]),
                JsonOptions.Default);
            File.WriteAllText(Path.Combine(dir, "Clean.json"), clean);

            var service = new LayoutService(dir);
            service.Load("Clean");

            Assert.False(service.IsLayoutMigrated("Clean"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// 세션 기록이 지워져도 파일에 옛 액션이 남아 있으면 편집기 경고가 유지되는지 확인합니다.
    /// 저장 후에는 파일이 정리되므로 경고가 사라지는지도 함께 확인합니다.
    [Fact]
    public void migration_warning_uses_file_marker_after_cache_invalidation()
    {
        var dir = CreateTempLayoutsDir();
        try
        {
            var legacy = @"{
  ""name"": ""My"",
  ""language"": null,
  ""columns"": [
    {
      ""gap"": 0.5,
      ""rows"": [
        {
          ""keys"": [
            { ""label"": ""가/A"", ""action"": { ""type"": ""ToggleKoreanSubmode"" } }
          ]
        }
      ]
    }
  ]
}";
            File.WriteAllText(Path.Combine(dir, "My.json"), legacy);
            var service = new LayoutService(dir);
            service.Load("My");

            service.InvalidateCache();
            Assert.True(service.IsLayoutMigrated("My"));

            var clean = new LayoutConfig(
                "My", null,
                [new KeyColumn(0.5, [new KeyRow([new KeySlot("가/A", null, new SendKeyAction("VK_HANGUL"))])])]);
            service.Save("My", clean);

            Assert.False(service.IsLayoutMigrated("My"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// type 값이 문자열이 아닌 노드가 있어도 예외 없이 로드되는지 확인합니다.
    [Fact]
    public void Load_does_not_throw_on_non_string_type_node()
    {
        var dir = CreateTempLayoutsDir();
        try
        {
            var odd = @"{
  ""name"": ""Odd"",
  ""language"": null,
  ""columns"": [
    {
      ""gap"": 0.5,
      ""rows"": [
        {
          ""keys"": [
            {
              ""label"": ""ToggleKoreanSubmode"",
              ""action"": { ""type"": ""SendKey"", ""vk"": ""VK_A"" },
              ""extra"": { ""type"": 123 }
            }
          ]
        }
      ]
    }
  ]
}";
            File.WriteAllText(Path.Combine(dir, "Odd.json"), odd);

            var layout = new LayoutService(dir).Load("Odd");

            var action = Assert.IsType<SendKeyAction>(
                layout.Columns!.Single().Rows!.Single().Keys!.Single().Action);
            Assert.Equal("VK_A", action.Vk);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateTempLayoutsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "altkey-layout-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
