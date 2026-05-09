using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AltKey.Services;
using AltKey.ViewModels;

namespace AltKey;

public partial class MainWindow : Window
{
    private readonly WindowService  _windowService;
    private readonly ConfigService  _configService;
    private readonly TrayService    _trayService;
    private readonly HotkeyService  _hotkeyService;
    private readonly MainViewModel  _viewModel;
    private readonly InputService   _inputService;

    private DispatcherTimer _fadeTimer = null!;
    private bool _isIdleOpacityApplied;

    /// T-5.6: 실제 종료(Shutdown) 여부 플래그
    public bool IsShuttingDown { get; set; }

    /// 창 레이아웃 초기화 후 종료 시 현재 창 설정 저장을 건너뛴다.
    public bool ResetPending { get; set; }

    /// T-5.6: 트레이 풍선 알림이 이미 표시됐는지
    private bool _trayNotified;

    public MainWindow(
        WindowService  windowService,
        ConfigService  configService,
        TrayService    trayService,
        HotkeyService  hotkeyService,
        MainViewModel  viewModel,
        InputService   inputService)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 설정·편집기 TextBox 등에서 가상 키를 누를 때, 클릭으로 키 포커스가 메인 키보드 쪽으로
        // 옮겨지면 IME 한글 조합이 끊깁니다. 넘어가려는 순간 되돌려 조합을 유지합니다.
        AddHandler(Keyboard.PreviewGotKeyboardFocusEvent,
            (KeyboardFocusChangedEventHandler)OnPreviewGotKeyboardFocus,
            handledEventsToo: true);

        _windowService = windowService;
        _configService = configService;
        _trayService   = trayService;
        _hotkeyService = hotkeyService;
        _viewModel     = viewModel;
        _inputService  = inputService;
        _configService.ConfigChanged += OnConfigChanged;

