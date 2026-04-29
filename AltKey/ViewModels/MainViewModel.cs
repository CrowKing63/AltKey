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

/// <summary>
/// [역할] AltKey의 '심장'과 같은 역할을 하는 메인 뷰모델입니다.
/// [기능] 키보드, 설정, 이모지, 클립보드 등 모든 하위 모듈을 하나로 묶어 관리하며, 레이아웃 전환이나 자동 업데이트 같은 앱의 전반적인 동작을 제어합니다.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ConfigService  _configService; // 앱 설정을 담당하는 서비스
    private readonly LayoutService  _layoutService;
    private readonly ProfileService _profileService;
    private readonly AutoCompleteService _autoCompleteService;
    private readonly InputService _inputService;
    private readonly LiveRegionService _liveRegion;

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

    // T-9.5: 업데이트 인디케이터 바인딩용 속성
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    [NotifyPropertyChangedFor(nameof(UpdateIndicatorTooltip))]
    private string? updateVersion;

    [ObservableProperty]
    private string? updateUrl;

    [ObservableProperty]
    private string? updateInstallerUrl;

    /// 업데이트 인디케이터 표시 여부 (BoolToVis 바인딩용)
    public bool HasUpdate => UpdateVersion is not null;

    /// 업데이트 인디케이터 툴팁 텍스트
    public string UpdateIndicatorTooltip => IsDownloading
        ? $"다운로드 중... {DownloadProgress:P0}"
        : $"새 버전 {UpdateVersion} — 클릭하여 설치";

    /// T-9.5: 설치형 앱 자동 업데이트 관련 상태
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateIndicatorTooltip))]
    private bool isDownloading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateIndicatorTooltip))]
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

    public bool AutoCompleteEnabled
    {
        get => _configService.Current.AutoCompleteEnabled;
        set
        {
            if (_configService.Current.AutoCompleteEnabled == value) return;

            var target = value ? InputMode.Unicode : InputMode.VirtualKey;
            bool ok = _inputService.TrySetMode(target);

            if (!ok && value)
            {
                System.Media.SystemSounds.Beep.Play();
                _liveRegion.Announce("자동완성 켜기 실패");
                OnPropertyChanged(nameof(AutoCompleteEnabled));
                return;
            }

            _configService.Update(c => c.AutoCompleteEnabled = value);
            OnPropertyChanged();

            _autoCompleteService.ResetState();
            AutoComplete.IsVisible = _configService.Current.AutoCompleteEnabled;

            _liveRegion.Announce(value ? "자동완성 켜짐" : "자동완성 꺼짐");
        }
    }

    public bool CanToggleAutoComplete => !_inputService.IsElevated;

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

    /// T-10: 키 반복 입력 활성화 (KeyButton 바인딩용)
    public bool KeyRepeatEnabled
    {
        get => _configService.Current.KeyRepeatEnabled;
        set
        {
            _configService.Current.KeyRepeatEnabled = value;
            OnPropertyChanged();
        }
    }

    /// T-10: 키 반복 입력 초기 지연 ms (KeyButton 바인딩용)
    public int KeyRepeatDelayMs
    {
        get => _configService.Current.KeyRepeatDelayMs;
        set
        {
            _configService.Current.KeyRepeatDelayMs = value;
            OnPropertyChanged();
        }
    }

    /// T-10: 키 반복 입력 간격 ms (KeyButton 바인딩용)
    public int KeyRepeatIntervalMs
    {
        get => _configService.Current.KeyRepeatIntervalMs;
        set
        {
            _configService.Current.KeyRepeatIntervalMs = value;
            OnPropertyChanged();
        }
    }

    /// L1: 포커스 가시화 + 탭 탐색 모드 (KeyButton 바인딩용)
    public bool KeyboardA11yNavigationEnabled
    {
        get => _configService.Current.KeyboardA11yNavigationEnabled;
        set
        {
            _configService.Current.KeyboardA11yNavigationEnabled = value;
            OnPropertyChanged();
        }
    }

    /// L2: 애니메이션 최소화 모드 (KeyButton 바인딩용)
    public bool ReducedMotionEnabled
    {
        get => _configService.Current.ReducedMotionEnabled;
        set
        {
            _configService.Current.ReducedMotionEnabled = value;
            OnPropertyChanged();
        }
    }

    /// L2: TTS 마우스 오버 읽기 (KeyButton 바인딩용)
    public bool TtsOnHover
    {
        get => _configService.Current.TtsOnHover;
        set
        {
            _configService.Current.TtsOnHover = value;
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
        SuggestionBarViewModel suggestionBarViewModel,
        AutoCompleteService    autoCompleteService,
        InputService           inputService,
        LiveRegionService      liveRegion)
    {
        _configService  = configService;
        _layoutService  = layoutService;
        _profileService = profileService;
        _liveRegion = liveRegion;

        Keyboard     = keyboardViewModel;
        Settings     = settingsViewModel;
        Emoji        = emojiViewModel;
        Clipboard    = clipboardViewModel;
        AutoComplete = suggestionBarViewModel;
        _autoCompleteService = autoCompleteService;
        _inputService = inputService;

        // T-5.4: 포그라운드 앱 변경 → 자동 레이아웃 전환
        _profileService.ForegroundAppChanged += OnForegroundAppChanged;

        // 체류 클릭 설정 변경 시 UI에 알림
        _configService.ConfigChanged += OnConfigChanged;

        // 레이아웃 변경 시 AvailableLayouts 새로고침
        _layoutService.LayoutsChanged += OnLayoutsChanged;

        // 키 입력 시 이모지/클립보드 패널 자동 닫기
        Keyboard.KeyTapped += () =>
        {
            Emoji.IsVisible     = false;
            Clipboard.IsVisible = false;
        };
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName == "DefaultLayout")
        {
            SwitchLayout(_configService.Current.DefaultLayout);
            return;
        }

        OnPropertyChanged(nameof(DwellEnabled));
        OnPropertyChanged(nameof(DwellTimeMs));
        OnPropertyChanged(nameof(AutoCompleteEnabled));
        OnPropertyChanged(nameof(KeyRepeatEnabled));
        OnPropertyChanged(nameof(KeyRepeatDelayMs));
        OnPropertyChanged(nameof(KeyRepeatIntervalMs));
        OnPropertyChanged(nameof(KeyboardA11yNavigationEnabled));
        OnPropertyChanged(nameof(ReducedMotionEnabled));
        OnPropertyChanged(nameof(TtsOnHover));
    }

    private void OnLayoutsChanged()
    {
        _layoutService.InvalidateCache();
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

        // 현재 레이아웃 다시 로드
        if (!string.IsNullOrEmpty(CurrentLayoutName))
            SwitchLayout(CurrentLayoutName);
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
        if (!fileNames.Contains(defaultName) && fileNames.Count > 0)
        {
            var fallback = fileNames[0];
            System.Diagnostics.Debug.WriteLine($"기본 레이아웃 '{defaultName}'이(가) 존재하지 않아 '{fallback}'(으)로 폴백합니다.");
            defaultName = fallback;
            _configService.Update(c => c.DefaultLayout = fallback, "DefaultLayout");
        }
        SwitchLayout(defaultName);

        // L3: 앱 시작 시 스위치 스캔 모드가 켜져 있으면 자동 시작
        if (_configService.Current.SwitchScanEnabled)
            Keyboard.StartScan();

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
            CurrentLayoutName = layout.Name;

            // 자동 완성 언어 모드 설정: 레이아웃 language가 "ko"면 한국어 모드
            _autoCompleteService.ResetState();
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
            // 접근성: 업데이트 후 자동 재시작 앱이 관리자 권한으로 실행되지 않도록
            // 자동 업데이트 경로에서는 runas 강제 요청을 사용하지 않는다.
            await installerService.RunInstallerAsync(
                installerPath,
                autoRestart: true,
                requestElevation: false);
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
    private void ToggleEmojiPanel() => Emoji.IsVisible = !Emoji.IsVisible;

    [RelayCommand]
    private void ToggleClipboardPanel() => Clipboard.IsVisible = !Clipboard.IsVisible;

    [RelayCommand]
    private void SendOsImeHangul()
    {
        _inputService.SendKeyPress(VirtualKeyCode.VK_HANGUL);
        _liveRegion.Announce("OS IME 한영 전환 신호 전송됨");
    }

    [RelayCommand]
    private void SendOsk()
    {
        _inputService.SendCombo([VirtualKeyCode.VK_LWIN, VirtualKeyCode.VK_LCONTROL, VirtualKeyCode.VK_O]);
        _liveRegion.Announce("화면 키보드 호출");
    }

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
