using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AltKey.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AltKey.Tools;

/// <summary>
/// [역할] 문서의 태스크 5(프로필 매핑 편집 검토)를 실제 UI 점검 흐름으로 제공합니다.
/// [접근성] 첫 포커스 고정, 선형 탭 순서, Esc 닫기를 제공해 키보드/스크린리더 사용성을 유지합니다.
/// </summary>
public partial class ProfileMappingReviewWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _configService;
    private readonly LayoutService _layoutService;

    public ObservableCollection<ProfileMappingReviewEntry> Entries { get; } = [];

    private string _summaryText = string.Empty;
    public string SummaryText
    {
        get => _summaryText;
        private set
        {
            if (_summaryText == value) return;
            _summaryText = value;
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    private string _riskText = string.Empty;
    public string RiskText
    {
        get => _riskText;
        private set
        {
            if (_riskText == value) return;
            _riskText = value;
            OnPropertyChanged(nameof(RiskText));
        }
    }

    private string _decisionText = string.Empty;
    public string DecisionText
    {
        get => _decisionText;
        private set
        {
            if (_decisionText == value) return;
            _decisionText = value;
            OnPropertyChanged(nameof(DecisionText));
        }
    }

    public ProfileMappingReviewWindow()
    {
        InitializeComponent();

        _configService = App.Services.GetRequiredService<ConfigService>();
        _layoutService = App.Services.GetRequiredService<LayoutService>();

        DataContext = this;

        // 접근성: 창 진입 직후 데이터 표로 바로 이동되도록 첫 포커스를 고정합니다.
        Loaded += (_, _) => ReviewGrid.Focus();
        PreviewKeyDown += OnPreviewKeyDown;

        RefreshReview();
    }

    private void RefreshReview()
    {
        Entries.Clear();

        var config = _configService.Current;
        var availableLayouts = _layoutService.GetAvailableLayouts().ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        var emptyLayoutCount = 0;
        var unknownLayoutCount = 0;

        foreach (var pair in config.Profiles.OrderBy(p => p.Key))
        {
            var processName = pair.Key?.Trim() ?? string.Empty;
            var layoutName = pair.Value?.Trim() ?? string.Empty;
            var status = "정상";

            if (string.IsNullOrWhiteSpace(layoutName))
            {
                status = "레이아웃 비어 있음";
                emptyLayoutCount++;
            }
            else if (!availableLayouts.Contains(layoutName))
            {
                status = "레이아웃 미존재";
                unknownLayoutCount++;
            }

            Entries.Add(new ProfileMappingReviewEntry(processName, layoutName, status));
        }

        SummaryText = $"총 {Entries.Count}개 매핑, 사용 가능 레이아웃 {availableLayouts.Count}개";
        RiskText = $"비어 있는 레이아웃 {emptyLayoutCount}개, 미존재 레이아웃 {unknownLayoutCount}개";

        // 태스크 5.2/5.3 판단: 데이터 구조는 단순(문자열->문자열)이라 도구 분리 적합,
        // 실제 반영은 메인 런타임(ProfileService + MainViewModel)이 담당하므로 2단계 후보로 유지합니다.
        DecisionText =
            "판단: 프로필 매핑은 별도 편집 도구로 분리하기 적합하지만, 적용/전환 로직은 메인 앱 런타임에 남겨야 합니다. " +
            "따라서 1단계에서는 검토 결과를 유지하고, 2단계 후보로 진행하는 것이 안전합니다.";
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
/// [역할] 프로필 매핑 한 줄을 접근성 표로 노출하기 위한 표시 모델입니다.
/// </summary>
public sealed record ProfileMappingReviewEntry(string ProcessName, string LayoutName, string Status);
