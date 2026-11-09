using AltKey.Models;

namespace AltKey.Tests.HeaderButton;

// 상단바 단축키 편집기의 추가/편집 분리(HeaderButtonConfig.ResolveEditTarget)를
// 확인하는 테스트입니다. 창 재사용으로 저장 대상이 기존 버튼으로 바뀌던 버그의
// 뿌리인 "ID 없이 열리면 첫 커스텀 버튼 불러오기" 폴백이 제거되었는지 검증합니다.
public class HeaderButtonResolveTargetTests
{
    [Fact]
    // 추가 모드에서는 기존 커스텀 버튼이 있어도 새 버튼으로 시작하고 기존 항목을 참조하지 않는지 확인합니다.
    public void ResolveEditTarget_create_mode_ignores_existing_buttons()
    {
        var existing = HeaderButtonConfig.CreateCustomDefault();
        List<HeaderButtonConfig> buttons = [existing];

        var (target, isCreate, error) = HeaderButtonConfig.ResolveEditTarget(buttons, null, createNew: true);

        Assert.NotNull(target);
        Assert.True(isCreate);
        Assert.Null(error);
        Assert.NotEqual(existing.Id, target!.Id);
    }

    [Fact]
    // 요청한 ID가 존재하면 해당 커스텀 버튼을 편집 대상으로 반환하는지 확인합니다.
    public void ResolveEditTarget_returns_requested_button()
    {
        var existing = HeaderButtonConfig.CreateCustomDefault();
        List<HeaderButtonConfig> buttons = [existing];

        var (target, isCreate, error) = HeaderButtonConfig.ResolveEditTarget(buttons, existing.Id, createNew: false);

        Assert.Same(existing, target);
        Assert.False(isCreate);
        Assert.Null(error);
    }

    [Fact]
    // 요청한 ID가 없으면 아무 버튼도 불러오지 않고 오류를 반환하는지 확인합니다.
    public void ResolveEditTarget_returns_error_when_id_missing()
    {
        List<HeaderButtonConfig> buttons = [HeaderButtonConfig.CreateCustomDefault()];

        var (target, isCreate, error) = HeaderButtonConfig.ResolveEditTarget(buttons, "custom-not-there", createNew: false);

        Assert.Null(target);
        Assert.False(isCreate);
        Assert.NotNull(error);
    }

    [Fact]
    // ID 없이 편집 모드로 들어오면 추가 대상으로 처리해 기존 버튼을 덮어쓰지 않는지 확인합니다.
    public void ResolveEditTarget_falls_back_to_create_when_no_id()
    {
        var existing = HeaderButtonConfig.CreateCustomDefault();
        List<HeaderButtonConfig> buttons = [existing];

        var (target, isCreate, error) = HeaderButtonConfig.ResolveEditTarget(buttons, null, createNew: false);

        Assert.NotNull(target);
        Assert.True(isCreate);
        Assert.Null(error);
        Assert.NotEqual(existing.Id, target!.Id);
    }
}