using AltKey.Models;
using AltKey.Services;

namespace AltKey.Services.InputLanguage;

/// 언어 입력 모듈의 공통 계약.
/// 이번 릴리스에서는 KoreanInputModule 단 하나만 존재.
public interface IInputLanguageModule
{
    /// ISO 언어 코드. "ko".
    string LanguageCode { get; }

    /// 현재 서브모드. "가/A" 토글 버튼이 라벨로 참조.
    InputSubmode ActiveSubmode { get; }

    /// 토글 버튼에 표시할 라벨. HangulJamo → "가", QuietEnglish → "A".
    string ComposeStateLabel { get; }

    /// 자동완성에 노출할 현재 조합 문자열.
    /// HangulJamo: HangulComposer.Current
    /// QuietEnglish: 누적 중인 영문 prefix
    string CurrentWord { get; }

    /// 자동완성 제안 변경 이벤트.
    event Action<IReadOnlyList<string>>? SuggestionsChanged;

    /// 서브모드 변경 이벤트.
    event Action<InputSubmode>? SubmodeChanged;

    /// 키 입력 처리. true면 호출자가 HandleAction을 스킵해야 함(모듈이 유니코드/SendInput으로 이미 처리).
    bool HandleKey(KeySlot slot, KeyContext ctx);

    /// 자동완성 제안 수락.
    /// 반환값: (백스페이스 개수, 전체 단어, 접두사 길이) — 화면에서 지울 글자 수, 최종 텍스트,
    /// 현재 조합과 전체 단어가 공유하는 앞부분 길이.
    /// Unicode 모드 호출자는 백스페이스 개수만큼 지운 뒤 전체 단어[접두사 길이..] (꼬리만)을 전송한다.
    /// VirtualKey 모드 호출자는 VK_BACK × 백스페이스 개수 + 전체 단어 전송을 유지한다.
    (int backspaceCount, string fullWord, int prefixLength) AcceptSuggestion(string suggestion);

    /// 외부(AutoCompleteService)에서 단어가 확정되었음을 엔진에 알립니다.
    /// 엔진 내부의 lastCommittedWord를 갱신하여 bigram 컨텍스트를 동기화합니다.
    void NotifyWordCommitted(string word);

    /// 직전에 확정된 단어 (bigram 문맥). 모듈이 유일한 소유자 — 서비스는 복제하지 않는다.
    string? LastCommittedWord { get; }

    /// "가/A" 토글 버튼이 호출. 이전 조합 상태는 플러시하고 Submode 반전.
    void ToggleSubmode();

    /// 단어 구분자(공백/엔터/탭) 도달 시 호출.
    void OnSeparator();

    /// 레이아웃 전환 등 상태 초기화.
    void Reset();

    /// 첫 번째 슬롯 좌클릭: 텍스트 변경 없이 사전에만 기록.
    void CommitCurrentWord();

    /// 첫 번째 슬롯 우클릭: 취소 플래그 설정 (이후 FinalizeComposition에서 학습 건너뜀).
    void CancelComposition();
}

/// HandleKey / AcceptSuggestion 호출 시 필요한 런타임 문맥.
/// 모듈이 InputService에 직접 의존하지 않도록 분리.
public sealed record KeyContext(
    bool ShowUpperCase,
    bool HasActiveModifiers,
    bool HasActiveModifiersExcludingShift,
    InputMode InputMode,
    int TrackedOnScreenLength
);
