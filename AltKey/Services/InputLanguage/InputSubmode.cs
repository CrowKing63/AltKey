namespace AltKey.Services.InputLanguage;

/// 한국어 모듈 내부 입력 상태. "가/A" 토글 버튼이 이 값을 스위치한다.
public enum InputSubmode
{
    /// 한글 자모 조합 모드 (기본). 키 라벨은 자모, 입력 경로는 HangulComposer.
    HangulJamo,

    /// 조용한 영어 모드. OS IME는 건드리지 않고 유니코드로 영문만 입력.
    /// 키 라벨은 알파벳, 사전은 EnglishDictionary.
    QuietEnglish,
}
