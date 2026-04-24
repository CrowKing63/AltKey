using System.Text.Json.Serialization;

namespace AltKey.Models;

/// <summary>
/// [역할] 키보드의 '버튼 하나'가 가져야 할 정보를 담는 그릇(Data Model)입니다.
/// [기능] 버튼에 써진 글자, 버튼을 눌렀을 때의 동작, 버튼의 가로/세로 크기 등을 저장합니다.
/// </summary>
public record KeySlot(
    string Label, // 버튼에 기본적으로 표시될 글자입니다.
    [property: JsonPropertyName("shift_label")]       string? ShiftLabel, // Shift를 눌렀을 때 바뀔 글자입니다.
    KeyAction? Action, // 이 버튼을 누르면 실행될 동작입니다.
    double Width = 1.0,  // 버튼의 가로 비율입니다 (1.0이 기본).
    double Height = 1.0, // 버튼의 세로 비율입니다 (1.0이 기본).
    [property: JsonPropertyName("style_key")]         string StyleKey = "", // 버튼의 색상이나 스타일을 지정하는 이름입니다.
    [property: JsonPropertyName("gap_before")]        double GapBefore = 0.0, // 버튼 앞에 띄울 공백의 크기입니다.
    /// <summary>Shift 상태에서 표시되지 않는 영어 알파벳 (통합 레이아웃용)</summary>
    [property: JsonPropertyName("english_label")]      string? EnglishLabel = null,
    /// <summary>Shift 상태에서 표시되는 영어 알파벳</summary>
    [property: JsonPropertyName("english_shift_label")] string? EnglishShiftLabel = null
);
