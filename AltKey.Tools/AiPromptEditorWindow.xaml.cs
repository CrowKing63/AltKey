using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AltKey.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Tools;

/// <summary>
/// [역할] AI 기본 프롬프트를 독립 창에서 편집하고 저장합니다.
/// [접근성] 메인 설정 창과 분리해 긴 한글 조합 입력, 첫 포커스, Esc 닫기 흐름을 안정적으로 유지합니다.
/// </summary>
public partial class AiPromptEditorWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _configService;
    private string _promptText = string.Empty;

    public string PromptText
    {
        get => _promptText;
        set
        {
            if (_promptText == value)
            {
                return;
            }

            _promptText = value;
            OnPropertyChanged(nameof(PromptText));
        }
    }

    public AiPromptEditorWindow()
    {
        InitializeComponent();

        _configService = App.Services.GetRequiredService<ConfigService>();
        DataContext = this;

        Loaded += (_, _) => PromptTextBox.Focus();
        PreviewKeyDown += OnPreviewKeyDown;

        LoadFromConfig();
    }

    /// <summary>
    /// 현재 설정 파일의 AI 기본 프롬프트를 입력창으로 가져옵니다.
    /// 사용자가 기존 문장을 조금만 고쳐도 되도록 항상 최신 저장값을 먼저 보여줍니다.
    /// </summary>
    private void LoadFromConfig()
    {
        PromptText = _configService.Current.AiDefaultPrompt ?? string.Empty;
    }

    /// <summary>
    /// 편집한 기본 프롬프트를 설정 파일에 저장하고, 메인 앱이 즉시 다시 읽도록 재로드 신호를 보냅니다.
    /// </summary>
    private void OnSave(object sender, RoutedEventArgs e)
    {
        var nextPrompt = PromptText?.Trim() ?? string.Empty;

        _configService.Update(c => c.AiDefaultPrompt = nextPrompt);
        ToolsReloadSignalService.NotifyReloadAiSettings();

        MessageBox.Show(
            "AI 기본 프롬프트를 저장했습니다.",
            "AI 프롬프트 저장",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
