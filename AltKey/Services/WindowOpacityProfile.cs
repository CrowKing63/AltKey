using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// [역할] 메인 창의 현재 투명도 규칙을 한곳에 모아 계산합니다.
/// [기능] 상시 투명도와 유휴 투명도 중 어떤 값을 써야 하는지 상태별로 일관되게 반환합니다.
/// </summary>
public static class WindowOpacityProfile
{
    /// <summary>
    /// 유휴가 아닐 때 유지할 기본 투명도를 계산합니다.
    /// 상시 투명도 기능이 꺼져 있으면 항상 완전 표시(1.0)를 반환합니다.
    /// </summary>
    public static double GetBaseOpacity(AppConfig config)
    {
        return config.ActiveOpacityEnabled
            ? ClampOpacity(config.OpacityActive)
            : 1.0;
    }

    /// <summary>
    /// 유휴 상태에 들어갔을 때 목표로 삼을 투명도를 계산합니다.
    /// 유휴 기능이 꺼져 있으면 추가로 흐려지지 않도록 기본 투명도를 그대로 반환합니다.
    /// </summary>
    public static double GetIdleOpacity(AppConfig config)
    {
        var baseOpacity = GetBaseOpacity(config);
        return config.IdleOpacityEnabled
            ? Math.Min(ClampOpacity(config.OpacityIdle), baseOpacity)
            : baseOpacity;
    }

    /// <summary>
    /// 유휴 투명도가 넘을 수 없는 상한값입니다.
    /// 상시 투명도를 켠 경우에는 그 값 이하로만 유휴 투명도를 둘 수 있습니다.
    /// </summary>
    public static double GetIdleOpacityMaximum(AppConfig config) => GetBaseOpacity(config);

    /// <summary>
    /// 마우스 이탈 후 유휴 타이머를 시작해야 하는지 알려 줍니다.
    /// </summary>
    public static bool ShouldStartIdleTimer(AppConfig config) => config.IdleOpacityEnabled;

    private static double ClampOpacity(double value) => Math.Clamp(value, 0.1, 1.0);
}
