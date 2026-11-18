using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using AltKey.Models;
using AltKey.Platform;
using AltKey.Views;
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
    private readonly OskLauncherService _oskLauncher;
    private readonly LiveRegionService _liveRegion;
    private readonly AiService _aiService;
    private readonly SoundService _soundService;
    private readonly ScratchPadViewModel _scratchPadVm; // 스크래치패드 메모 (U1)
    private readonly AnchoredPanelService _anchoredPanels; // 키보드 창에 앵커되는 패널(빠른 메모·클립보드) 호스트

    // 표시명 → 파일명 매핑 (T-7.1: AvailableLayouts가 표시명을 저장)
    private readonly Dictionary<string, string> _displayToFileName = [];
    // SwitchLayout 재진입 방지 플래그
    private bool _isSwitching;

    public KeyboardViewModel       Keyboard    { get; }
    public SettingsViewModel       Settings    { get; }
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

    /// <summary>
    /// [접근성] 현재 포커스 하이라이트를 탭 탐색/스위치 스캔 중 누가 소유하는지 표시용 상태입니다.
    /// </summary>
    public A11yFocusOwner A11yFocusOwner => Keyboard.A11yFocusOwner;

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
        ClipboardViewModel     clipboardViewModel,
        SuggestionBarViewModel suggestionBarViewModel,
        AutoCompleteService    autoCompleteService,
        InputService           inputService,
        OskLauncherService     oskLauncher,
        LiveRegionService      liveRegion,
        AiService              aiService,
        SoundService           soundService,
        ScratchPadViewModel    scratchPadViewModel,
        AnchoredPanelService   anchoredPanels)
    {
        _configService  = configService;
        _layoutService  = layoutService;
        _profileService = profileService;
        _liveRegion = liveRegion;
        _aiService = aiService;
        _soundService = soundService;
        _scratchPadVm = scratchPadViewModel;
        _anchoredPanels = anchoredPanels;

        Keyboard     = keyboardViewModel;
        Settings     = settingsViewModel;
        Clipboard    = clipboardViewModel;
        AutoComplete = suggestionBarViewModel;
        _autoCompleteService = autoCompleteService;
        _inputService = inputService;
        _oskLauncher = oskLauncher;

        // [스크래치패드] AltKey의 전역 입력 모드를 유지한다. VirtualKey 모드에서는 메모 TextBox의 OS IME가 조합을 맡는다.
        keyboardViewModel.ScratchPadForegroundBox = () =>
        {
            if (_scratchPadWindow is not { IsVisible: true } pad) return null;

            var fg = Win32.GetForegroundWindow();
            if (fg == IntPtr.Zero) return null;

            // "패드를 여고 AltKey 안에 있다"면 메모 박스가 입력 대상이다.
            // 키보드 창에서 버튼을 클릭하면 WPF가 같은 프로세스의 키보드 창을 활성화해 포그라운드가
            // 키보드(메인) 창으로 쏠리거나, 클립보드 패널 탭 시 그 창으로 쏠릴 수 있다 — 둘 다 "AltKey 내부"이므로
            // 패드로 라우팅한다. 외부 앱은 백그라운드에서 활성화가 차단되어 포그라운드가 그대로 유지되므로
            // 여전히 외부 경로로 간다. (2026-11: 클립보드 붙여넣기 시 복원 로직이 패드 포커스를 빼앗는 문제를
            //  대비해 판별 기준을 "메인/패드만"에서 "자기 프로세스 창이면 모두"로 넓혔다.)
            return AnchoredPanelWindowBase.IsOwnProcessWindow(fg) ? pad.InputBox : null;
        };

        // [스크래치패드] 전송 전 입력 대상(패드의 메모 박스)을 확보한다.
        // 패드가 열려 있을 때 키보드 창의 버튼을 클릭하면 WPF가 같은 프로세스의 키보드 창을 활성화해
        // 포그라운드를 뺏긴다(SetForegroundWindow는 우리 프로세스가 이미 포그라운드를 갖고 있으면 성공).
        // KeyPressed는 메모 박스 포커스가 끊겼을 때 스스로 복원하지만(KeyboardViewModel),
        // 제안 수락·클립보드 붙여넣기 경로는 그 로직이 없어 그대로 SendInput을 보내면
        // AltKey 자체로 가 사라진다 — 전송 전 패드 창/메모 박스 포커스를 다시 놓아 준다.
        // true 반환 = 패드가 실제로 포그라운드가 되었으니 호출 쪽은 외부 복원을 스킵한다.
        bool EnsureScratchPadInputTarget()
        {
            var padBox = keyboardViewModel.ScratchPadForegroundBox?.Invoke();
            if (padBox is null) return false; // 외부 앱이 입력 대상 — 포그라운드를 건드리지 않는다
            if (_scratchPadWindow is not { IsVisible: true } pad) return false;

            // WPF의 Activate()는 비동기라, 그대로 Ctrl+V를 보내면 활성화 완료 전 키가 엉뚱한 창에 간다.
            // 공용 기반 클래스의 EnsureForeground()가 실제로 포그라운드가 될 때까지 동기 검증한다.
            var padHwnd = new WindowInteropHelper(pad).Handle;
            if (!AnchoredPanelWindowBase.EnsureForeground(padHwnd))
            {
                DebugLog.Info("InputPrep pad foreground failed — falling back to external restore");
                return false; // 확보 실패 — 외부 복원 경로가 붙여넣기 대상을 잡는다
            }

            ScratchPadImePolicy.ConfigureTextBox(padBox, _inputService.Mode);
            if (!padBox.IsKeyboardFocused) System.Windows.Input.Keyboard.Focus(padBox);
            return true;
        }

        suggestionBarViewModel.PrepareInputTarget = EnsureScratchPadInputTarget;
        clipboardViewModel.PrepareInputTarget = EnsureScratchPadInputTarget;

        // ── 키보드 창에 앵커되는 패널 등록 (AnchoredPanelService) ────────────────
        // 생성·Show/Hide·배치·트레이 동기화·투명도 연동은 서비스가 소유 — 여기는 도메인 훅만 배선합니다.
        // order = 복수 패널 한 줄 배치 시 좌→우 순서 (클립보드가 왼쪽).
        _anchoredPanels.Register(
            "clipboard", order: 0,
            factory: () =>
            {
                var w = _clipboardPanelWindow ??= new ClipboardPanelWindow(Clipboard);
                w.OnCloseRequested += () => _anchoredPanels.Hide("clipboard"); // ✕·Esc → 숨김(종료 아님)
                Clipboard.RestoreInputTarget = w.RestoreInputForeground; // 붙여넣기 전 포커스 복원 훅 (클릭 대상)
                w.GetInputTargetToPreserveOnHide = () => _scratchPadWindow is { IsVisible: true } scratchPad
                    ? new WindowInteropHelper(scratchPad).Handle
                    : IntPtr.Zero;
                return w;
            },
            onBeforeShow: () =>
            {
                var scratchPadHwnd = _scratchPadWindow is { IsVisible: true } pad
                    ? new WindowInteropHelper(pad).Handle
                    : IntPtr.Zero;
                _clipboardPanelWindow?.CapturePreviousForeground(scratchPadHwnd);
            });

        _anchoredPanels.Register(
            "scratchpad", order: 1,
            factory: () =>
            {
                var w = _scratchPadWindow ??= new ScratchPadWindow(_scratchPadVm);
                w.OnCloseRequested += () => _anchoredPanels.Hide("scratchpad"); // ✕ → 숨김(종료 아님)
                return w;
            },
            onShow: () =>
            {
                if (_scratchPadWindow is not { } pad) return;

                // [열자마자 타이핑] 패드 창을 OS 포그라운드로 올린다. 메인 창은 ShowActivated="False"라
                // 버튼 클릭으로 포그라운드가 바뀌지 않고, ShowPanel도 공용이라 Activate()를 하지 않는다
                // (클립보드는 포커스 뺏김 방지). 그래서 여기에서 EnsureForeground로 동기 전환한다 —
                // 제안 수락·붙여넣기(EnsureScratchPadInputTarget)와 같은 기법. 패드가 포그라운드가 되어야
                // ScratchPadForegroundBox가 메모 박스를 입력 대상으로 인정해 타이핑이 패드로 간다.
                AnchoredPanelWindowBase.EnsureForeground(new WindowInteropHelper(pad).Handle);

                ScratchPadImePolicy.ConfigureTextBox(pad.InputBox, _inputService.Mode);
                pad.FocusTextBox();
            },
            onHide: () => _scratchPadVm.SaveNow()); // 500ms 디바운스 넘어 안 저장된 내용이 없도록 즉시 저장

        _profileService.ForegroundAppChanged += OnForegroundAppChanged;
        _configService.ConfigChanged += OnConfigChanged;
        _inputService.SpecialActionRequested += OnSpecialActionRequested;
        Keyboard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(KeyboardViewModel.A11yFocusOwner))
                OnPropertyChanged(nameof(A11yFocusOwner));
        };
        _layoutService.LayoutsChanged += OnLayoutsChanged;
        // [2026-11] 키 탭 시 클립보드 자동 숨김 제거 — 독립 창(앵커 패널)에서는 스크래치패드에 타이핑해도
        // 클립보드가 닫히면 안 되고, 화상키보드 화살표로 패널 내비게이션하려면 더더욱 닫히면 안 된다.
        // 닫기는 ✕/Esc/키보드 빈 공간 클릭(CloseAnchoredPanels)으로 충분하다.

        // 상단바 버튼 초기 구성
        RebuildHeaderButtons();
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName == "DefaultLayout")
        {
            SwitchLayout(_configService.Current.DefaultLayout);
            return;
        }
        if (propertyName == "HeaderButtons")
            RebuildHeaderButtons();

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
    private void ToggleClipboardPanel() => _anchoredPanels.Toggle("clipboard");

    // ── 키보드 창에 앵커되는 패널 (빠른 메모 + 클립보드) ────────────────────────
    // 창 생성·Show/Hide·배치·트레이 동기화·투명도 연동은 전부 AnchoredPanelService가 소유합니다.
    // 이 VM은 도메인 훅(포커스·저장·입력 대상 확보)만 배선하고, 아래 필드는 입력-대상 로직에서 창 참조용으로 씁니다.
    private ScratchPadWindow? _scratchPadWindow;         // 첫 열림 시 생성 (서비스 팩토리 경유)
    private ClipboardPanelWindow? _clipboardPanelWindow;  // 위와 동일

    [RelayCommand]
    private void ToggleScratchPad() => _anchoredPanels.Toggle("scratchpad");

    /// <summary>열려 있는 앵커 패널을 전부 닫습니다 (키보드 빈 공간 클릭).</summary>
    public void CloseAnchoredPanels()
    {
        _anchoredPanels.Hide("clipboard");
        _anchoredPanels.Hide("scratchpad");
    }

    [RelayCommand]
    private void SendOsImeHangul()
    {
        _inputService.SendKeyPress(VirtualKeyCode.VK_HANGUL);
        _liveRegion.Announce("OS IME 한영 전환 신호 전송됨");
    }

    [RelayCommand]
    private void SendOsk()
    {
        // 접근성 우선: 포커스를 옮길 수 없는 상황에서도 동작하도록 직접 실행을 먼저 시도합니다.
        if (!_oskLauncher.TryLaunch())
        {
            // 일부 환경에서는 직접 실행이 막힐 수 있어, 기존 단축키 경로를 최후 보조 수단으로 남겨 둡니다.
            _inputService.SendCombo([VirtualKeyCode.VK_LWIN, VirtualKeyCode.VK_LCONTROL, VirtualKeyCode.VK_O]);
        }

        _liveRegion.Announce("화면 키보드 호출");
    }

    // ── AI 텍스트 처리 ───────────────────────────────────────────

    /// AI 처리 진행 중 여부
    [ObservableProperty]
    private bool isAiProcessing;

    private CancellationTokenSource? _aiCts;

    // [왜] 선택 텍스트 캡처 재시도 최대 횟수 — 첫 시도가 타이밍에 안 맞으면(느린 앱/브라우저) 내부에서 같은 순서 반복.
    private const int CaptureMaxAttempts = 3;
    // [왜] UIA로 "포커스가 대상 앱 안에 잡혔는지" 확인 주기(ms) — 고정 80ms 대기를 결정적 대기 대신한다.
    private const int FocusWaitPollDelayMs = 40;
    // [왜] 위 주기 × 횟수 ≈ 1s. 느린 앱도 포커스 잡히도록, 무한 대기 방지.
    private const int FocusWaitPollCount = 25;

    /// <summary>
    /// 대상 창 내부 포커스가 실제로 대상 앱 안으로 들어온 것을 UIA로 확인한다.
    /// [왜] SetForegroundWindow만 보내고 고정 지연(80ms)을 기다리면, 느린 앱/브라우저에선 아직 내부 포커스가
    /// 안 잡힌 상태에서 Ctrl+C가 날아가 아무것도 복사 못 한다 — 이게 랜덤 실패의 원인이다.
    /// UIA로 "글로벌 포커스 요소가 대상 프로세스 안에 있는지"를 확인해 그 타이밍 불확실성을 제거한다.
    /// [폴백] UIA가 안 되면(타임아웃/예외) false를 돌려 호출부에서 기존처럼 진행 — 레그레스션 방지.
    /// </summary>
    private async Task<bool> WaitForFocusInTargetAsync(uint targetPid, CancellationToken ct)
    {
        try
        {
            for (int i = 0; i < FocusWaitPollCount; i++)
            {
                var foc = System.Windows.Automation.AutomationElement.FocusedElement;
                if (foc != null && foc.Current.ProcessId == targetPid)
                    return true;   // 포커스가 대상 앱 안에 → Ctrl+C가 올바른 창으로 감
                await Task.Delay(FocusWaitPollDelayMs, ct);
            }
        }
        catch { /* UIA 호출 실패 → 폴백 */ }
        return false;
    }

    /// <summary>
    /// AI 텍스트 처리: Ctrl+C → API → Ctrl+V
    /// </summary>
    /// <summary>
    /// 상단바 클릭 후 포그라운드가 AltKey로만 남아 있으면, 직전에 쓰던 앱으로 포커스를 돌려 Ctrl+C가 올바른 창으로 가게 합니다.
    /// </summary>
    private void TryFocusLastExternalTarget()
    {
        var fg = Win32.GetForegroundWindow();
        Win32.GetWindowThreadProcessId(fg, out var pid);
        if (pid != (uint)Environment.ProcessId) return;

        var target = _profileService.LastExternalForegroundHwnd;
        if (target == IntPtr.Zero || !Win32.IsWindow(target)) return;

        Win32.SetForegroundWindow(target);
    }

    [RelayCommand]
    private async Task ExecuteAi()
    {
        await ExecuteAiCoreAsync("");
    }

    private async Task ExecuteAiCoreAsync(string prompt)
    {
        if (IsAiProcessing) return;
        IsAiProcessing = true;
        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;
        string? originalClipboard = null;
        try
        {
            // 클릭 처리가 끝난 뒤 포커스 복원 → 클립보드 백업/복사 순서
            await Task.Yield();

            // Ctrl+C 전 클립보드를 한 번만 백업해 둔다 (finally에서 원래대로 돌림).
            await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                originalClipboard = ClipboardHelper.GetTextWithRetry());

            // 선택 텍스트 캡처: 포커스 복원 → Ctrl+C → 읽기.
            // [왜 재시도] 첫 시도가 타이밍에 안 맞으면(느린 앱/브라우저) 빈 값이 온다. 같은 순서를 반복하면
            // 수동으로 두세 번 누르는 효과를 내부에서 얻는다.
            var target = _profileService.LastExternalForegroundHwnd;
            Win32.GetWindowThreadProcessId(target, out var targetPid);

            string? selectedText = null;
            for (int attempt = 1; attempt <= CaptureMaxAttempts; attempt++)
            {
                TryFocusLastExternalTarget();
                // [왜] 고정 80ms 대신 UIA로 "포커스가 대상 앱에 잡혔는지" 결정적으로 확인. 안 되면 폴백으로 진행.
                if (target != IntPtr.Zero && Win32.IsWindow(target))
                    await WaitForFocusInTargetAsync(targetPid, ct);

                _inputService.SendCombo([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_C]);
                await Task.Delay(220, ct);

                selectedText = null;
                await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                    selectedText = ClipboardHelper.GetTextWithRetry());

                if (!string.IsNullOrWhiteSpace(selectedText))
                    break;   // 캡처 성공

                Debug.WriteLine($"[AI] 선택 텍스트 캡처 실패 (시도 {attempt}/{CaptureMaxAttempts})");
            }

            if (string.IsNullOrWhiteSpace(selectedText))
            { _liveRegion.Announce("선택된 텍스트를 읽을 수 없습니다. 다시 선택한 뒤 AI 버튼(번역)을 누르세요."); return; }

            _liveRegion.Announce("AI 처리 중...");
            var result = await _aiService.ProcessTextAsync(selectedText, prompt, ct);

            await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                ClipboardHelper.SetTextWithRetry(result));
            await Task.Delay(50, ct);
            _inputService.SendCombo([VirtualKeyCode.VK_CONTROL, VirtualKeyCode.VK_V]);
            // 붙여넣기 직후 키 사운드 한 번 재생해 결과가 입력됐음을 알려 줌.
            _soundService.Play();
            await Task.Delay(100, ct);
            _liveRegion.Announce("AI 처리 완료");
        }
        catch (AiServiceException ex) { _liveRegion.Announce($"AI 실패: {ex.Message}"); }
        catch (OperationCanceledException) { _liveRegion.Announce("AI 취소됨"); }
        catch (Exception ex) { _liveRegion.Announce("AI 오류"); Debug.WriteLine($"[AI] {ex}"); }
        finally
        {
            if (originalClipboard is not null)
            {
                try { await Task.Delay(200);
                    await WpfApp.Current.Dispatcher.InvokeAsync(() =>
                        ClipboardHelper.SetTextWithRetry(originalClipboard));
                } catch { }
            }
            IsAiProcessing = false; _aiCts?.Dispose(); _aiCts = null;
        }
    }

    [RelayCommand] private void CancelAi() => _aiCts?.Cancel();

    private void OnSpecialActionRequested(KeyAction action)
    {
        if (action is AiAction aiAction)
        {
            _ = ExecuteAiCoreAsync(aiAction.Prompt);
        }
    }

    // ── 상단바 동적 버튼 ─────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<HeaderButtonVm> headerButtonsLeft = [];
    [ObservableProperty] private ObservableCollection<HeaderButtonVm> headerButtonsRight = [];

    public void RebuildHeaderButtons()
    {
        var configs = _configService.Current.HeaderButtons;
        if (configs.Count == 0)
        {
            configs = HeaderButtonConfig.CreateDefaults();
            _configService.Current.HeaderButtons = configs;
            _configService.Save();
        }
        else
        {
            // 마이그레이션: 기존 사용자의 목록에는 스크래치패드 같은 나중에 생긴 기본 버튼이 없으므로
            // 누락된 BuiltIn만 목록 끝에 보정합니다 (커스텀 버튼·순서는 그대로 유지).
            var ensured = HeaderButtonConfig.EnsureAllBuiltIns(configs);
            if (ensured.Count != configs.Count)
            {
                _configService.Current.HeaderButtons = ensured;
                _configService.Save();
                configs = ensured;
            }
        }
        var left  = new ObservableCollection<HeaderButtonVm>();
        var right = new ObservableCollection<HeaderButtonVm>();
        foreach (var cfg in configs)
        {
            if (!cfg.Visible) continue;
            var vm = CreateHeaderButtonVm(cfg);
            if (vm is null) continue;
            if (cfg.Position == "Left") left.Add(vm); else right.Add(vm);
        }
        HeaderButtonsLeft  = left;
        HeaderButtonsRight = right;
    }

    private HeaderButtonVm? CreateHeaderButtonVm(HeaderButtonConfig config)
    {
        if (config.Kind == HeaderButtonKind.Custom)
        {
            return CreateCustomHeaderButtonVm(config);
        }

        return config.Id switch
        {
            HeaderButtonConfig.IdClipboard => CreateBuiltInHeaderButtonVm(config, ToggleClipboardPanelCommand),
            HeaderButtonConfig.IdAutoComplete => CreateBuiltInHeaderButtonVm(config, command: null, isToggle: true),
            HeaderButtonConfig.IdOsIme => CreateBuiltInHeaderButtonVm(config, SendOsImeHangulCommand, width: 40, fontSize: 10),
            HeaderButtonConfig.IdOsk => CreateBuiltInHeaderButtonVm(config, SendOskCommand),
            HeaderButtonConfig.IdSettings => CreateBuiltInHeaderButtonVm(config, Settings.OpenSettingsCommand),
            // AI ✨ 버튼은 상단바 설정의 Visible만으로 표시됩니다.
            HeaderButtonConfig.IdAi => CreateBuiltInHeaderButtonVm(config, ExecuteAiCommand),
            // 스크래치패드도 AI와 같은 패턴 — Visible 토글만 (창 생성·Show/Hide는 ToggleScratchPad가 관리)
            HeaderButtonConfig.IdScratchPad => CreateBuiltInHeaderButtonVm(config, ToggleScratchPadCommand),
            _ => null
        };
    }

    private HeaderButtonVm CreateBuiltInHeaderButtonVm(
        HeaderButtonConfig config,
        System.Windows.Input.ICommand? command,
        bool isToggle = false,
        double width = 32,
        double fontSize = 14)
    {
        return new HeaderButtonVm
        {
            Id = config.Id,
            Icon = HeaderButtonConfig.GetBuiltInIconText(config.Id),
            Label = HeaderButtonConfig.GetDisplayName(config.Id),
            ToolTip = HeaderButtonConfig.GetBuiltInTooltip(config.Id),
            AccessibleName = HeaderButtonConfig.GetBuiltInAccessibleName(config.Id),
            AccessibleHelp = BuildHeaderActionHelp(config.CustomAction, config.Id),
            Command = command,
            IsToggle = isToggle,
            Width = width,
            FontSize = fontSize
        };
    }

    private HeaderButtonVm? CreateCustomHeaderButtonVm(HeaderButtonConfig config)
    {
        if (config.CustomAction is null)
            return null;

        return new HeaderButtonVm
        {
            Id = config.Id,
            Icon = string.IsNullOrWhiteSpace(config.IconText) ? "새" : config.IconText,
            Label = HeaderButtonConfig.GetDisplayName(config.Id),
            ToolTip = string.IsNullOrWhiteSpace(config.Tooltip) ? "커스텀 상단바 단축키" : config.Tooltip,
            AccessibleName = string.IsNullOrWhiteSpace(config.AccessibleName) ? config.Tooltip : config.AccessibleName,
            AccessibleHelp = BuildHeaderActionHelp(config.CustomAction, null),
            Command = new RelayCommand(() => ExecuteHeaderButtonAction(config.CustomAction)),
            Width = 32,
            FontSize = 12
        };
    }

    private void ExecuteHeaderButtonAction(KeyAction? action)
    {
        if (action is null)
            return;

        if (action is ToggleKoreanSubmodeAction)
        {
            _autoCompleteService.ToggleKoreanSubmode();
            return;
        }

        _inputService.HandleAction(action);
    }

    private static string BuildHeaderActionHelp(KeyAction? action, string? builtInId)
    {
        if (builtInId is not null)
        {
            return builtInId switch
            {
                HeaderButtonConfig.IdClipboard => "클립보드 패널을 엽니다.",
                HeaderButtonConfig.IdAutoComplete => "자동완성을 켜거나 끕니다.",
                HeaderButtonConfig.IdOsIme => "OS IME에 한영 전환 신호를 보냅니다.",
                HeaderButtonConfig.IdOsk => "화면 키보드를 엽니다.",
                HeaderButtonConfig.IdSettings => "설정 창을 엽니다.",
                HeaderButtonConfig.IdAi => "선택한 텍스트를 AI로 가공합니다.",
                HeaderButtonConfig.IdScratchPad => "빠른 메모를 엽니다.",
                _ => ""
            };
        }

        return action switch
        {
            SendKeyAction sendKey => $"{sendKey.Vk} 키를 보냅니다.",
            SendComboAction sendCombo => $"{string.Join(" + ", sendCombo.Keys)} 조합키를 보냅니다.",
            ToggleStickyAction sticky => $"{sticky.Vk} 고정 상태를 전환합니다.",
            SwitchLayoutAction switchLayout => $"{switchLayout.Name} 레이아웃으로 전환합니다.",
            RunAppAction runApp => $"{runApp.Path} 프로그램을 실행합니다.",
            BoilerplateAction => "미리 지정한 텍스트를 입력합니다.",
            ShellCommandAction => "셸 명령을 실행합니다.",
            VolumeControlAction volume => $"볼륨을 {volume.Direction} 방향으로 조절합니다.",
            ClipboardPasteAction => "지정한 텍스트를 클립보드로 붙여넣습니다.",
            ToggleKoreanSubmodeAction => "AltKey 내부 한국어/영어 입력 모드를 전환합니다.",
            ToggleFunctionLayerAction => "Fn 레이어 상태를 전환합니다.",
            AiAction => "선택한 텍스트를 AI로 가공합니다.",
            _ => ""
        };
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
                catch { }
            }
        });
    }
}

/// 상단바 동적 버튼 VM
public class HeaderButtonVm : ObservableObject
{
    public string Id { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Label { get; init; } = "";
    public string ToolTip { get; init; } = "";
    public string AccessibleName { get; init; } = "";
    public string AccessibleHelp { get; init; } = "";
    public System.Windows.Input.ICommand? Command { get; init; }
    public bool IsToggle { get; init; }
    public double Width { get; init; } = 32;
    public double FontSize { get; init; } = 14;

    // AI(✨) 버튼 여부 — 처리 중 스피너 표시에 사용 (Id가 "Ai"일 때 true).
    // 계산 속성이므로 생성 시 별도로 설정할 필요가 없다.
    public bool IsAiButton => Id == HeaderButtonConfig.IdAi;
}
