using System.Text.Json;
using AltKey.Models;

namespace AltKey.Tests.AiLocal;

// 로컬 AI 엔진·모델 설치 명세(ModelManifest)의 직렬화와 구성 요소 조회를 검증하는 테스트입니다.
public class ModelManifestTests
{
    [Fact]
    // manifest를 JSON으로 왕복 직렬화해도 모든 필드가 그대로 유지되는지 확인합니다.
    public void Manifest_roundTrips_throughJson()
    {
        var manifest = new ModelManifest
        {
            Entries =
            [
                new ManifestEntry { Component = "engine", FileName = "llama-server.exe", SourceUrl = "https://example.com/engine", Sha256 = "abc123", SizeBytes = 100, Version = "b12262" },
                new ManifestEntry { Component = "model", FileName = "EXAONE.gguf", SourceUrl = "https://example.com/model", Sha256 = "def456", SizeBytes = 200, Version = "Q8_0" }
            ]
        };

        var json = JsonSerializer.Serialize(manifest);
        var back = JsonSerializer.Deserialize<ModelManifest>(json);

        Assert.NotNull(back);
        Assert.Equal(2, back.Entries.Count);
        Assert.Equal("engine", back.Entries[0].Component);
        Assert.Equal("llama-server.exe", back.Entries[0].FileName);
        Assert.Equal("abc123", back.Entries[0].Sha256);
        Assert.Equal(100, back.Entries[0].SizeBytes);
        Assert.Equal("b12262", back.Entries[0].Version);
    }

    [Fact]
    // GetEntry가 구성 종류(engine/model)에 맞는 항목을 반환하고, 없는 종류는 null인지 확인합니다.
    public void GetEntry_returnsMatchingComponent_OrNull()
    {
        var manifest = new ModelManifest
        {
            Entries =
            [
                new ManifestEntry { Component = "engine", FileName = "llama-server.exe" },
                new ManifestEntry { Component = "model", FileName = "EXAONE.gguf" }
            ]
        };

        Assert.Equal("llama-server.exe", manifest.GetEntry("engine")?.FileName);
        Assert.Equal("EXAONE.gguf", manifest.GetEntry("model")?.FileName);
        Assert.Null(manifest.GetEntry("nope"));
    }
}
