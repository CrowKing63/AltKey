using System.Text;
using System.Windows.Automation;

namespace AltKey.Services;

/// <summary>
/// [역할] AI 텍스트 처리 시 현재 선택된 텍스트를 UIA TextPattern으로 읽어옵니다.
/// [왜] 합성 Ctrl+C는 브라우저(Chromium 계열)·Electron 앱에서 무시되는 경우가 많아
/// 클립보드 폴백은 항상 신뢰할 수 없고, UIA로 직접 읽는 것이 가장 안정적입니다.
/// [스레드 정책] UIA 쿼리는 Dispatcher 워커(Task.Run)에서만 호출 — Dispatcher 위에서
/// 호출하면 Chromium/Edge 렌더러가 멈출 수 있습니다 (docs/feature-ai-capture/plan.md §3.2).
/// </summary>
public sealed class AiTextCaptureService
{
    // [왜] selection-hook이 Chromium 기준 10단계에서 검증으로 채택한 상한.
    // 상위 요소까지 탐색해도 GetSelection()만 호출하므로 (문서 전체 읽기 금지) 깊이 늘림의 비용은 낮습니다.
    internal const int MaxAncestorDepth = 10;

    // [왜] 테스트에서 실제 AutomationElement 없이 탐색 로직을 검증할 수 있도록
    // 포커스 요소 생성 → ancestor 순회 → 선택 텍스트 반환을 최소 인터페이스로 분리합니다.
    private readonly Func<ITextPatternWalker?> _walkerFactory;

    public AiTextCaptureService()
        : this(CreateRealWalker) { }

    internal AiTextCaptureService(Func<ITextPatternWalker?> walkerFactory)
        => _walkerFactory = walkerFactory;

    /// <summary>
    /// 현재 포커스 앱의 선택 텍스트를 반환합니다.
    /// [실패 조건] 포커스 요소 없음 / UIA 주입 실패 / 탐색 후에도 빈 선택 — 모두 null.
    /// [스레드] 호출자는 Dispatcher가 아닐 때 워커에서 호출해야 합니다.
    /// </summary>
    public string? CaptureSelectedText()
    {
        var walker = _walkerFactory();
        if (walker is null)
            return null;

        for (int depth = 0; depth < MaxAncestorDepth; depth++)
        {
            var text = walker.SelectedText();
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            if (!walker.MoveToParent())
                break;
        }
        return null;
    }

    private static ITextPatternWalker? CreateRealWalker()
    {
        var focused = AutomationElement.FocusedElement;
        return focused is null ? null : new UiAutomationWalker(focused);
    }
}

/// <summary>
/// UIA 요소 체인의 탐색 인터페이스.
/// [왜] 실제 AutomationElement는 테스트에서 가짜로 만들 수 없어서, 서비스 테스트는
/// 이 인터페이스를 가짜 체인으로 대체해 탐색 로직(depth 한도, 빈 텍스트 스킵, 상위 이동)만 검증합니다.
/// </summary>
internal interface ITextPatternWalker
{
    /// <summary>현재 요소의 선택 텍스트. 빈/실패 시 null.</summary>
    string? SelectedText();

    /// <summary>상위 요소로 이동. 더 이상 상위가 없으면 false.</summary>
    bool MoveToParent();
}

/// <summary>
/// 실제 UIA 요소 체인을 순회하는 구현.
/// [주의] 기본 구현체는 ancestor 탐색에서 DocumentRange/문서 전체 읽기를 하지 않습니다.
/// </summary>
internal sealed class UiAutomationWalker : ITextPatternWalker
{
    private AutomationElement? _element;

    public UiAutomationWalker(AutomationElement element)
    {
        _element = element;
    }

    public string? SelectedText()
    {
        var el = _element;
        if (el is null)
            return null;

        try
        {
            if (!el.TryGetCurrentPattern(TextPattern.Pattern, out object? patternObj))
                return null;
            if (patternObj is not TextPattern tp)
                return null;
            if (tp.SupportedTextSelection == SupportedTextSelection.None)
                return null;

            var ranges = tp.GetSelection();
            if (ranges is null)
                return null;

            var sb = new StringBuilder();
            foreach (var range in ranges)
            {
                try
                {
                    sb.Append(range.GetText(-1));
                }
                catch
                {
                    // 한 범위가 실패해도 나머지는 시도
                }
            }

            var text = sb.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    public bool MoveToParent()
    {
        if (_element is null)
            return false;

        try
        {
            AutomationElement? parent = TreeWalker.ControlViewWalker.GetParent(_element);
            if (parent is null)
                return false;
            _element = parent;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
