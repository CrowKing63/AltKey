using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AltKey.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Tools;

/// <summary>
/// [역할] 프로세스 이름 → 레이아웃 이름 매핑을 독립 도구 창에서 편집/저장합니다.
/// [접근성] 첫 포커스, 선형 탭 순서, Esc 닫기를 제공해 키보드/스크린리더 사용성을 유지합니다.
/// </summary>
public partial class ProfileMappingEditorWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _configService;
    private readonly LayoutService _layoutService;

    public ObservableCollection<ProfileMappingEditorRow> Rows { get; } = [];
    public ObservableCollection<string> AvailableLayouts { get; } = [];

    private string _validationSummaryText = string.Empty;
    public string ValidationSummaryText
    {
        get => _validationSummaryText;
        private set
        {
            if (_validationSummaryText == value) return;
            _validationSummaryText = value;
            OnPropertyChanged(nameof(ValidationSummaryText));
        }
    }

    public ProfileMappingEditorWindow()
    {
        InitializeComponent();

        _configService = App.Services.GetRequiredService<ConfigService>();
        _layoutService = App.Services.GetRequiredService<LayoutService>();

        DataContext = this;

        Rows.CollectionChanged += OnRowsCollectionChanged;
        Loaded += (_, _) => ProfileGrid.Focus();
        PreviewKeyDown += OnPreviewKeyDown;

        LoadFromConfig();
    }

    /// <summary>
    /// 설정 파일과 레이아웃 목록을 다시 읽어 편집 상태를 초기화합니다.
    /// </summary>
    private void LoadFromConfig()
    {
        AvailableLayouts.Clear();
        foreach (var layout in _layoutService.GetAvailableLayouts().OrderBy(x => x))
        {
            AvailableLayouts.Add(layout);
        }

        Rows.Clear();
        foreach (var pair in _configService.Current.Profiles.OrderBy(p => p.Key))
        {
            var row = CreateRow(pair.Key, pair.Value);
            Rows.Add(row);
        }

        if (Rows.Count == 0)
        {
            Rows.Add(CreateRow(string.Empty, string.Empty));
        }

        UpdateRowStatuses();
    }

    private ProfileMappingEditorRow CreateRow(string processName, string layoutName)
    {
        var row = new ProfileMappingEditorRow(processName, layoutName);
        row.PropertyChanged += OnRowPropertyChanged;
        return row;
    }

    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<ProfileMappingEditorRow>())
            {
                oldItem.PropertyChanged -= OnRowPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<ProfileMappingEditorRow>())
            {
                newItem.PropertyChanged += OnRowPropertyChanged;
            }
        }
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateRowStatuses();
    }

    /// <summary>
    /// 저장 전에 사용자가 즉시 문제를 알 수 있도록 행별 상태를 계산합니다.
    /// </summary>
    private void UpdateRowStatuses()
    {
        var duplicated = Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ProcessName))
            .GroupBy(r => r.ProcessName.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var emptyProcessCount = 0;
        var emptyLayoutCount = 0;
        var unknownLayoutCount = 0;
        var duplicateCount = 0;

        foreach (var row in Rows)
        {
            var processName = row.ProcessName?.Trim() ?? string.Empty;
            var layoutName = row.LayoutName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(processName))
            {
                row.Status = "프로세스 이름 비어 있음";
                emptyProcessCount++;
                continue;
            }

            if (duplicated.Contains(processName.ToLowerInvariant()))
            {
                row.Status = "프로세스 이름 중복";
                duplicateCount++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(layoutName))
            {
                row.Status = "레이아웃 이름 비어 있음";
                emptyLayoutCount++;
                continue;
            }

            if (!AvailableLayouts.Contains(layoutName))
            {
                row.Status = "레이아웃 미존재";
                unknownLayoutCount++;
                continue;
            }

            row.Status = "정상";
        }

        ValidationSummaryText =
            $"총 {Rows.Count}행 | 빈 프로세스 {emptyProcessCount} | 빈 레이아웃 {emptyLayoutCount} | " +
            $"미존재 레이아웃 {unknownLayoutCount} | 중복 프로세스 {duplicateCount}";
    }

    private void OnAddRow(object sender, RoutedEventArgs e)
    {
        Rows.Add(CreateRow(string.Empty, string.Empty));
        UpdateRowStatuses();
    }

    private void OnRemoveRow(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProfileMappingEditorRow row })
        {
            return;
        }

        Rows.Remove(row);
        if (Rows.Count == 0)
        {
            Rows.Add(CreateRow(string.Empty, string.Empty));
        }

        UpdateRowStatuses();
    }

    /// <summary>
    /// 유효한 행만 설정 파일에 저장하고 메인 앱에 재로드 신호를 보냅니다.
    /// </summary>
    private void OnSave(object sender, RoutedEventArgs e)
    {
        var validRows = Rows
            .Where(r => string.Equals(r.Status, "정상", StringComparison.Ordinal))
            .Select(r => new
            {
                ProcessName = r.ProcessName.Trim().ToLowerInvariant(),
                LayoutName = r.LayoutName.Trim()
            })
            .ToList();

        _configService.Update(c =>
        {
            c.Profiles = validRows.ToDictionary(x => x.ProcessName, x => x.LayoutName);
        });

        // 프로필 매핑은 메인 앱 런타임에서 적용되므로 설정 파일 변경 후 즉시 재로드 신호를 보냅니다.
        ToolsReloadSignalService.NotifyReloadProfiles();

        MessageBox.Show(
            $"프로필 매핑 {validRows.Count}개를 저장했습니다.",
            "프로필 매핑 저장",
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

/// <summary>
/// [역할] 편집 가능한 프로필 매핑 한 줄을 표현합니다.
/// </summary>
public sealed class ProfileMappingEditorRow : INotifyPropertyChanged
{
    private string _processName;
    private string _layoutName;
    private string _status;

    public string ProcessName
    {
        get => _processName;
        set
        {
            if (_processName == value) return;
            _processName = value;
            OnPropertyChanged(nameof(ProcessName));
        }
    }

    public string LayoutName
    {
        get => _layoutName;
        set
        {
            if (_layoutName == value) return;
            _layoutName = value;
            OnPropertyChanged(nameof(LayoutName));
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public ProfileMappingEditorRow(string processName, string layoutName)
    {
        _processName = processName ?? string.Empty;
        _layoutName = layoutName ?? string.Empty;
        _status = "정상";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
