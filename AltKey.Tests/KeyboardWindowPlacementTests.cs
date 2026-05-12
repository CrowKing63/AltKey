using System.Windows;
using AltKey.Views;

namespace AltKey.Tests;

public class KeyboardWindowPlacementTests
{
    private static readonly Rect WorkArea = new(0, 0, 1920, 1080);

    [Fact]
    public void ComputeAnchoredTop_keeps_bottom_gap_when_expanding_from_collapsed_state()
    {
        double currentTop = 1044;
        double currentHeight = 28;
        double newHeight = 320;

        double nextTop = KeyboardWindowPlacement.ComputeAnchoredTop(
            currentTop,
            currentHeight,
            newHeight,
            WorkArea,
            KeyboardWindowPlacement.VerticalAnchor.Bottom,
            anchorGapOverride: 8);

        Assert.Equal(8, WorkArea.Bottom - (nextTop + newHeight), precision: 6);
    }

    [Fact]
    public void ComputeAnchoredTop_keeps_bottom_gap_when_height_shrinks()
    {
        double currentTop = 752;
        double currentHeight = 320;
        double newHeight = 280;

        double nextTop = KeyboardWindowPlacement.ComputeAnchoredTop(
            currentTop,
            currentHeight,
            newHeight,
            WorkArea,
            KeyboardWindowPlacement.VerticalAnchor.Bottom,
            anchorGapOverride: 8);

        Assert.Equal(8, WorkArea.Bottom - (nextTop + newHeight), precision: 6);
    }

    [Fact]
    public void ComputeAnchoredTop_keeps_top_gap_for_top_anchor()
    {
        double currentTop = 24;
        double currentHeight = 320;
        double newHeight = 280;

        double nextTop = KeyboardWindowPlacement.ComputeAnchoredTop(
            currentTop,
            currentHeight,
            newHeight,
            WorkArea,
            KeyboardWindowPlacement.VerticalAnchor.Top,
            anchorGapOverride: 24);

        Assert.Equal(24, nextTop, precision: 6);
    }

    [Fact]
    public void ComputeAnchoredTop_keeps_current_top_for_freeform_anchor()
    {
        double currentTop = 612;
        double currentHeight = 320;
        double newHeight = 28;

        double nextTop = KeyboardWindowPlacement.ComputeAnchoredTop(
            currentTop,
            currentHeight,
            newHeight,
            WorkArea,
            KeyboardWindowPlacement.VerticalAnchor.Freeform);

        Assert.Equal(currentTop, nextTop, precision: 6);
    }

    [Fact]
    public void ComputeAnchoredTop_clamps_to_work_area_when_new_height_would_overflow()
    {
        double currentTop = 1044;
        double currentHeight = 28;
        double newHeight = 1200;

        double nextTop = KeyboardWindowPlacement.ComputeAnchoredTop(
            currentTop,
            currentHeight,
            newHeight,
            WorkArea,
            KeyboardWindowPlacement.VerticalAnchor.Bottom,
            anchorGapOverride: 8);

        Assert.Equal(WorkArea.Top, nextTop, precision: 6);
    }

    [Fact]
    public void DetectVerticalAnchor_returns_bottom_when_window_is_closer_to_bottom()
    {
        var anchor = KeyboardWindowPlacement.DetectVerticalAnchor(752, 320, WorkArea);

        Assert.Equal(KeyboardWindowPlacement.VerticalAnchor.Bottom, anchor);
    }

    [Fact]
    public void DetectVerticalAnchor_returns_freeform_when_window_is_not_docked_to_edge()
    {
        var anchor = KeyboardWindowPlacement.DetectVerticalAnchor(570, 320, WorkArea);

        Assert.Equal(KeyboardWindowPlacement.VerticalAnchor.Freeform, anchor);
    }
}
