namespace AltKey.Models;

/// <summary>
/// [역할] 자동완성 추천의 표시 모드를 구분합니다.
/// [기능] 일반적인 단어 추천(Normal)과 바이그램 문맥 기반 추천(Bigram)을 UI가 다르게 표시할 수 있도록 돕습니다.
/// </summary>
public enum SuggestionMode
{
    /// 일반 단어 추천 (사용자 사전 + 내장 사전 기반).
    Normal,
    /// 직전에 확정된 단어를 기반으로 한 바이그램 추천.
    Bigram
}
