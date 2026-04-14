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
    /// <summary>항상 표시되는 한글 기본 자모 (통합 레이아웃용)</summary>
    [property: JsonPropertyName("hangul_label")]      string? HangulLabel = null,
    /// <summary>Shift 상태에서 표시되는 한글 자모 (쌍자음/쌍모음)</summary>
    [property: JsonPropertyName("hangul_shift_label")] string? HangulShiftLabel = null
);
