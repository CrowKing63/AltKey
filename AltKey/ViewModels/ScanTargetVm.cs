namespace AltKey.ViewModels;

/// <summary>
/// [접근성][L3] 스캔 가능한 UI 대상을 통합 표현하는 데이터 모델입니다.
/// [참고] 키보드 키/현재 단어/제안 단어를 같은 방식으로 훑을 수 있게 만듭니다.
/// </summary>
public sealed class ScanTargetVm
{
    // 화면에 보이거나 음성 안내에 사용할 접근성 이름입니다.
    public required string AccessibleName { get; init; }

    // 실제 선택(Enter/스위치 선택) 시 실행할 동작입니다.
    public required Action Activate { get; init; }

    // 스캔 포커스 하이라이트를 켜거나 끄는 처리기입니다.
    public required Action<bool> SetScanFocused { get; init; }

    // UI에 표시할 텍스트입니다.
    public required string DisplayText { get; init; }

    // 어떤 종류의 대상인지 구분하는 값입니다.
    public required string Kind { get; init; }
}
