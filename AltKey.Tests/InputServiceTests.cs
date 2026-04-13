using AltKey.Models;
using AltKey.Platform;
using AltKey.Services;

namespace AltKey.Tests;

/// Win32.SendInput을 호출하지 않는 테스트용 InputService
internal class NoOpInputService : InputService
{
    public override void SendKeyDown(VirtualKeyCode vk) { }
    public override void SendKeyUp(VirtualKeyCode vk)   { }
}

public class InputServiceTests
{
    private static NoOpInputService Create() => new();

    // T-2.5 — 첫 번째 클릭: StickyKeys에 추가
    [Fact]
    public void ToggleModifier_FirstClick_AddsToStickyKeys()
    {
        var svc = Create();
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);

        Assert.Contains(VirtualKeyCode.VK_SHIFT, svc.StickyKeys);
        Assert.DoesNotContain(VirtualKeyCode.VK_SHIFT, svc.LockedKeys);
    }

    // T-2.5 — 두 번째 클릭: LockedKeys에도 추가 (StickyKeys도 유지)
    [Fact]
    public void ToggleModifier_SecondClick_AddsToLockedKeys()
    {
        var svc = Create();
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);

        Assert.Contains(VirtualKeyCode.VK_SHIFT, svc.StickyKeys);
        Assert.Contains(VirtualKeyCode.VK_SHIFT, svc.LockedKeys);
    }

    // T-2.5 — 세 번째 클릭: 잠금 해제 (두 집합 모두에서 제거)
    [Fact]
    public void ToggleModifier_ThirdClick_ClearsAll()
    {
        var svc = Create();
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);

        Assert.DoesNotContain(VirtualKeyCode.VK_SHIFT, svc.StickyKeys);
        Assert.DoesNotContain(VirtualKeyCode.VK_SHIFT, svc.LockedKeys);
    }

    // T-2.5 — ReleaseTransientModifiers: 잠금 키는 해제하지 않는다
    [Fact]
    public void ReleaseTransientModifiers_DoesNotClearLockedKeys()
    {
        var svc = Create();

        // Ctrl을 영구 잠금으로
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);
        svc.ToggleModifier(VirtualKeyCode.VK_CONTROL);

        // Shift는 일회성 고정(transient)으로
        svc.ToggleModifier(VirtualKeyCode.VK_SHIFT);

        svc.ReleaseTransientModifiers();

        // Ctrl은 여전히 잠금 상태
        Assert.Contains(VirtualKeyCode.VK_CONTROL, svc.LockedKeys);
        // Shift는 해제됨
        Assert.DoesNotContain(VirtualKeyCode.VK_SHIFT, svc.StickyKeys);
    }
}
