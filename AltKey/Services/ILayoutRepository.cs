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

    /// <summary>레이아웃 이름이 기본 제공(프리셋) 레이아웃인지 판정합니다.</summary>
    bool IsBuiltin(string name);

    IReadOnlyList<string> GetAvailableLayouts();

    LayoutConfig? TryLoad(string name, Action<Exception>? onError = null);

    /// <summary>
    /// 해당 레이아웃을 Load/TryLoad에서 자동 마이그레이션했는지(제거된 ToggleKoreanSubmode → SendKey VK_HANGUL).
    /// 세션 범위이며, 로드 직후 파일을 다시 읽을 때 갱신됩니다.
    /// </summary>
    bool IsLayoutMigrated(string name);

    void Save(string name, LayoutConfig config);

    bool Delete(string name);
}

