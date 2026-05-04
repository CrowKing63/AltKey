using AltKey.Models;

namespace AltKey.Services;

/// <summary>
/// 레이아웃 편집기가 메인 앱 내부 서비스 구현에 직접 의존하지 않도록 분리한 저장소 경계입니다.
/// 도구 앱/메인 앱 모두 이 인터페이스만 의존하면 파일 저장소 구현을 교체해도 편집기 코드는 유지됩니다.
/// </summary>
public interface ILayoutRepository
{
    event Action? LayoutsChanged;

    string DefaultLayoutName { get; }

    IReadOnlyList<string> GetAvailableLayouts();

    LayoutConfig? TryLoad(string name, Action<Exception>? onError = null);

    void Save(string name, LayoutConfig config);

    bool Delete(string name);
}

