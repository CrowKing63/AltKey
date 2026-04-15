using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using AltKey.Models;
using Microsoft.Extensions.DependencyInjection;
using WpfApp = System.Windows.Application;
using WpfMsgBox = System.Windows.MessageBox;
using WpfMsgBoxButton = System.Windows.MessageBoxButton;
using WpfMsgBoxImage = System.Windows.MessageBoxImage;
using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService  _configService;
    private readonly LayoutService  _layoutService;
    private readonly ProfileService _profileService;

    // 표시명 → 파일명 매핑 (T-7.1: AvailableLayouts가 표시명을 저장)
    private readonly Dictionary<string, string> _displayToFileName = [];
    // SwitchLayout 재진입 방지 플래그
    private bool _isSwitching;

    public KeyboardViewModel       Keyboard    { get; }
    public SettingsViewModel       Settings    { get; }
    public EmojiViewModel          Emoji       { get; }
    public ClipboardViewModel      Clipboard   { get; }
    public SuggestionBarViewModel  AutoComplete { get; }

    [ObservableProperty]
    private string currentLayoutName = "";

    [ObservableProperty]
    private ObservableCollection<string> availableLayouts = [];

    /// T-5.10: 설정 패널 표시 여부
    [ObservableProperty]
    private bool isSettingsOpen;

    // T-9.5: 업데이트 배너 바인딩용 속성
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    private string? updateVersion;

    [ObservableProperty]
    private string? updateUrl;

    [ObservableProperty]
    private string? updateInstallerUrl;

    /// 업데이트 배너 표시 여부 (BoolToVis 바인딩용)
    public bool HasUpdate => UpdateVersion is not null;

    /// T-9.5: 설치형 앱 자동 업데이트 관련 상태
    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private double downloadProgress;

    [ObservableProperty]
    private bool isInstalling;

    [ObservableProperty]
    private string? updateStatusMessage;

    /// T-5.1: 체류 클릭 활성화 (KeyButton 바인딩용)
    public bool DwellEnabled
    {
        get => _configService.Current.DwellEnabled;
        set
        {
            _configService.Current.DwellEnabled = value;
            OnPropertyChanged();
        }
    }

    /// T-5.1: 체류 클릭 시간 ms (KeyButton 바인딩용)
    public int DwellTimeMs
    {
        get => _configService.Current.DwellTimeMs;
        set
        {
            _configService.Current.DwellTimeMs = value;
            OnPropertyChanged();
        }
    }

    public MainViewModel(
        ConfigService          configService,
        LayoutService          layoutService,
        KeyboardViewModel      keyboardViewModel,
        ProfileService         profileService,
        SettingsViewModel      settingsViewModel,
        EmojiViewModel         emojiViewModel,
        ClipboardViewModel     clipboardViewModel,
        SuggestionBarViewModel suggestionBarViewModel)
    {
        _configService  = configService;
        _layoutService  = layoutService;
        _profileService = profileService;

        Keyboard     = keyboardViewModel;
        Settings     = settingsViewModel;
        Emoji        = emojiViewModel;
        Clipboard    = clipboardViewModel;
        AutoComplete = suggestionBarViewModel;

        // T-5.4: 포그라운드 앱 변경 → 자동 레이아웃 전환
        _profileService.ForegroundAppChanged += OnForegroundAppChanged;

        // 체류 클릭 설정 변경 시 UI에 알림
        _configService.ConfigChanged += OnConfigChanged;

        // 키 입력 시 이모지/클립보드 패널 자동 닫기
        Keyboard.KeyTapped += () =>
        {
            Emoji.IsVisible     = false;
            Clipboard.IsVisible = false;
        };
    }

    private void OnConfigChanged(string? propertyName)
    {
        // Dwell 관련 속성 변경 알림
        OnPropertyChanged(nameof(DwellEnabled));
        OnPropertyChanged(nameof(DwellTimeMs));
    }

    public Task InitializeAsync()
    {
        // T-7.1: 파일명 → 표시명 매핑 구성, AvailableLayouts에 표시명 저장
        _displayToFileName.Clear();
        var fileNames    = _layoutService.GetAvailableLayouts();
        var displayNames = new List<string>();
        foreach (var fn in fileNames)
        {
            var l = _layoutService.TryLoad(fn);
            var display = l?.Name ?? fn;
            _displayToFileName[display] = fn;
            displayNames.Add(display);
        }
        AvailableLayouts = new ObservableCollection<string>(displayNames);

        var defaultName = _configService.Current.DefaultLayout;
        SwitchLayout(defaultName);
        return Task.CompletedTask;
    }

    // T-7.1: 드롭다운 TwoWay 바인딩 → 선택 변경 시 레이아웃 전환
    partial void OnCurrentLayoutNameChanged(string value)
    {
        if (_isSwitching || string.IsNullOrEmpty(value)) return;
        SwitchLayout(value);
    }

    [RelayCommand]
    public void SwitchLayout(string name)
    {
        _isSwitching = true;
        try
        {
            // 표시명 → 파일명 해석 (파일명 직접 전달 시 폴백)
            var fileName = _displayToFileName.TryGetValue(name, out var fn) ? fn : name;

            // T-6.7: 레이아웃 로드 실패 시 에러 로그 + 폴백
            var layout = _layoutService.TryLoad(fileName, ex =>
            {
                App.LogError(ex);

                // 첫 번째로 사용 가능한 다른 레이아웃으로 폴백
                var fallbackDisplay = AvailableLayouts.FirstOrDefault(l => l != name);
                if (fallbackDisplay is not null
                    && _displayToFileName.TryGetValue(fallbackDisplay, out var fbFile))
                {
                    var fb = _layoutService.TryLoad(fbFile);
                    if (fb is not null)
                    {
                        Keyboard.LoadLayout(fb);
                        CurrentLayoutName = fb.Name;
                    }
                }

                WpfApp.Current.Dispatcher.BeginInvoke(() =>
                    WpfMsgBox.Show(
                        $"레이아웃 '{name}'을 불러오지 못했습니다.\n{ex.Message}\n\n기본 레이아웃으로 전환합니다.",
                        "레이아웃 오류",
                        WpfMsgBoxButton.OK,
                        WpfMsgBoxImage.Warning));
            });

            if (layout is null) return;
            Keyboard.LoadLayout(layout);
            CurrentLayoutName = layout.Name; // 표시명으로 저장 → ComboBox 일치
        }
        finally
        {
            _isSwitching = false;
        }
    }

    // T-9.5: 업데이트 배너 커맨드
    [RelayCommand]
    private void DismissUpdate()
    {
        UpdateVersion = null;
        UpdateUrl = null;
        UpdateInstallerUrl = null;
        UpdateStatusMessage = null;
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        if (!string.IsNullOrEmpty(UpdateUrl))
            Process.Start(new ProcessStartInfo(UpdateUrl) { UseShellExecute = true });
    }

    /// T-9.5: 설치형 앱 자동 다운로드 및 설치
    [RelayCommand]
    private async Task DownloadAndInstallUpdate()
    {
        if (string.IsNullOrEmpty(UpdateInstallerUrl) || string.IsNullOrEmpty(UpdateVersion))
            return;

        try
        {
            // 포터블 모드 체크
            if (PathResolver.IsPortable)
            {
                WpfMsgBox.Show(
                    "포터블 버전에서는 자동 업데이트를 지원하지 않습니다.\n수동으로 최신 버전을 다운로드해주세요.",
                    "포터블 버전",
                    WpfMsgBoxButton.OK,
                    WpfMsgBoxImage.Information);
                OpenReleasePage();
                return;
            }

            IsDownloading = true;
            DownloadProgress = 0;
            UpdateStatusMessage = $"버전 {UpdateVersion} 다운로드 중...";

            var downloadService = App.Services.GetRequiredService<DownloadService>();
            var tempDir = Path.GetTempPath();
            var installerFileName = $"AltKey-Setup-{UpdateVersion}.exe";
            var installerPath = Path.Combine(tempDir, installerFileName);

            var progress = new Progress<double>(p => DownloadProgress = p);

            await downloadService.DownloadAsync(
                UpdateInstallerUrl,
                installerPath,
                progress);

            IsDownloading = false;
            UpdateStatusMessage = "설치를 시작합니다...";

            // 설치 실행
            IsInstalling = true;
            var installerService = App.Services.GetRequiredService<InstallerService>();
            await installerService.RunInstallerAsync(installerPath);
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            IsInstalling = false;
            UpdateStatusMessage = "업데이트 실패";

            WpfMsgBox.Show(
                $"업데이트 중 오류가 발생했습니다:\n{ex.Message}\n\nGitHub 릴리즈 페이지에서 수동으로 다운로드해주세요.",
                "업데이트 오류",
                WpfMsgBoxButton.OK,
                WpfMsgBoxImage.Error);

            OpenReleasePage();
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        IsDownloading = false;
        DownloadProgress = 0;
        UpdateStatusMessage = "다운로드가 취소되었습니다.";
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    [RelayCommand]
    private void ToggleEmojiPanel() => Emoji.IsVisible = !Emoji.IsVisible;

    [RelayCommand]
    private void ToggleClipboardPanel() => Clipboard.IsVisible = !Clipboard.IsVisible;

    // T-5.4: 앱 프로필 자동 전환
    private void OnForegroundAppChanged(string processName)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            var config = _configService.Current;
            if (!config.AutoProfileSwitch) return;

            if (config.Profiles.TryGetValue(processName, out var layoutName))
            {
                try { SwitchLayout(layoutName); }
                catch { /* 프로필 전환 실패 — 무시 */ }
            }
        });
    }
}
