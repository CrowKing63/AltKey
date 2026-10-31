using System.Windows;
using AltKey.ViewModels;
using AltKey.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Tools;

/// <summary>
/// [역할] Tools 프로세스 전체에서 편집기 창 생성/재사용을 한 곳에서 관리합니다.
/// [기능] 같은 종류의 창이 이미 열려 있으면 새 창을 만들지 않고 기존 창을 돌려 포커스 혼란을 줄입니다.
/// 허브 모드와 상주(resident) 모드가 동일한 창 집합을 공유하도록 합니다.
/// </summary>
internal static class ToolWindowRegistry
{
    private static readonly Dictionary<string, Window> _open = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 도구 이름에 해당하는 창이 열려 있으면 기존 창을, 없으면 새 창을 만들어 반환합니다.
    /// Show()/Activate() 호출은 요청한 쪽(호출부)이 책임집니다.
    /// </summary>
    public static Window GetOrCreate(string toolName, string? extraArgs = null, Window? owner = null)
    {
        lock (_open)
        {
            if (_open.TryGetValue(toolName, out var existing) && existing is { IsLoaded: true })
            {
                return existing;
            }

            var window = CreateWindow(toolName, extraArgs, owner);
            _open[toolName] = window;
            window.Closed += (_, _) =>
            {
                lock (_open)
                {
                    // 레지스트리가 아직 이 창을 가리킬 때만 제거합니다.
                    // 닫히던 옛 창의 이벤트가 뒤늦게 새 창 항목을 지우는 것을 막습니다.
                    if (_open.TryGetValue(toolName, out var current) && ReferenceEquals(current, window))
                    {
                        _open.Remove(toolName);
                    }
                }
            };
            return window;
        }
    }

    /// <summary>
    /// 도구 이름에 따라 새 편집기 창을 만듭니다.
    /// 지원 값: "layout", "dictionary", "profile", "ai-prompt", "header-shortcut"
    /// </summary>
    private static Window CreateWindow(string toolName, string? extraArgs, Window? owner)
    {
        Window window = toolName.ToLowerInvariant() switch
        {
            "layout" => new LayoutEditorWindow(App.Services.GetRequiredService<LayoutEditorViewModel>()),
            "dictionary" => new UserDictionaryEditorWindow(App.Services.GetRequiredService<UserDictionaryEditorViewModel>()),
            "profile" => new ProfileMappingEditorWindow(),
            "ai-prompt" => new AiPromptEditorWindow(),
            "header-shortcut" => CreateHeaderShortcutWindow(extraArgs),
            _ => throw new ArgumentException($"지원하지 않는 도구 이름: {toolName}"),
        };

        if (owner is not null)
        {
            window.Owner = owner;
        }

        return window;
    }

    /// <summary>
    /// 상단바 단축키 편집기는 추가 인자(버튼 ID, 생성 모드)를 받아 창을 만듭니다.
    /// </summary>
    private static Window CreateHeaderShortcutWindow(string? extraArgs)
    {
        var headerButtonId = GetArgumentValue(extraArgs, "--header-button-id");
        var createNew = string.Equals(
            GetArgumentValue(extraArgs, "--header-button-mode"),
            "create",
            StringComparison.OrdinalIgnoreCase);
        return new HeaderShortcutEditorWindow(headerButtonId, createNew);
    }

    /// <summary>
    /// "--옵션 값" 형태의 인자 문자열에서 옵션 값을 추출합니다.
    /// </summary>
    internal static string? GetArgumentValue(string? args, string optionName)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return null;
        }

        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            if (!string.Equals(parts[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < parts.Length)
            {
                return parts[i + 1];
            }
        }

        return null;
    }
}
