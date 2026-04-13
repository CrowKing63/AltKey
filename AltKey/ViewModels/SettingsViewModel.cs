using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using AltKey.Services;
using WpfApp = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AltKey.ViewModels;

/// T-5.10 / T-5.12: 설정 패널 ViewModel
public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService  _configService;
    private readonly ThemeService   _themeService;
    private readonly LayoutService  _layoutService;
    private readonly HotkeyService  _hotkeyService;

    // ── Observable 속성 ─────────────────────────────────────────────────────

    [ObservableProperty] private string themeMode      = "system";
    [ObservableProperty] private bool   alwaysOnTop    = true;
    [ObservableProperty] private double opacityIdle    = 0.4;
    [ObservableProperty] private int    fadeDelaySec   = 5;
    [ObservableProperty] private bool   dwellEnabled   = false;
    [ObservableProperty] private int    dwellTimeMs    = 800;
    [ObservableProperty] private string selectedLayout = "";
    [ObservableProperty] private string globalHotkey   = "Ctrl+Alt+K";

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
        HotkeyService hotkeyService)
    {
        _configService = configService;
        _themeService  = themeService;
        _layoutService = layoutService;
        _hotkeyService = hotkeyService;

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

            AvailableLayouts = new ObservableCollection<string>(
                _layoutService.GetAvailableLayouts());
        }
        finally
        {
            _isLoading = false;
        }
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
