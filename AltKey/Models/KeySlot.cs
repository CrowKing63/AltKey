using System.Text.Json.Serialization;

namespace AltKey.Models;

public record KeySlot(
    string Label,
    [property: JsonPropertyName("shift_label")]       string? ShiftLabel,
    KeyAction? Action,
    double Width = 1.0,
    double Height = 1.0,
    [property: JsonPropertyName("style_key")]         string StyleKey = "",
    [property: JsonPropertyName("gap_before")]        double GapBefore = 0.0,
    /// <summary>Shift 상태에서 표시되지 않는 영어 알파벳 (통합 레이아웃용)</summary>
    [property: JsonPropertyName("english_label")]      string? EnglishLabel = null,
    /// <summary>Shift 상태에서 표시되는 영어 알파벳</summary>
    [property: JsonPropertyName("english_shift_label")] string? EnglishShiftLabel = null
);
