using System.Collections.ObjectModel;
using System.IO;
using AltKey.Models;
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

    public KeyboardViewModel  Keyboard { get; }
    public SettingsViewModel  Settings { get; }

    [ObservableProperty]
    private string currentLayoutName = "";

    [ObservableProperty]
    private ObservableCollection<string> availableLayouts = [];

    /// T-5.10: 설정 패널 표시 여부
    [ObservableProperty]
    private bool isSettingsOpen;

    /// T-5.1: 체류 클릭 활성화 (KeyButton 바인딩용)
    public bool DwellEnabled => _configService.Current.DwellEnabled;

    /// T-5.1: 체류 클릭 시간 ms (KeyButton 바인딩용)
    public int DwellTimeMs => _configService.Current.DwellTimeMs;

    public MainViewModel(
        ConfigService    configService,
        LayoutService    layoutService,
        KeyboardViewModel keyboardViewModel,
        ProfileService   profileService,
        SettingsViewModel settingsViewModel)
    {
        _configService  = configService;
        _layoutService  = layoutService;
        _profileService = profileService;

        Keyboard = keyboardViewModel;
        Settings = settingsViewModel;

        // T-5.4: 포그라운드 앱 변경 → 자동 레이아웃 전환
        _profileService.ForegroundAppChanged += OnForegroundAppChanged;
    }

    public Task InitializeAsync()
    {
        AvailableLayouts = new ObservableCollection<string>(
            _layoutService.GetAvailableLayouts());

        var defaultName = _configService.Current.DefaultLayout;
        SwitchLayout(defaultName);
        return Task.CompletedTask;
    }

    [RelayCommand]
    public void SwitchLayout(string name)
    {
        // T-6.7: 레이아웃 로드 실패 시 에러 로그 + 폴백
        var layout = _layoutService.TryLoad(name, ex =>
        {
            App.LogError(ex);

            // 첫 번째로 사용 가능한 다른 레이아웃으로 폴백
            var fallback = AvailableLayouts.FirstOrDefault(l => l != name);
            if (fallback is not null)
            {
                var fb = _layoutService.TryLoad(fallback);
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
    }

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

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
