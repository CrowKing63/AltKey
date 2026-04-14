using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AltKey.Services;
using WpfApp = System.Windows.Application;
using WpfDialog = Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

/// T-5.10 / T-5.12 / T-8: 설정 패널 ViewModel
public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService  _configService;
    private readonly ThemeService   _themeService;
    private readonly LayoutService  _layoutService;
    private readonly HotkeyService  _hotkeyService;
    private readonly StartupService _startupService;
    private readonly SoundService   _soundService;

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
        ConfigService configService,
        ThemeService  themeService,
        LayoutService layoutService,
        HotkeyService hotkeyService,
        StartupService startupService,
        SoundService soundService)
    {
        _configService = configService;
        _themeService  = themeService;
        _layoutService = layoutService;
        _hotkeyService = hotkeyService;
        _startupService = startupService;
        _soundService = soundService;

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