        // T-5.5: 트레이 초기화
        _trayService.Initialize(this);

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            PlayOpenAnimation();
        };

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Normal)
            {
                BeginAnimation(OpacityProperty, null);
                ApplyOpacityForCurrentState(animated: false);
            }
        };

    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // T-1.3: WS_EX_NOACTIVATE 적용
        _windowService.ApplyNoActivate(hwnd);

        // T-1.4: 배경 설정
        _windowService.ApplyBackground(this);

        // T-1.6: 창 위치/크기 복원
        RestoreWindowPosition();

        // T-1.7: 자동 페이딩 타이머
        SetupFadeTimer();
        ApplyOpacityForCurrentState(animated: false);

        MouseEnter += MainWindow_MouseEnter;
        MouseLeave += MainWindow_MouseLeave;

        // T-5.7: 전역 단축키 등록
        var (mods, vk) = HotkeyService.ParseHotkey(_configService.Current.GlobalHotkey);
        _hotkeyService.Register(hwnd, mods, vk);
        _hotkeyService.HotkeyPressed += () => _trayService.ToggleVisibility();
    }

    // Esc 키로 닫기 → 트레이로
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
            Close();
        base.OnKeyDown(e);
    }

    // T-1.6 / T-5.6: 창 닫기 처리
    protected override void OnClosing(CancelEventArgs e)
    {
        _inputService.ReleaseAllHeldKeys("MainWindow.OnClosing");
        _inputService.ReleaseAllModifiers("MainWindow.OnClosing");

        if (!IsShuttingDown)
        {
            // 트레이로 숨기기
            e.Cancel = true;
            Hide();

            // 첫 번째 숨김 시 풍선 알림 (한 번만)
            if (!_trayNotified)
            {
                _trayService.ShowBalloon("AltKey가 트레이에서 실행 중입니다.");
                _trayNotified = true;
            }
        }
        else
        {
            if (!ResetPending)
            {
                _configService.Update(c =>
                {
                    c.Window.Left   = Left;
                    c.Window.Top    = Top;
                });
            }
        }

        base.OnClosing(e);
    }

    // T-1.6: 창 위치 복원 (화면 경계 밖이면 중앙 하단으로 초기화)
    // 창 크기는 KeyboardView.ApplyScale()이 담당.
    private void RestoreWindowPosition()
    {
        var cfg = _configService.Current.Window;
        var scale = Math.Clamp(cfg.Scale, 60, 200) / 100.0;

        // 위치 계산용 예상 크기 (실제 창 크기는 ApplyScale()에서 설정)
        var expectedWidth  = 900 * scale;
        var expectedHeight = 320 * scale;

        var screen = System.Windows.SystemParameters.WorkArea;

        double left = cfg.Left;
        double top  = cfg.Top;

        // -1이거나 화면 밖이면 화면 중앙 하단으로 초기화
        bool offScreen = left < 0 || top < 0
            || left + expectedWidth  > screen.Right  + 200
            || top  + expectedHeight > screen.Bottom + 200
            || left < screen.Left - 200
            || top  < screen.Top  - 200;

        if (offScreen)
        {
            Left = screen.Left + (screen.Width  - expectedWidth)  / 2;
            Top  = screen.Top  + (screen.Height - expectedHeight) * 0.75;
        }
        else
        {
            Left = left;
            Top  = top;
        }
    }

    // T-1.7: 자동 페이딩
    private void SetupFadeTimer()
    {
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_configService.Current.FadeDelayMs)
        };
        _fadeTimer.Tick += FadeTimer_Tick;
    }

    private void MainWindow_MouseEnter(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        _isIdleOpacityApplied = false;
        _fadeTimer.Stop();
        ApplyOpacityForCurrentState();
    }

    private void MainWindow_MouseLeave(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        _isIdleOpacityApplied = false;

        if (WindowOpacityProfile.ShouldStartIdleTimer(_configService.Current))
        {
            _fadeTimer.Stop();
            _fadeTimer.Start();
        }
        else
        {
            _fadeTimer.Stop();
        }

        ApplyOpacityForCurrentState();
    }

    private void FadeTimer_Tick(object? s, EventArgs e)
    {
        _fadeTimer.Stop();
        _isIdleOpacityApplied = true;
        ApplyOpacityForCurrentState(durationMs: 400);
    }

    /// <summary>
    /// [접근성] 투명도 적용 규칙을 한곳에 모아 설정 변경과 마우스 이동이 겹쳐도 같은 기준으로 보이게 합니다.
    /// 상시 투명도는 '유휴가 아닐 때의 기본값', 유휴 투명도는 '이탈 후 일정 시간이 지난 뒤의 값'으로 해석합니다.
    /// </summary>
    private void ApplyOpacityForCurrentState(bool animated = true, int durationMs = 150)
    {
        var targetOpacity = GetCurrentTargetOpacity();

        if (!animated)
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = targetOpacity;
            return;
        }

        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation
            {
                From = Opacity,
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(durationMs)
            });
    }

    private double GetCurrentTargetOpacity()
    {
        var config = _configService.Current;
        return _isIdleOpacityApplied
            ? WindowOpacityProfile.GetIdleOpacity(config)
            : WindowOpacityProfile.GetBaseOpacity(config);
    }

    private void OnConfigChanged(string? propertyName)
    {
        if (propertyName is not null
            and not nameof(Models.AppConfig.ActiveOpacityEnabled)
            and not nameof(Models.AppConfig.OpacityActive)
            and not nameof(Models.AppConfig.IdleOpacityEnabled)
            and not nameof(Models.AppConfig.OpacityIdle)
            and not nameof(Models.AppConfig.FadeDelayMs))
        {
            return;
        }

        _fadeTimer.Interval = TimeSpan.FromMilliseconds(_configService.Current.FadeDelayMs);

        if (!WindowOpacityProfile.ShouldStartIdleTimer(_configService.Current))
        {
            _fadeTimer.Stop();
            _isIdleOpacityApplied = false;
        }

        // 유휴 상태에서 수치를 바꿔도 앱 재시작 없이 즉시 현재 규칙으로 다시 그립니다.
        ApplyOpacityForCurrentState();
    }

    // T-4.9: 진입 애니메이션 (Window는 RenderTransform 미지원 → 페이드인만)
    private void PlayOpenAnimation()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 0;
        BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, GetCurrentTargetOpacity(), new Duration(TimeSpan.FromMilliseconds(280))));
    }

    private void OnPreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        IInputElement? restoreTarget = e.OldFocus switch
        {
            System.Windows.Controls.Primitives.TextBoxBase tb => tb,
            System.Windows.Controls.PasswordBox pb => pb,
            _ => null
        };
        if (restoreTarget is not UIElement oldEl || !oldEl.IsVisible) return;

        var prevWin = Window.GetWindow(oldEl);
        if (prevWin is null || ReferenceEquals(prevWin, this)) return;

        if (e.NewFocus is not DependencyObject newFocus) return;
        if (!IsWithinKeyboardViewSurface(newFocus)) return;

        e.Handled = true;
        Keyboard.Focus(restoreTarget);
    }

    /// <summary>포커스 대상이 메인 창 안의 KeyboardView(키·제안바·이모지 등) 아래인지 판별합니다.</summary>
    private bool IsWithinKeyboardViewSurface(DependencyObject? d)
    {
        if (KeyboardViewControl is null || d is null) return false;
        for (; d is not null; d = VisualTreeHelper.GetParent(d))
        {
            if (ReferenceEquals(d, KeyboardViewControl)) return true;
        }
        return false;
    }
}
