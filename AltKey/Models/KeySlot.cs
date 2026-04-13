using System.Text.Json.Serialization;

namespace AltKey.Models;

public record KeySlot(
    string Label,
    [property: JsonPropertyName("shift_label")] string? ShiftLabel,
    KeyAction? Action,
    double Width = 1.0,
    double Height = 1.0,
    [property: JsonPropertyName("style_key")] string StyleKey = ""
);
