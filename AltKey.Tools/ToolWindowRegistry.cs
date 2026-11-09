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

    // 상단바 단축키 편집기만 창마다 다른 인자(버튼 ID, 생성 모드)를 받습니다.
    // 열린 창과 새 요청의 인자가 다르면 기존 창을 재사용해도 안 되므로,
    // 인자를 함께 저장해 비교할 수 있게 둡니다.
    private static readonly Dictionary<string, string?> _extraArgs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 도구 이름에 해당하는 창이 열려 있으면 기존 창을, 없으면 새 창을 만들어 반환합니다.
    /// 상단바 단축키 편집기("header-shortcut")만 예외: 인자가 바뀐 요청이면
    /// 기존 창을 닫고 새 창으로 만들어, 추가/편집 모드가 뒤섞이지 않게 합니다.
    /// Show()/Activate() 호출은 요청한 쪽(호출부)이 책임집니다.
    /// </summary>
    public static Window GetOrCreate(string toolName, string? extraArgs = null, Window? owner = null)
    {
        Window? toClose = null;
        lock (_open)
        {
            if (_open.TryGetValue(toolName, out var existing) && existing is { IsLoaded: true })
            {
                var argsChanged = toolName.Equals("header-shortcut", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(_extraArgs.GetValueOrDefault(toolName), extraArgs, StringComparison.Ordinal);

                if (!argsChanged)
                {
                    return existing;
                }

                // 레지스트리에서 먼저 제거한 뒤 창을 닫습니다.
                // Closed 이벤트 핸들러가 다시 이 잠금을 잡으므로,
                // 닫기 전에 항목을 지워야 재진입으로 멈추는 것을 막고,
                // 옛 창의 늦게 온 이벤트가 새 창 항목을 지우는 것도 막습니다.
                _open.Remove(toolName);
                _extraArgs.Remove(toolName);
                toClose = existing;
            }

            var window = CreateWindow(toolName, extraArgs, owner);
            _open[toolName] = window;
            _extraArgs[toolName] = extraArgs;
            window.Closed += (_, _) =>
            {
                lock (_open)
                {
                    // 레지스트리가 아직 이 창을 가리킬 때만 제거합니다.
                    // 닫히던 옛 창의 이벤트가 뒤늦게 새 창 항목을 지우는 것을 막습니다.
                    if (_open.TryGetValue(toolName, out var current) && ReferenceEquals(current, window))
                    {
                        _open.Remove(toolName);
                        _extraArgs.Remove(toolName);
                    }
                }
            };
        }

        // 잠금을 벗어난 뒤에서 창을 닫아 Closed 이벤트의 재진입 잠금을 피합니다.
        toClose?.Close();
        return _open[toolName];
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
