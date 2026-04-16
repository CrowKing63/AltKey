using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using AltKey.Services;
using WpfApp = System.Windows.Application;
using WpfMsgBox = System.Windows.MessageBox;
using WpfMsgBoxButton = System.Windows.MessageBoxButton;
using WpfMsgBoxImage = System.Windows.MessageBoxImage;
using WpfMsgBoxResult = System.Windows.MessageBoxResult;
using WpfDialog = Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

/// T-5.10 / T-5.12 / T-8 / T-9.5: 설정 패널 ViewModel
public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService        _configService;
    private readonly ThemeService         _themeService;
    private readonly LayoutService        _layoutService;
    private readonly HotkeyService        _hotkeyService;
    private readonly StartupService       _startupService;
    private readonly SoundService         _soundService;
    private readonly LayoutEditorViewModel _layoutEditorVm;
    private readonly UpdateService        _updateService;
    private readonly DownloadService      _downloadService;
    private readonly InstallerService     _installerService;

    private CancellationTokenSource?      _downloadCts;

    // ── Observable 속성 ─────────────────────────────────────────────────────

    [ObservableProperty] private string themeMode      = "system";
    [ObservableProperty] private bool   alwaysOnTop    = true;
    [ObservableProperty] private double opacityIdle    = 0.4;
    [ObservableProperty] private int    fadeDelaySec   = 5;
    [ObservableProperty] private bool   dwellEnabled   = false;
    [ObservableProperty] private int    dwellTimeMs    = 800;
    [ObservableProperty] private string selectedLayout = "";
    [ObservableProperty] private string globalHotkey   = "Ctrl+Alt+K";

    // T-8.1: 자동 실행
    [ObservableProperty] private bool runOnStartup;

    // T-8.2: 키 클릭 사운드
    [ObservableProperty] private bool soundEnabled;
    [ObservableProperty] private string soundFilePath = "";

    // T-8.4: 클립보드 패널
    [ObservableProperty] private bool clipboardPanelEnabled;

    // T-9.3: 영문 자동 완성
    [ObservableProperty] private bool autoCompleteEnabled;

    // T-9.5: 현재 버전 표시
    [ObservableProperty] private string currentVersion = "";

    // T-9.5: 업데이트 체크 및 설치 상태
    [ObservableProperty] private bool isCheckingUpdate;
    [ObservableProperty] private bool isDownloading;
    [ObservableProperty] private double downloadProgress;
    [ObservableProperty] private bool isInstalling;
    [ObservableProperty] private string updateStatusMessage = "";
    [ObservableProperty] private bool hasUpdateAvailable;
    [ObservableProperty] private string latestVersion = "";
    [ObservableProperty] private string updateInstallerUrl = "";
    [ObservableProperty] private string updateReleaseUrl = "";

    // T-8.5: 앱별 레이아웃 프로필
    [ObservableProperty]
    private ObservableCollection<ProfileEntry> profiles = [];

    [ObservableProperty]
    private ObservableCollection<string> availableLayouts = [];

    // 라디오 버튼 바인딩용 (Theme)
    public bool ThemeIsSystem { get => ThemeMode == "system"; set { if (value) ThemeMode = "system"; } }
    public bool ThemeIsLight  { get => ThemeMode == "Light";  set { if (value) ThemeMode = "Light";  } }
    public bool ThemeIsDark   { get => ThemeMode == "Dark";   set { if (value) ThemeMode = "Dark";   } }

    // ── 생성자 ──────────────────────────────────────────────────────────────

    public SettingsViewModel(
        ConfigService        configService,
        ThemeService         themeService,
        LayoutService        layoutService,
        HotkeyService        hotkeyService,
        StartupService       startupService,
        SoundService         soundService,
        LayoutEditorViewModel layoutEditorViewModel,
        UpdateService        updateService,
        DownloadService      downloadService,
        InstallerService     installerService)
    {
        _configService  = configService;
        _themeService   = themeService;
        _layoutService  = layoutService;
        _hotkeyService  = hotkeyService;
        _startupService = startupService;
        _soundService   = soundService;
        _layoutEditorVm = layoutEditorViewModel;
        _updateService  = updateService;
        _downloadService = downloadService;
        _installerService = installerService;

        // T-9.5: 현재 버전 초기화
        var asmVersion = Assembly.GetExecutingAssembly().GetName().Version;
        CurrentVersion = asmVersion?.ToString(3) ?? "0.1.0";

        LoadFromConfig();
    }

    private bool _isLoading;

    private void LoadFromConfig()
    {
        _isLoading = true;
        try
        {
            var c = _configService.Current;
            ThemeMode      = c.Theme;
            AlwaysOnTop    = c.AlwaysOnTop;
            OpacityIdle    = c.OpacityIdle;
            FadeDelaySec   = c.FadeDelayMs / 1000;
            DwellEnabled   = c.DwellEnabled;
            DwellTimeMs    = c.DwellTimeMs;
            SelectedLayout = c.DefaultLayout;
            GlobalHotkey   = c.GlobalHotkey;

            // T-8.1: 자동 실행
            RunOnStartup = _startupService.IsEnabled;

            // T-8.2: 사운드
            SoundEnabled = c.SoundEnabled;
            SoundFilePath = c.SoundFilePath ?? "";

            // T-8.4: 클립보드 패널
            ClipboardPanelEnabled = c.ClipboardPanelEnabled;

            // T-9.3: 자동 완성
            AutoCompleteEnabled = c.AutoCompleteEnabled;

            // T-8.5: 프로필
            Profiles = new ObservableCollection<ProfileEntry>(
                c.Profiles.Select(p => new ProfileEntry(p.Key, p.Value)));

            AvailableLayouts = new ObservableCollection<string>(
                _layoutService.GetAvailableLayouts());
        }
        finally
        {
            _isLoading = false;
        }

        // T-8.2: _isLoading 가드 해제 후 사운드 초기 구성 적용
        // (OnSoundEnabledChanged / OnSoundFilePathChanged 는 _isLoading=true 중 호출되므로 직접 호출)
        _soundService.Configure(SoundEnabled, string.IsNullOrEmpty(SoundFilePath) ? null : SoundFilePath);
    }

    // ── 속성 변경 시 자동 저장 ───────────────────────────────────────────────

    partial void OnThemeModeChanged(string value)
    {
        if (_isLoading) return;
        _themeService.Apply(value);
        _configService.Update(c => c.Theme = value);
        OnPropertyChanged(nameof(ThemeIsSystem));
        OnPropertyChanged(nameof(ThemeIsLight));
        OnPropertyChanged(nameof(ThemeIsDark));
    }

    partial void OnAlwaysOnTopChanged(bool value)
    {
        _configService.Update(c => c.AlwaysOnTop = value);
        if (WpfApp.Current.MainWindow is not null)
            WpfApp.Current.MainWindow.Topmost = value;
    }

    partial void OnOpacityIdleChanged(double value)
        => _configService.Update(c => c.OpacityIdle = value);

    partial void OnFadeDelaySecChanged(int value)
        => _configService.Update(c => c.FadeDelayMs = value * 1000);

    partial void OnDwellEnabledChanged(bool value)
        => _configService.Update(c => c.DwellEnabled = value);

    partial void OnDwellTimeMsChanged(int value)
        => _configService.Update(c => c.DwellTimeMs = value);

    partial void OnSelectedLayoutChanged(string value)
        => _configService.Update(c => c.DefaultLayout = value);

    partial void OnGlobalHotkeyChanged(string value)
    {
        _configService.Update(c => c.GlobalHotkey = value);
        if (WpfApp.Current.MainWindow is MainWindow mw)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(mw).Handle;
            if (hwnd != IntPtr.Zero)
                _hotkeyService.Reregister(hwnd, value);
        }
    }

    // ── T-8.1: 자동 실행 ────────────────────────────────────────────────────

    partial void OnRunOnStartupChanged(bool value)
    {
        if (_isLoading) return;
        if (value) _startupService.Enable();
        else        _startupService.Disable();
        _configService.Update(c => c.RunOnStartup = value);
    }

    // ── T-8.2: 키 클릭 사운드 ──────────────────────────────────────────────

    partial void OnSoundEnabledChanged(bool value)
    {
        if (_isLoading) return;
        _soundService.Configure(value, string.IsNullOrEmpty(SoundFilePath) ? null : SoundFilePath);
        _configService.Update(c => c.SoundEnabled = value);
    }

    partial void OnSoundFilePathChanged(string value)
    {
        if (_isLoading) return;
        _soundService.Configure(SoundEnabled, string.IsNullOrEmpty(value) ? null : value);
        _configService.Update(c => c.SoundFilePath = string.IsNullOrEmpty(value) ? null : value);
    }

    [RelayCommand]
    private void BrowseSoundFile()
    {
        var dlg = new WpfDialog.OpenFileDialog { Filter = "WAV 파일|*.wav|모든 파일|*.*" };
        if (dlg.ShowDialog() == true)
        {
            SoundFilePath = dlg.FileName;
            _soundService.Configure(SoundEnabled, SoundFilePath);
            _configService.Update(c => c.SoundFilePath = SoundFilePath);
        }
    }

    [RelayCommand]
    private void ResetSoundFile()
    {
        SoundFilePath = "";
        _soundService.Configure(SoundEnabled, null);
        _configService.Update(c => c.SoundFilePath = null);
    }

    // ── T-8.4: 클립보드 패널 ───────────────────────────────────────────────

    partial void OnClipboardPanelEnabledChanged(bool value)
        => _configService.Update(c => c.ClipboardPanelEnabled = value);

    // T-9.3: 자동 완성
    partial void OnAutoCompleteEnabledChanged(bool value)
        => _configService.Update(c => c.AutoCompleteEnabled = value);

    // ── T-8.5: 앱별 레이아웃 프로필 ────────────────────────────────────────

    [RelayCommand]
    private void AddProfile()
    {
        Profiles.Add(new ProfileEntry("", ""));
    }

    [RelayCommand]
    private void RemoveProfile(ProfileEntry entry)
    {
        Profiles.Remove(entry);
        SaveProfiles();
    }

    [RelayCommand]
    private void SaveProfiles()
    {
        _configService.Update(c =>
            c.Profiles = Profiles
                .Where(p => !string.IsNullOrWhiteSpace(p.ProcessName))
                .ToDictionary(p => p.ProcessName.ToLower(), p => p.LayoutName));
    }

    // ── T-9.4: 레이아웃 편집기 열기 ──────────────────────────────────────────

    [RelayCommand]
    private void OpenLayoutEditor()
    {
        var win = new AltKey.Views.LayoutEditorWindow(_layoutEditorVm)
        {
            Owner = WpfApp.Current.MainWindow
        };
        win.Show();
    }

    // ── T-5.12: 관리자 권한으로 재시작 ──────────────────────────────────────

    [RelayCommand]
    private void RestartAsAdmin()
    {
        var psi = new ProcessStartInfo
        {
            FileName       = Environment.ProcessPath,
            Verb           = "runas",
            UseShellExecute = true,
        };
        try
        {
            Process.Start(psi);
            WpfApp.Current.Dispatcher.Invoke(() =>
            {
                if (WpfApp.Current.MainWindow is MainWindow mw)
                    mw.IsShuttingDown = true;
                WpfApp.Current.Shutdown();
            });
        }
        catch (Win32Exception)
        {
            // 사용자가 UAC 취소
        }
    }

    // ── T-9.5: 업데이트 확인 및 자동 설치 ───────────────────────────────────

    /// <summary>GitHub에서 새 버전 확인</summary>
    [RelayCommand]
    private async Task CheckForUpdate()
    {
        IsCheckingUpdate = true;
        UpdateStatusMessage = "업데이트 확인 중...";
        HasUpdateAvailable = false;

        try
        {
            var (hasUpdate, version, url, installerUrl) = await _updateService.CheckAsync();

            if (string.IsNullOrEmpty(version))
            {
                UpdateStatusMessage = "업데이트 확인 실패 (네트워크 오류)";
                ShowUpdateMessage("업데이트를 확인할 수 없습니다.\n네트워크 연결을 확인해주세요.");
                return;
            }

            LatestVersion = version;
            UpdateReleaseUrl = url;
            UpdateInstallerUrl = installerUrl;

            if (hasUpdate)
            {
                HasUpdateAvailable = true;
                UpdateStatusMessage = $"새 버전 {version}이 있습니다!";
            }
            else
            {
                HasUpdateAvailable = false;
                UpdateStatusMessage = "최신 버전입니다";
                ShowUpdateMessage($"현재 최신 버전을 사용 중입니다.\n현재 버전: {CurrentVersion}");
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = "업데이트 확인 오류";
            ShowUpdateMessage($"업데이트 확인 중 오류:\n{ex.Message}");
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    /// <summary>설치형 앱 자동 업데이트 (다운로드 → 설치 → 재시작)</summary>
    [RelayCommand]
    private async Task DownloadAndInstallFromSettings()
    {
        if (string.IsNullOrEmpty(UpdateInstallerUrl))
        {
            ShowUpdateMessage("인스톨러 URL을 찾을 수 없습니다.\nGitHub 페이지에서 수동으로 다운로드해주세요.");
            OpenReleasePage();
            return;
        }

        if (PathResolver.IsPortable)
        {
            ShowUpdateMessage("포터블 버전에서는 자동 업데이트를 지원하지 않습니다.\nGitHub 페이지에서 최신 버전을 다운로드해주세요.");
            OpenReleasePage();
            return;
        }

        try
        {
            _downloadCts = new CancellationTokenSource();

            IsDownloading = true;
            DownloadProgress = 0;
            UpdateStatusMessage = $"{LatestVersion} 다운로드 중...";

            var tempDir = Path.GetTempPath();
            var installerFileName = $"AltKey-Setup-{LatestVersion}.exe";
            var installerPath = Path.Combine(tempDir, installerFileName);

            var progress = new Progress<double>(p => DownloadProgress = p);

            await _downloadService.DownloadAsync(
                UpdateInstallerUrl,
                installerPath,
                progress,
                _downloadCts.Token);

            IsDownloading = false;
            UpdateStatusMessage = "설치 준비 중...";

            // 설치 전 사용자 확인
            var result = WpfMsgBox.Show(
                $"AltKey {LatestVersion} 설치를 시작합니다.\n\n앱이 자동으로 종료되고, 설치 후 재시작됩니다.\n\n계속하시겠습니까?",
                "설치 확인",
                WpfMsgBoxButton.YesNo,
                WpfMsgBoxImage.Question);

            if (result != WpfMsgBoxResult.Yes)
            {
                UpdateStatusMessage = "설치가 취소되었습니다.";
                try { File.Delete(installerPath); } catch { }
                return;
            }

            // 설치 실행
            IsInstalling = true;

            // 1. 인스톨러 실행 (부모 프로세스가 죽기 전에 실행)
            _installerService.StartInstaller(installerPath, autoRestart: true);

            // 2. 앱 즉시 종료 (인스톨러가 재시작 Manager를 통해 앱을 닫을 수도 있지만, 명시적으로 종료)
            if (WpfApp.Current.MainWindow is MainWindow mw)
                mw.IsShuttingDown = true;

            WpfApp.Current.Dispatcher.Invoke(() => WpfApp.Current.Shutdown());
        }
        catch (OperationCanceledException)
        {
            UpdateStatusMessage = "다운로드 취소됨";
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            IsInstalling = false;
            UpdateStatusMessage = "업데이트 실패";

            ShowUpdateMessage($"업데이트 중 오류:\n{ex.Message}\n\nGitHub에서 수동으로 다운로드해주세요.");
            OpenReleasePage();
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        if (!string.IsNullOrEmpty(UpdateReleaseUrl))
            Process.Start(new ProcessStartInfo(UpdateReleaseUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void CancelDownload()
    {
        _downloadCts?.Cancel();
        IsDownloading = false;
    }

    private static void ShowUpdateMessage(string message)
    {
        WpfMsgBox.Show(message, "업데이트", WpfMsgBoxButton.OK, WpfMsgBoxImage.Information);
    }
}

/// T-8.5: 편집 가능한 프로필 엔트리
public partial class ProfileEntry : ObservableObject
{
    [ObservableProperty] private string processName = "";
    [ObservableProperty] private string layoutName = "";

    public ProfileEntry(string processName, string layoutName)
    {
        ProcessName = processName;
        LayoutName = layoutName;
    }
}
