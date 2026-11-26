using AltKey.Services;
using Xunit;

namespace AltKey.Tests.AiLocal;

// UIA TextPattern 탐색 로직(빈 텍스트 스킵·상위 이동·depth 한도)을 가짜 탐색 체인으로 검증한다.
// 실제 AutomationElement는 테스트에서 만들어 검증할 수 없기 때문에,
// 서비스의 탐색 루프가 호출하는 최소 인터페이스(ITextPatternWalker)를 대체해 순회만 검증한다.
public class AiTextCaptureServiceTests
{
    /// <summary>
    /// 화면 어딘가에서 "텍스트가 있는 단계"를 순서대로 흉내내는 가짜 탐색기.
    /// [왜] 실제 요소 계층(AutomationElement)은 UIA 의존이라 가짜로 만들 수 없다 —
    /// 가짜 체인은 서비스 탐색 루프의 로직(depth 한도·빈 텍스트 스킵·상위 이동)만 검증에 쓴다.
    /// </summary>
    private sealed class FakeWalker(IReadOnlyList<string?> levelTexts) : ITextPatternWalker
    {
        private int _depth;
        private int _selectedCalls;
        private int _moveCalls;

        public int SelectedTextCallCount => _selectedCalls;
        public int MoveToParentCallCount => _moveCalls;

        public string? SelectedText()
        {
            _selectedCalls++;
            return _depth < levelTexts.Count ? levelTexts[_depth] : null;
        }

        public bool MoveToParent()
        {
            _moveCalls++;
            _depth++;
            // 더 이상 위 레벨이 없으면 false — 서비스는 다음 반복에서 루프를 끊는다.
            return _depth < levelTexts.Count;
        }
    }

    [Fact]
    // 첫 레벨에서 텍스트가 있으면 바로 반환하고 상위 탐색은 하지 않는다.
    public void CaptureSelectedText_returns_text_when_text_found_in_first_level()
    {
        var fake = new FakeWalker(["선택된 텍스트", null, null]);
        var service = new AiTextCaptureService(() => fake);

        Assert.Equal("선택된 텍스트", service.CaptureSelectedText());
        Assert.Equal(0, fake.MoveToParentCallCount);
    }

    [Fact]
    // null/빈 문자열/공백 레벨을 건너뛰고 첫 비어있지 않은 텍스트를 반환한다.
    public void CaptureSelectedText_skips_empty_or_whitespace_levels()
    {
        var fake = new FakeWalker([null, "", "   ", "셋째 레벨", null]);
        var service = new AiTextCaptureService(() => fake);

        Assert.Equal("셋째 레벨", service.CaptureSelectedText());
    }

    [Fact]
    // 모든 레벨이 빈 텍스트면 null을 반환한다.
    public void CaptureSelectedText_returns_null_when_all_levels_empty()
    {
        var fake = new FakeWalker([null, "", " ", null]);
        var service = new AiTextCaptureService(() => fake);

        Assert.Null(service.CaptureSelectedText());
    }

    [Fact]
    // 탐색 깊이 상한(MaxAncestorDepth)을 넘긴 뒤에는 더 이상 요소를 조회하지 않는다.
    public void CaptureSelectedText_stops_at_max_ancestor_depth()
    {
        // 깊이 한도(10)를 모두 소진하도록 null만 담은 체인
        var levels = new string?[AiTextCaptureService.MaxAncestorDepth + 2];
        for (int i = 0; i < levels.Length; i++) levels[i] = null;
        var fake = new FakeWalker(levels);
        var service = new AiTextCaptureService(() => fake);

        Assert.Null(service.CaptureSelectedText());
        Assert.Equal(AiTextCaptureService.MaxAncestorDepth, fake.SelectedTextCallCount);
    }

    [Fact]
    // 포커스 요소가 없어 가짜 체인을 만들 수 없는 경우 → null 반환.
    public void CaptureSelectedText_returns_null_when_WalkerFactory_returns_null()
    {
        var service = new AiTextCaptureService(() => null);

        Assert.Null(service.CaptureSelectedText());
    }
}
