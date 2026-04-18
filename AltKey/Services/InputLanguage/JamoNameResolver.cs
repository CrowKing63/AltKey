namespace AltKey.Services.InputLanguage;

public static class JamoNameResolver
{
    private static readonly Dictionary<string, string> _names = new()
    {
        // 자음
        ["ㄱ"] = "기역", ["ㄲ"] = "쌍기역", ["ㄴ"] = "니은", ["ㄷ"] = "디귿",
        ["ㄸ"] = "쌍디귿", ["ㄹ"] = "리을", ["ㅁ"] = "미음", ["ㅂ"] = "비읍",
        ["ㅃ"] = "쌍비읍", ["ㅅ"] = "시옷", ["ㅆ"] = "쌍시옷", ["ㅇ"] = "이응",
        ["ㅈ"] = "지읒", ["ㅉ"] = "쌍지읒", ["ㅊ"] = "치읓", ["ㅋ"] = "키읔",
        ["ㅌ"] = "티읕", ["ㅍ"] = "피읖", ["ㅎ"] = "히읗",
        // 모음
        ["ㅏ"] = "아", ["ㅐ"] = "애", ["ㅑ"] = "야", ["ㅒ"] = "얘",
        ["ㅓ"] = "어", ["ㅔ"] = "에", ["ㅕ"] = "여", ["ㅖ"] = "예",
        ["ㅗ"] = "오", ["ㅛ"] = "요", ["ㅜ"] = "우", ["ㅠ"] = "유",
        ["ㅡ"] = "으", ["ㅣ"] = "이",
        ["ㅘ"] = "와", ["ㅙ"] = "왜", ["ㅚ"] = "외", ["ㅝ"] = "워",
        ["ㅞ"] = "웨", ["ㅟ"] = "위", ["ㅢ"] = "의",
    };

    public static string? ResolveKorean(string? jamo)
        => jamo is not null && _names.TryGetValue(jamo, out var name) ? name : null;
}
