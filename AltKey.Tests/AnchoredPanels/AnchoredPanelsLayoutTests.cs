using System.Windows;
using AltKey.Services;
using Xunit;

namespace AltKey.Tests.AnchoredPanels;

/// <summary>
/// AnchoredPanelsLayout.PlacePanels — 키보드 창에 앵커되는 패널(빠른 메모·클립보드) 좌표 계산 검증.
/// 단일 패널은 기존 스크래치패드 규칙과 동일해야 하고, 복수 패널은 한 줄 나란히 + 총 너비 기준 중앙 정렬.
/// </summary>
public class AnchoredPanelsLayoutTests
{
    // 작업 영역: 1920×1040 (하단 작업표시줄 40px 가정) — 주 모니터 기준.
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);

    // 실제 등록 크기와 동일 (MainViewModel.Register).
    private static AnchoredPanelsLayout.PanelSpec Clipboard() => new("clipboard", 0, 360, 420);
    private static AnchoredPanelsLayout.PanelSpec ScratchPad() => new("scratchpad", 1, 600, 320);

    // ── 단일 패널 (기존 스크래치패드 규칙 유지) ────────────────────────────

    [Fact]
    public void 단일_패널_키보드_상반부_이면_아래에_수평_중앙()
    {
        var keyboard = new Rect(660, 100, 900, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, [ScratchPad()]);

        var (left, top) = result["scratchpad"];
        Assert.Equal(810, left, 2);   // 660 + (900-600)/2
        Assert.Equal(454, top, 2);    // 100 + 350 + Gap(4)
    }

    [Fact]
    public void 단일_패널_키보드_하반부_이면_위에_수평_중앙()
    {
        var keyboard = new Rect(660, 700, 900, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, [ScratchPad()]);

        var (left, top) = result["scratchpad"];
        Assert.Equal(810, left, 2);
        Assert.Equal(376, top, 2);    // 700 - 320 - Gap(4)
    }

    [Fact]
    public void 단일_패널_키보드_좁아도_중앙_기준()
    {
        var keyboard = new Rect(100, 100, 400, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, [ScratchPad()]);

        // (400-600)/2 = -100 → 100 + (-100) = 0
        Assert.Equal(0, result["scratchpad"].left, 2);
        Assert.Equal(454, result["scratchpad"].top, 2);
    }

    [Fact]
    public void 단일_패널_오른쪽_끝_클램프()
    {
        var keyboard = new Rect(1700, 100, 900, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, [ScratchPad()]);

        Assert.Equal(1320, result["scratchpad"].left, 2);  // 1920 - 600
        Assert.Equal(454, result["scratchpad"].top, 2);
    }

    [Fact]
    public void 단일_패널_정확히_중심선_위면_위로_배치()
    {
        // 기준선이 "엄밀히 상반부"(<)이므로 중심선 위에 있으면 위로.
        var keyboard = new Rect(660, WorkArea.Top + WorkArea.Height / 2, 900, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, [ScratchPad()]);

        Assert.Equal(520 - 320 - 4, result["scratchpad"].top, 2);
    }

    [Fact]
    public void 단일_패널_극소_작업_영역_범위_내()
    {
        var tinyWorkArea = new Rect(0, 0, 500, 400);
        var keyboard = new Rect(0, 250, 400, 100);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, tinyWorkArea, [ScratchPad()]);

        // 패드(600×320)가 영역보다 커서 계산값이 벗어날 때 경계(0)로 붙습니다.
        Assert.Equal(0, result["scratchpad"].left, 2);
        Assert.Equal(0, result["scratchpad"].top, 2);
    }

    // ── 복수 패널 (한 줄 나란히 + 총 너비 기준 중앙 정렬) ────────────────

    [Fact]
    public void 복수_패널_아래_배치면_상단_동일선_총너비_중앙()
    {
        var keyboard = new Rect(660, 100, 900, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, [Clipboard(), ScratchPad()]);

        // 총 너비 360+4+600=964 (간격은 패널 사이만) → left0 = 660 + (900-964)/2 = 628
        Assert.Equal(628, result["clipboard"].left, 2);
        Assert.Equal(992, result["scratchpad"].left, 2);   // 628 + 360 + Gap(4)

        // 아래 배치: 모든 패널 상단이 같은 선 (키보드 하단 + Gap).
        double rowTop = keyboard.Bottom + 4;
        Assert.Equal(rowTop, result["clipboard"].top, 2);
        Assert.Equal(rowTop, result["scratchpad"].top, 2);

        // 순서: order 0(클립보드)이 왼쪽.
        Assert.True(result["clipboard"].left < result["scratchpad"].left);
    }

    [Fact]
    public void 복수_패널_위_배치면_하단_동일선_총너비_중앙()
    {
        var keyboard = new Rect(660, 700, 900, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, [Clipboard(), ScratchPad()]);

        Assert.Equal(628, result["clipboard"].left, 2);
        Assert.Equal(992, result["scratchpad"].left, 2);

        // 위 배치: 모든 패널 하단이 같은 선 (키보드 상단 - Gap).
        double rowBottom = keyboard.Top - 4;
        Assert.Equal(rowBottom, result["clipboard"].top + 420, 2);
        Assert.Equal(rowBottom, result["scratchpad"].top + 320, 2);
    }

    [Fact]
    public void 복수_패널_총너비가_작업영역을_넘으면_폴백()
    {
        // 좁은 모니터: 총 너비(968)가 작업 영역(900)보다 넓음 → 폴백 규칙.
        var narrowArea = new Rect(0, 0, 900, 1040);
        var keyboard = new Rect(300, 100, 500, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, narrowArea, [Clipboard(), ScratchPad()]);

        // 1번째(order 0) 패널은 rowSide(아래), 나머지 패널은 반대편(위).
        Assert.Equal(keyboard.Bottom + 4, result["clipboard"].top, 2);
        // 위 배치 계산값(100-320-4=-224)이 작업 영역을 벗어나 경계(0)로 붙습니다.
        Assert.Equal(0, result["scratchpad"].top, 2);

        // 각자 키보드 기준 수평 중앙.
        Assert.Equal(300 + (500 - 360) / 2, result["clipboard"].left, 2);
        Assert.Equal(300 + (500 - 600) / 2, result["scratchpad"].left, 2);
    }

    [Fact]
    public void 복수_패널_경계_클램프()
    {
        // 키보드 창이 화면 왼쪽 끝이라 총 너비 중앙 정렬값이 음수가 되는 경우.
        var keyboard = new Rect(0, 100, 400, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, [Clipboard(), ScratchPad()]);

        // left0 = 0 + (400-968)/2 = -284 → 0으로 클램프.
        Assert.Equal(0, result["clipboard"].left, 2);
        Assert.Equal(364, result["scratchpad"].left, 2);   // 0 + 360 + Gap(4)
    }

    [Fact]
    public void 빈_목록이면_빈_결과()
    {
        var keyboard = new Rect(660, 100, 900, 350);
        var result = AnchoredPanelsLayout.PlacePanels(keyboard, WorkArea, []);

        Assert.Empty(result);
    }
}
