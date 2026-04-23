using System.Text.Json.Serialization;

namespace AltKey.Models;

public record LayoutConfig(
    string Name,
    string? Language,
    List<KeyColumn>? Columns = null
);

public record KeyColumn(
    [property: JsonPropertyName("gap")] double Gap = 0.5,
    List<KeyRow>? Rows = null
);

public record KeyRow(List<KeySlot> Keys);
