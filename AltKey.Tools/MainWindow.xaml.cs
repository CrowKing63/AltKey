using System.Windows;
using System.Windows.Input;
using AltKey.ViewModels;
using AltKey.Views;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Tools;

/// <summary>
/// [역할] AltKey 편집 도구의 시작 화면입니다.
/// [접근성] 첫 포커스, Esc 닫기, 창 재활성화를 일관되게 유지합니다.
/// </summary>
public partial class MainWindow : Window
{
    private LayoutEditorWindow? _layoutEditorWindow;
    private UserDictionaryEditorWindow? _userDictionaryEditorWindow;
    private ProfileMappingEditorWindow? _profileMappingEditorWindow;
    private AiPromptEditorWindow? _aiPromptEditorWindow;
    private HeaderShortcutEditorWindow? _headerShortcutEditorWindow;

    public MainWindow()
    {
        InitializeComponent();

        // 접근성: 창이 열린 직후 첫 포커스를 예측 가능하게 고정합니다.
        Loaded += (_, _) => LayoutEditorButton.Focus();

        // 접근성: 키보드만으로도 언제든 창을 닫을 수 있도록 Esc를 통일 동작으로 제공합니다.
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>
    /// 시작 인자에서 도구 이름을 읽어, 메인 앱에서 특정 편집기로 바로 열 수 있게 합니다.
    /// 지원 값: "layout", "dictionary", "profile", "ai-prompt", "header-shortcut"
    /// </summary>
    public void ApplyStartupArguments(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return;
        }

        var toolName = GetToolArgument(args);
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return;
        }

