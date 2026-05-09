using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AltKey.Models;
using AltKey.Services;
using AltKey.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Tools;

/// <summary>
/// [역할] 상단바 커스텀 단축키 하나를 독립 창에서 편집하고 저장합니다.
/// [접근성] 긴 툴팁, 접근성 이름, 액션 파라미터 입력을 메인 설정 창과 분리해 한글 조합 입력과 포커스를 안정적으로 유지합니다.
/// </summary>
public partial class HeaderShortcutEditorWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _configService;
    private readonly string? _requestedHeaderButtonId;
    private string _editingId = "";
    private bool _isCreateMode;
    private string _windowTitle = "상단바 단축키 편집기";
    private string _iconText = "";
    private string _tooltipText = "";
    private string _accessibleNameText = "";
    private string _selectedPosition = "Right";
    private bool _isHeaderButtonVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ActionBuilderViewModel ActionBuilder { get; } = new();
    public IReadOnlyList<string> Positions { get; } = ["Left", "Right"];

    public string WindowTitle
    {
        get => _windowTitle;
        private set
        {
            if (_windowTitle == value) return;
            _windowTitle = value;
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string IconText
    {
        get => _iconText;
        set
        {
            if (_iconText == value) return;
            _iconText = value;
            OnPropertyChanged(nameof(IconText));
        }
    }

    public string TooltipText
    {
        get => _tooltipText;
        set
        {
            if (_tooltipText == value) return;
            _tooltipText = value;
            OnPropertyChanged(nameof(TooltipText));
        }
    }

    public string AccessibleNameText
    {
        get => _accessibleNameText;
        set
        {
            if (_accessibleNameText == value) return;
            _accessibleNameText = value;
            OnPropertyChanged(nameof(AccessibleNameText));
        }
    }

    public string SelectedPosition
    {
        get => _selectedPosition;
        set
        {
            var normalized = HeaderButtonConfig.NormalizePosition(value);
            if (_selectedPosition == normalized) return;
            _selectedPosition = normalized;
            OnPropertyChanged(nameof(SelectedPosition));
        }
    }

    public bool IsHeaderButtonVisible
    {
        get => _isHeaderButtonVisible;
        set
        {
            if (_isHeaderButtonVisible == value) return;
            _isHeaderButtonVisible = value;
            OnPropertyChanged(nameof(IsHeaderButtonVisible));
        }
    }

    public HeaderShortcutEditorWindow(string? headerButtonId = null, bool createNew = false)
    {
        InitializeComponent();

        _configService = App.Services.GetRequiredService<ConfigService>();
        _requestedHeaderButtonId = headerButtonId;
        _isCreateMode = createNew;

        DataContext = this;

        Loaded += (_, _) => IconTextBox.Focus();
        PreviewKeyDown += OnPreviewKeyDown;

        LoadFromConfig();
    }

    /// <summary>
    /// 편집 대상 ID가 있으면 해당 커스텀 버튼을 불러오고, 없으면 새 버튼 기본값으로 시작합니다.
    /// </summary>
    private void LoadFromConfig()
    {
        var current = _configService.Current.HeaderButtons
            .FirstOrDefault(button => button.Kind == HeaderButtonKind.Custom && button.Id == _requestedHeaderButtonId);

        if (current is null && !_isCreateMode)
        {
            current = _configService.Current.HeaderButtons
                .FirstOrDefault(button => button.Kind == HeaderButtonKind.Custom);
        }

        current ??= HeaderButtonConfig.CreateCustomDefault();
        _isCreateMode = _isCreateMode || !_configService.Current.HeaderButtons.Any(button => button.Id == current.Id);

        _editingId = current.Id;
        WindowTitle = _isCreateMode ? "상단바 단축키 추가" : "상단바 단축키 편집기";
        IconText = current.EffectiveIconText;
        TooltipText = current.EffectiveTooltip;
        AccessibleNameText = current.EffectiveAccessibleName;
        SelectedPosition = current.Position;
        IsHeaderButtonVisible = current.Visible;
        ActionBuilder.LoadFromAction(current.CustomAction);
    }

    /// <summary>
    /// 편집한 내용을 HeaderButtons 리스트에 저장하고, 메인 앱과 설정 창이 즉시 다시 읽도록 신호를 보냅니다.
    /// </summary>
    private void OnSave(object sender, RoutedEventArgs e)
    {
        var savedButton = BuildEditedButton();
        var nextButtons = _configService.Current.HeaderButtons
            .Select(CloneHeaderButtonConfig)
            .ToList();

        var existingIndex = nextButtons.FindIndex(button => button.Id == _editingId);
        if (existingIndex >= 0)
        {
            nextButtons[existingIndex] = savedButton;
        }
        else
        {
            nextButtons.Add(savedButton);
        }

        if (!ValidateHeaderButtonLimits(nextButtons, savedButton, existingIndex >= 0))
        {
            return;
        }

        _configService.Update(config => config.HeaderButtons = nextButtons);
        ToolsReloadSignalService.NotifyReloadHeaderButtons();

        _editingId = savedButton.Id;
        _isCreateMode = false;
        WindowTitle = "상단바 단축키 편집기";

        MessageBox.Show(
            "상단바 단축키를 저장했습니다.",
            "상단바 단축키 저장",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>
    /// 새 커스텀 단축키 개수와 좌우 표시 개수가 안전 한도를 넘지 않는지 확인합니다.
    /// 독립 편집기에서도 메인 앱과 같은 기준을 써야 저장 직후 상단바가 중앙 조작부를 가리지 않습니다.
    /// </summary>
    private static bool ValidateHeaderButtonLimits(
        IReadOnlyCollection<HeaderButtonConfig> buttons,
        HeaderButtonConfig savedButton,
        bool isEditingExisting)
    {
        if (!isEditingExisting
            && HeaderButtonConfig.CountCustomButtons(buttons, savedButton.Id) >= HeaderButtonConfig.MaxCustomButtonCount)
        {
            MessageBox.Show(
                $"커스텀 상단바 단축키는 최대 {HeaderButtonConfig.MaxCustomButtonCount}개까지 만들 수 있습니다.",
                "상단바 단축키 한도",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (!savedButton.Visible)
            return true;

        var maxVisibleButtons = HeaderButtonConfig.GetMaxVisibleButtons(savedButton.Position);
        var visibleButtonsOnSide = HeaderButtonConfig.CountVisibleButtons(buttons, savedButton.Position, savedButton.Id);
        if (visibleButtonsOnSide >= maxVisibleButtons)
        {
            var sideName = HeaderButtonConfig.NormalizePosition(savedButton.Position) == "Left" ? "좌측" : "우측";
            MessageBox.Show(
                $"상단바 {sideName}에는 최대 {maxVisibleButtons}개까지만 표시할 수 있습니다.\n위치를 바꾸거나 표시를 끄고 다시 저장해 주세요.",
                "상단바 표시 한도",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private HeaderButtonConfig BuildEditedButton()
    {
        var action = ActionBuilder.BuildAction() ?? new SendKeyAction("VK_A");
        var tooltip = string.IsNullOrWhiteSpace(TooltipText) ? "커스텀 상단바 단축키" : TooltipText.Trim();
        var accessibleName = string.IsNullOrWhiteSpace(AccessibleNameText) ? tooltip : AccessibleNameText.Trim();

        return new HeaderButtonConfig
        {
            Id = string.IsNullOrWhiteSpace(_editingId) ? HeaderButtonConfig.CreateCustomDefault().Id : _editingId,
            Kind = HeaderButtonKind.Custom,
            Visible = IsHeaderButtonVisible,
            Position = HeaderButtonConfig.NormalizePosition(SelectedPosition),
            DisplayMode = HeaderButtonDisplayMode.IconOnly,
            IconText = string.IsNullOrWhiteSpace(IconText) ? "새" : IconText.Trim(),
            Tooltip = tooltip,
            AccessibleName = accessibleName,
            CustomAction = CloneKeyAction(action)
        };
    }

    private static HeaderButtonConfig CloneHeaderButtonConfig(HeaderButtonConfig source) => new()
    {
        Id = source.Id,
        Kind = source.Kind,
        Visible = source.Visible,
        Position = HeaderButtonConfig.NormalizePosition(source.Position),
        DisplayMode = HeaderButtonDisplayMode.IconOnly,
        IconText = source.IconText,
        Tooltip = source.Tooltip,
        AccessibleName = source.AccessibleName,
        CustomAction = CloneKeyAction(source.CustomAction)
    };

    private static KeyAction? CloneKeyAction(KeyAction? action) => action switch
    {
        SendKeyAction sendKey => new SendKeyAction(sendKey.Vk),
        SendComboAction sendCombo => new SendComboAction(sendCombo.Keys.ToList()),
        ToggleStickyAction sticky => new ToggleStickyAction(sticky.Vk),
        SwitchLayoutAction switchLayout => new SwitchLayoutAction(switchLayout.Name),
        RunAppAction runApp => new RunAppAction(runApp.Path, runApp.Args),
        BoilerplateAction boilerplate => new BoilerplateAction(boilerplate.Text),
        ShellCommandAction shell => new ShellCommandAction(shell.Command, shell.Shell),
        VolumeControlAction volume => new VolumeControlAction(volume.Direction, volume.Step),
        ClipboardPasteAction clipboard => new ClipboardPasteAction(clipboard.Text),
        ToggleKoreanSubmodeAction => new ToggleKoreanSubmodeAction(),
        ToggleFunctionLayerAction => new ToggleFunctionLayerAction(),
        AiAction ai => new AiAction(ai.Prompt),
        _ => null
    };

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

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
