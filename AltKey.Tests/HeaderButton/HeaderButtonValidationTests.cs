using AltKey.Models;

namespace AltKey.Tests.HeaderButton;

// 상단바 버튼 한도 검증(HeaderButtonConfig.TryValidateSave)이
// 설정 탭과 독립 편집기에서 같은 기준을 쓰는지 확인하는 테스트입니다.
public class HeaderButtonValidationTests
{
    [Fact]
    // 커스텀 10개에 하나를 더 추가하면 거부되는지 확인합니다.
    public void TryValidateSave_rejects_11th_custom_button()
    {
        var buttons = CreateCustomButtons(HeaderButtonConfig.MaxCustomButtonCount);
        var candidate = HeaderButtonConfig.CreateCustomDefault();

        var (valid, _) = HeaderButtonConfig.TryValidateSave(buttons, candidate, isEditingExisting: false);

        Assert.False(valid);
    }

    [Fact]
    // 커스텀 9개에 하나를 더 추가하면 허용되는지 확인합니다.
    // 기존 버튼은 표시를 꺼둬서 좌우 표시 한도와 무관하게 커스텀 총개수만 검증합니다.
    public void TryValidateSave_allows_10th_custom_button()
    {
        var buttons = CreateCustomButtons(HeaderButtonConfig.MaxCustomButtonCount - 1);
        foreach (var button in buttons)
        {
            button.Visible = false;
        }
        var candidate = HeaderButtonConfig.CreateCustomDefault();

        var (valid, _) = HeaderButtonConfig.TryValidateSave(buttons, candidate, isEditingExisting: false);

        Assert.True(valid);
    }

    [Fact]
    // 좌측에 이미 8개 표시 중일 때 9번째 표시 추가는 거부되는지 확인합니다.
    public void TryValidateSave_rejects_9th_visible_button_on_left_side()
    {
        var buttons = CreateVisibleButtons("Left", HeaderButtonConfig.MaxVisibleButtonsLeft);
        var candidate = HeaderButtonConfig.CreateCustomDefault();
        candidate.Position = "Left";

        var (valid, sideName) = HeaderButtonConfig.TryValidateSave(buttons, candidate, isEditingExisting: false);

        Assert.False(valid);
        Assert.Equal("좌측", sideName);
    }

    [Fact]
    // 좌측에 7개 표시 중일 때 8번째 표시 추가는 허용되는지 확인합니다.
    public void TryValidateSave_allows_8th_visible_button_on_left_side()
    {
        var buttons = CreateVisibleButtons("Left", HeaderButtonConfig.MaxVisibleButtonsLeft - 1);
        var candidate = HeaderButtonConfig.CreateCustomDefault();
        candidate.Position = "Left";

        var (valid, _) = HeaderButtonConfig.TryValidateSave(buttons, candidate, isEditingExisting: false);

        Assert.True(valid);
    }

    [Fact]
    // 기존 버튼을 편집할 때 같은 ID가 표시 개수에 중복 계산되지 않는지 확인합니다.
    public void TryValidateSave_does_not_double_count_existing_button()
    {
        var buttons = CreateVisibleButtons("Right", HeaderButtonConfig.MaxVisibleButtonsRight);
        var candidate = buttons[0];

        var (valid, _) = HeaderButtonConfig.TryValidateSave(buttons, candidate, isEditingExisting: true);

        Assert.True(valid);
    }

    [Fact]
    // 표시를 꺼둔 버튼은 좌우 표시 개수에 포함되지 않는지 확인합니다.
    public void CountVisibleButtons_ignores_hidden_buttons()
    {
        var buttons = CreateVisibleButtons("Right", HeaderButtonConfig.MaxVisibleButtonsRight);
        buttons[0].Visible = false;

        var count = HeaderButtonConfig.CountVisibleButtons(buttons, "Right");

        Assert.Equal(HeaderButtonConfig.MaxVisibleButtonsRight - 1, count);
    }

    private static List<HeaderButtonConfig> CreateCustomButtons(int count) =>
        Enumerable.Range(0, count).Select(_ => HeaderButtonConfig.CreateCustomDefault()).ToList();

    private static List<HeaderButtonConfig> CreateVisibleButtons(string position, int count) =>
        Enumerable.Range(0, count)
            .Select(_ =>
            {
                var button = HeaderButtonConfig.CreateCustomDefault();
                button.Position = position;
                return button;
            })
            .ToList();
}
