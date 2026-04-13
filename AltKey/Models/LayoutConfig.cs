namespace AltKey.Models;

public record LayoutConfig(
    string Name,
    string? Language,
    List<KeyRow> Rows
);

public record KeyRow(List<KeySlot> Keys);