        Loaded += (_, _) =>
        {
            var headerButtonId = GetArgumentValue(args, "--header-button-id");
            var createNewHeaderButton = string.Equals(GetArgumentValue(args, "--header-button-mode"), "create", StringComparison.OrdinalIgnoreCase);

            if (string.Equals(toolName, "layout", StringComparison.OrdinalIgnoreCase))
            {
                OpenLayoutEditorWindow(attachOwner: false);
                // 접근성: 특정 편집기 바로가기로 진입한 경우 중간 허브 창을 닫아 불필요한 포커스 이동을 없앱니다.
                Close();
                return;
            }

            if (string.Equals(toolName, "dictionary", StringComparison.OrdinalIgnoreCase))
            {
                OpenUserDictionaryEditorWindow(attachOwner: false);
                Close();
                return;
            }

            if (string.Equals(toolName, "profile", StringComparison.OrdinalIgnoreCase))
            {
                OpenProfileMappingEditorWindow(attachOwner: false);
                Close();
                return;
            }

            if (string.Equals(toolName, "ai-prompt", StringComparison.OrdinalIgnoreCase))
            {
                OpenAiPromptEditorWindow(attachOwner: false);
                Close();
                return;
            }

            if (string.Equals(toolName, "header-shortcut", StringComparison.OrdinalIgnoreCase))
            {
                OpenHeaderShortcutEditorWindow(attachOwner: false, headerButtonId, createNewHeaderButton);
                Close();
            }
        };
    }

    /// <summary>
    /// "--tool layout" 형태의 인자를 파싱합니다.
    /// </summary>
    private static string? GetToolArgument(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--tool", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string? GetArgumentValue(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>
    /// 레이아웃 편집기를 엽니다.
    /// 이미 열려 있으면 새 창을 만들지 않고 기존 창을 활성화하여 포커스 유실을 줄입니다.
    /// </summary>
    private void OnOpenLayoutEditor(object sender, RoutedEventArgs e)
    {
        OpenLayoutEditorWindow(attachOwner: true);
    }

    /// <summary>
    /// 레이아웃 편집기 창을 생성/활성화합니다.
    /// attachOwner=false면 허브 창 없이 단독 편집기 모드로 띄웁니다.
    /// </summary>
    private void OpenLayoutEditorWindow(bool attachOwner)
    {
        if (_layoutEditorWindow is { IsLoaded: true })
        {
            _layoutEditorWindow.Activate();
            return;
        }

        var vm = App.Services.GetRequiredService<LayoutEditorViewModel>();
        _layoutEditorWindow = new LayoutEditorWindow(vm);
        if (attachOwner)
        {
            _layoutEditorWindow.Owner = this;
        }
        _layoutEditorWindow.Closed += (_, _) => _layoutEditorWindow = null;
        _layoutEditorWindow.Show();
    }

    /// <summary>
    /// 사용자 단어 편집기를 엽니다.
    /// 이미 열려 있으면 기존 창을 재사용해 화면 전환 혼란을 줄입니다.
    /// </summary>
    private void OnOpenUserDictionaryEditor(object sender, RoutedEventArgs e)
    {
        OpenUserDictionaryEditorWindow(attachOwner: true);
    }

    /// <summary>
    /// 사용자 단어 편집기 창을 생성/활성화합니다.
    /// attachOwner=false면 허브 창과 독립된 단일 창 흐름으로 엽니다.
    /// </summary>
    private void OpenUserDictionaryEditorWindow(bool attachOwner)
    {
        if (_userDictionaryEditorWindow is { IsLoaded: true })
        {
            _userDictionaryEditorWindow.Activate();
            return;
        }

        var vm = App.Services.GetRequiredService<UserDictionaryEditorViewModel>();
        _userDictionaryEditorWindow = new UserDictionaryEditorWindow(vm);
        if (attachOwner)
        {
            _userDictionaryEditorWindow.Owner = this;
        }
        _userDictionaryEditorWindow.Closed += (_, _) => _userDictionaryEditorWindow = null;
        _userDictionaryEditorWindow.Show();
    }

    /// <summary>
    /// 프로필 매핑 편집기를 엽니다.
    /// 메인 앱 런타임 책임(실제 전환)과 분리된 편집 전용 UI를 제공해 입력 안정성을 유지합니다.
    /// </summary>
    private void OnOpenProfileMappingEditor(object sender, RoutedEventArgs e)
    {
        OpenProfileMappingEditorWindow(attachOwner: true);
    }

    /// <summary>
    /// 프로필 매핑 편집기 창을 생성/활성화합니다.
    /// attachOwner=false면 설정 버튼에서 진입 시 허브 창을 생략할 수 있습니다.
    /// </summary>
    private void OpenProfileMappingEditorWindow(bool attachOwner)
    {
        if (_profileMappingEditorWindow is { IsLoaded: true })
        {
            _profileMappingEditorWindow.Activate();
            return;
        }

        _profileMappingEditorWindow = new ProfileMappingEditorWindow();
        if (attachOwner)
        {
            _profileMappingEditorWindow.Owner = this;
        }
        _profileMappingEditorWindow.Closed += (_, _) => _profileMappingEditorWindow = null;
        _profileMappingEditorWindow.Show();
    }

    /// <summary>
    /// AI 기본 프롬프트 편집기를 엽니다.
    /// 긴 한글 프롬프트 입력을 메인 설정 창에서 분리해 조합 입력 안정성과 포커스 예측 가능성을 높입니다.
    /// </summary>
    private void OnOpenAiPromptEditor(object sender, RoutedEventArgs e)
    {
        OpenAiPromptEditorWindow(attachOwner: true);
    }

    /// <summary>
    /// AI 기본 프롬프트 편집기 창을 생성/활성화합니다.
    /// attachOwner=false면 메인 앱에서 직접 진입할 때 허브 창을 건너뜁니다.
    /// </summary>
    private void OpenAiPromptEditorWindow(bool attachOwner)
    {
        if (_aiPromptEditorWindow is { IsLoaded: true })
        {
            _aiPromptEditorWindow.Activate();
            return;
        }

        _aiPromptEditorWindow = new AiPromptEditorWindow();
        if (attachOwner)
        {
            _aiPromptEditorWindow.Owner = this;
        }
        _aiPromptEditorWindow.Closed += (_, _) => _aiPromptEditorWindow = null;
        _aiPromptEditorWindow.Show();
    }

    /// <summary>
    /// 상단바 단축키 편집기를 엽니다.
    /// 메인 앱 설정 탭에서는 특정 버튼 편집 인자를 주고, 허브에서는 일반 편집기로 진입할 수 있습니다.
    /// </summary>
    private void OnOpenHeaderShortcutEditor(object sender, RoutedEventArgs e)
    {
        OpenHeaderShortcutEditorWindow(attachOwner: true, headerButtonId: null, createNew: false);
    }

    private void OpenHeaderShortcutEditorWindow(bool attachOwner, string? headerButtonId, bool createNew)
    {
        if (_headerShortcutEditorWindow is { IsLoaded: true })
        {
            _headerShortcutEditorWindow.Activate();
            return;
        }

        _headerShortcutEditorWindow = new HeaderShortcutEditorWindow(headerButtonId, createNew);
        if (attachOwner)
        {
            _headerShortcutEditorWindow.Owner = this;
        }

        _headerShortcutEditorWindow.Closed += (_, _) => _headerShortcutEditorWindow = null;
        _headerShortcutEditorWindow.Show();
    }

    /// <summary>
    /// 닫기 버튼 동작입니다.
    /// </summary>
    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Esc 키 입력 시 창을 닫아 키보드 중심 사용성을 유지합니다.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close();
    }
}
