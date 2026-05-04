using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// 기존 LayoutService/ConfigService를 래핑해 편집기 전용 경계(ILayoutRepository)를 제공하는 어댑터입니다.
/// </summary>
public sealed class LayoutRepository : ILayoutRepository
{
    private readonly LayoutService _layoutService;
    private readonly ConfigService _configService;

    public LayoutRepository(LayoutService layoutService, ConfigService configService)
    {
        _layoutService = layoutService;
        _configService = configService;
        _layoutService.LayoutsChanged += () => LayoutsChanged?.Invoke();
    }

    public event Action? LayoutsChanged;

    public string DefaultLayoutName => _configService.Current.DefaultLayout;

    public IReadOnlyList<string> GetAvailableLayouts() => _layoutService.GetAvailableLayouts();

    public LayoutConfig? TryLoad(string name, Action<Exception>? onError = null) =>
        _layoutService.TryLoad(name, onError);

    public void Save(string name, LayoutConfig config) => _layoutService.Save(name, config);

    public bool Delete(string name) => _layoutService.Delete(name);
}

