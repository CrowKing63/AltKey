using System.Windows;
using System.Windows.Input;
using AltKey.Views;

namespace AltKey.Tools;

/// <summary>
/// [역할] AltKey 편집 도구의 시작 화면입니다.
/// [접근성] 첫 포커스, Esc 닫기, 창 재활성화를 일관되게 유지합니다.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 접근성: 창이 열린 직후 첫 포커스를 예측 가능하게 고정합니다.
        Loaded += (_, _) => LayoutEditorButton.Focus();

        // 접근성: 키보드만으로도 언제든 창을 닫을 수 있도록 Esc를 통일 동작으로 제공합니다.
        PreviewKeyDown += OnPreviewKeyDown;
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
        // 프로세스 전체에서 같은 창을 재사용해 포커스 이동을 예측 가능하게 유지합니다.
        var window = ToolWindowRegistry.GetOrCreate("layout", null, attachOwner ? this : null);
        window.Show();
        window.Activate();
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
        var window = ToolWindowRegistry.GetOrCreate("dictionary", null, attachOwner ? this : null);
        window.Show();
        window.Activate();
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
        var window = ToolWindowRegistry.GetOrCreate("profile", null, attachOwner ? this : null);
        window.Show();
        window.Activate();
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
        var window = ToolWindowRegistry.GetOrCreate("ai-prompt", null, attachOwner ? this : null);
        window.Show();
        window.Activate();
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
        var extraArgs = BuildHeaderShortcutExtraArgs(headerButtonId, createNew);
        var window = ToolWindowRegistry.GetOrCreate("header-shortcut", extraArgs, attachOwner ? this : null);
        window.Show();
        window.Activate();
    }

    /// <summary>
    /// 상단바 단축키 편집기용 추가 인자 문자열을 조립합니다.
    /// </summary>
    private static string? BuildHeaderShortcutExtraArgs(string? headerButtonId, bool createNew)
    {
        if (headerButtonId is null && !createNew)
        {
            return null;
        }

        var parts = new List<string>();
        if (headerButtonId is not null) parts.Add($"--header-button-id {headerButtonId}");
        if (createNew) parts.Add("--header-button-mode create");
        return string.Join(" ", parts);
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
