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
    private ProfileMappingReviewWindow? _profileMappingReviewWindow;

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
    /// 지원 값: "layout", "dictionary"
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
            if (string.Equals(toolName, "layout", StringComparison.OrdinalIgnoreCase))
            {
                OnOpenLayoutEditor(this, new RoutedEventArgs());
                return;
            }

            if (string.Equals(toolName, "dictionary", StringComparison.OrdinalIgnoreCase))
            {
                OnOpenUserDictionaryEditor(this, new RoutedEventArgs());
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

    /// <summary>
    /// 레이아웃 편집기를 엽니다.
    /// 이미 열려 있으면 새 창을 만들지 않고 기존 창을 활성화하여 포커스 유실을 줄입니다.
    /// </summary>
    private void OnOpenLayoutEditor(object sender, RoutedEventArgs e)
    {
        if (_layoutEditorWindow is { IsLoaded: true })
        {
            _layoutEditorWindow.Activate();
            return;
        }

        var vm = App.Services.GetRequiredService<LayoutEditorViewModel>();
        _layoutEditorWindow = new LayoutEditorWindow(vm);
        _layoutEditorWindow.Owner = this;
        _layoutEditorWindow.Closed += (_, _) => _layoutEditorWindow = null;
        _layoutEditorWindow.Show();
    }

    /// <summary>
    /// 사용자 단어 편집기를 엽니다.
    /// 이미 열려 있으면 기존 창을 재사용해 화면 전환 혼란을 줄입니다.
    /// </summary>
    private void OnOpenUserDictionaryEditor(object sender, RoutedEventArgs e)
    {
        if (_userDictionaryEditorWindow is { IsLoaded: true })
        {
            _userDictionaryEditorWindow.Activate();
            return;
        }

        var vm = App.Services.GetRequiredService<UserDictionaryEditorViewModel>();
        _userDictionaryEditorWindow = new UserDictionaryEditorWindow(vm);
        _userDictionaryEditorWindow.Owner = this;
        _userDictionaryEditorWindow.Closed += (_, _) => _userDictionaryEditorWindow = null;
        _userDictionaryEditorWindow.Show();
    }

    /// <summary>
    /// 프로필 매핑 편집의 "1단계 검토" 창을 엽니다.
    /// 문서 태스크 5의 목적(구조 분석/적합성 판단/2단계 후보 결정)을
    /// 접근성 속성을 갖춘 별도 창에서 확인할 수 있게 합니다.
    /// </summary>
    private void OnOpenProfileMappingReview(object sender, RoutedEventArgs e)
    {
        if (_profileMappingReviewWindow is { IsLoaded: true })
        {
            _profileMappingReviewWindow.Activate();
            return;
        }

        _profileMappingReviewWindow = new ProfileMappingReviewWindow();
        _profileMappingReviewWindow.Owner = this;
        _profileMappingReviewWindow.Closed += (_, _) => _profileMappingReviewWindow = null;
        _profileMappingReviewWindow.Show();
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
