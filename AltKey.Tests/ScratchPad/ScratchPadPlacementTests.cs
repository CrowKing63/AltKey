using System.Windows;
using AltKey.Views;
using Xunit;

namespace AltKey.Tests.ScratchPad;

/// <summary>스크래치패드 배치(ScratchPadPlacement.PlacePad)가 키보드 위치 기준으로 올바른 좌표를 계산하는지 검증합니다.</summary>
public class ScratchPadPlacementTests
{
    // 작업 영역: 1920×1040 (하단 작업표시줄 40px 가정) — 주 모니터 기준.
    private static readonly Rect WorkArea = new(0, 0, 1920, 1040);

    // 패드 고정 크기 600×320 (창 스펙과 동일).
    private const double PadWidth = 600;
    private const double PadHeight = 320;

    [Fact]
    public void 키보드_상반부_이면_패드는_아래에_수평_중앙()
    {
        // 키보드가 화면 상단 쪽에 있으면 패드는 그 아래로.
        var keyboard = new Rect(660, 100, 900, 350);

        var (left, top) = ScratchPadPlacement.PlacePad(keyboard, WorkArea, PadWidth, PadHeight);

        Assert.Equal(810, left, 2);   // 660 + (900-600)/2
        Assert.Equal(454, top, 2);    // 100 + 350 + Gap(4)
    }

    [Fact]
    public void 키보드_하반부_이면_패드는_위에_수평_중앙()
    {
        var keyboard = new Rect(660, 700, 900, 350);

        var (left, top) = ScratchPadPlacement.PlacePad(keyboard, WorkArea, PadWidth, PadHeight);

        Assert.Equal(810, left, 2);
        Assert.Equal(376, top, 2);    // 700 - 320 - Gap(4)
    }

    [Fact]
    public void 키보드가_패드보다_좁아도_키보드_중앙_기준()
    {
        var keyboard = new Rect(100, 100, 400, 350);

        var (left, top) = ScratchPadPlacement.PlacePad(keyboard, WorkArea, PadWidth, PadHeight);

        // (400-600)/2 = -100 → 100 + (-100) = 0
        Assert.Equal(0, left, 2);
        Assert.Equal(454, top, 2);
    }

    [Fact]
    public void 작업_영역_오른쪽_끝을_넘으면_경계로_클램프()
    {
        var keyboard = new Rect(1700, 100, 900, 350);

        var (left, top) = ScratchPadPlacement.PlacePad(keyboard, WorkArea, PadWidth, PadHeight);

        Assert.Equal(1320, left, 2);  // 1920 - 600
        Assert.Equal(454, top, 2);
    }

    [Fact]
    public void 정확히_중심선_위면_위로_배치()
    {
        // 기준선이 "엄밀히 상반부"(<)이므로 중심선 위에 있으면 '아래'가 아님 → 위로.
        var keyboard = new Rect(660, WorkArea.Top + WorkArea.Height / 2, 900, 350);

        var (_, top) = ScratchPadPlacement.PlacePad(keyboard, WorkArea, PadWidth, PadHeight);

        Assert.Equal(520 - PadHeight - 4, top, 2);
    }

    [Fact]
    // 극소 작업 영역(패드가 화면보다 큰 경우)에서도 좌표가 항상 범위 안에 있는지 확인합니다.
    public void 극소_작업_영역에서_패드가_커도_범위_내()
    {
        var tinyWorkArea = new Rect(0, 0, 500, 400);
        var keyboard = new Rect(0, 250, 400, 100);

        var (left, top) = ScratchPadPlacement.PlacePad(keyboard, tinyWorkArea, PadWidth, PadHeight);

        // 패드(600×320)가 영역(500×400)보다 커서 계산값이 벗어날 때 경계(0)로 붙습니다.
        Assert.Equal(0, left, 2);   // Max(0, 500-600) → 0, 계산값 -100 → 0으로 클램프
        Assert.Equal(0, top, 2);    // 계산값 250-320-4 = -74 → 상단 경계 0으로 클램프
    }
}
