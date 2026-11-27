using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AltKey.Services;
using AltKey.ViewModels;
using AltKey.Views;

namespace AltKey;

public partial class MainWindow : Window
{
    private readonly WindowService  _windowService;
    private readonly ConfigService  _configService;
    private readonly TrayService    _trayService;
    private readonly MainViewModel  _viewModel;
    private readonly InputService   _inputService;
    private readonly AnchoredPanelService _anchoredPanels;

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
        MainViewModel  viewModel,
        InputService   inputService,
        AnchoredPanelService anchoredPanels)
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
        _viewModel     = viewModel;
        _inputService  = inputService;
        _configService.ConfigChanged += OnConfigChanged;

        // 키보드 창에 앵커되는 패널(빠른 메모·클립보드): 이 창이 배치 기준이 되므로 이동·크기·숨김/보임을 연결합니다.
        _anchoredPanels = anchoredPanels;
        _anchoredPanels.AttachHost(this);

        // 키보드 창 전체에서 일어나는 모든 누름(헤더 버튼·가상 키 포함)을 활동으로 보고
        // 유휴 타이머를 리셋합니다 — 누름 경로 자체가 타이머를 초기화하지 않기 때문에,
        // 창 표시 직후 외부에서 타이핑하다가 메모 버튼을 누르기 직전까지의 흐려진 상태가
        // 패널 열림 순간에 그대로 복사되지 않도록 합니다.
        AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new System.Windows.Input.MouseButtonEventHandler((_, _) => ResetIdleFade()), handledEventsToo: true);
        AddHandler(UIElement.PreviewMouseRightButtonDownEvent, new System.Windows.Input.MouseButtonEventHandler((_, _) => ResetIdleFade()), handledEventsToo: true);

        // T-1.7: 자동 페이딩 타이머 — 생성자에서 먼저 만들어두면
        // Show() 중 IsVisibleChanged가 OnSourceInitialized보다 앞서 떠도 null이 아니어서 NRE를 피한다.
        SetupFadeTimer();

        // T-5.5: 트레이 초기화
        _trayService.Initialize(this);

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            PlayOpenAnimation();
        };

        // 창이 실제로 보일 때만 유휴 카운트를 돌립니다.
        // Hide()/Show()는 WindowState를 바꾸지 않으므로, 가시성 변화로 추적합니다.
        // 처음 표시될 때도 IsVisible이 false→true로 바뀌어 카운트가 시작됩니다.
        IsVisibleChanged += (_, _) =>
        {
            if (_fadeTimer is null) return; // Show() 중 타이머가 아직 없으면 건너뜀
            if (IsVisible)
            {
                _isIdleOpacityApplied = false;
                ApplyOpacityForCurrentState(animated: false);
                StartIdleTimerIfNeeded();
            }
            else
            {
                _fadeTimer.Stop();
            }
        };

        // 최소화에서 복원될 때 투명도를 현재 규칙으로 다시 그립니다.
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

        // T-1.7: 자동 페이딩 — 타이머는 생성자에서 이미 만들어졌으므로
        // 여기서 Interval만 갱신(설정값 기준)하고 초기 투명도를 그린다.
        _fadeTimer.Interval = TimeSpan.FromMilliseconds(_configService.Current.FadeDelayMs);
        ApplyOpacityForCurrentState(animated: false);

        MouseEnter += MainWindow_MouseEnter;
        MouseLeave += MainWindow_MouseLeave;
    }

    // Esc 키도 제목 표시줄 닫기와 같은 확인 절차를 거쳐 트레이 숨김으로 이어집니다.
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
            Close();
        base.OnKeyDown(e);
    }

    // T-1.6 / T-5.6: 창 닫기 처리
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!IsShuttingDown)
        {
            if (_configService.Current.AskBeforeHideToTray)
            {
                var confirmWindow = new CloseToTrayConfirmWindow
                {
                    Owner = this
                };

                var shouldHideToTray = confirmWindow.ShowDialog() == true;
                if (!shouldHideToTray)
                {
                    e.Cancel = true;
                    return;
                }

                // "다시 묻지 않기"는 실제로 숨기기를 확정했을 때만 저장해 실수로 옵션이 바뀌지 않게 합니다.
                if (confirmWindow.DontAskAgain)
                {
                    _configService.Update(c => c.AskBeforeHideToTray = false, nameof(Models.AppConfig.AskBeforeHideToTray));
                }
            }

            e.Cancel = true;
            HideToTray();
            return;
        }

        _inputService.ReleaseAllHeldKeys("MainWindow.OnClosing");
        _inputService.ReleaseAllModifiers("MainWindow.OnClosing");

        if (!ResetPending)
        {
            double persistedTop = KeyboardViewControl?.GetPersistedTopForExpandedLaunch() ?? Top;
            _configService.Update(c =>
            {
                c.Window.Left   = Left;
                c.Window.Top    = persistedTop;
            });
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// 창을 실제로 트레이로 숨기는 공통 경로입니다.
    /// 닫기 버튼, Esc, 이후 다른 닫기 요청도 모두 같은 안전 정리 절차를 타게 합니다.
    /// </summary>
    private void HideToTray()
    {
        ModifierSafety.PrepareForWindowHide(_inputService, "MainWindow.HideToTray");
        Hide();

        // 첫 번째 숨김 시에만 풍선 알림을 보여 줘 복귀 경로를 한 번 알려 줍니다.
        if (_trayNotified)
        {
            return;
        }

        _trayService.ShowBalloon("AltKey가 트레이에서 실행 중입니다.");
        _trayNotified = true;
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

    /// <summary>
    /// 이 창(키보드 창)의 투명도 목표가 적용된 뒤 일어납니다 (목표값, 애니메이션 시간 ms — 0이면 즉시 적용).
    /// 스크래치패드 창이 같은 값으로 자신도 동기화하는 데 사용합니다 (2026-11-15 완전 연동).
    /// </summary>
    internal event Action<double, int>? OpacityStateApplied;

    /// <summary>
    /// 패널 열림 시 호스트의 목표 투명도로 패널도 함께 진행시킵니다.
    /// 호스트가 유휴/진입/복원 애니메이션 중이면 <c>Opacity</c>는 중간값이 읽히므로
    /// 패널이 중간값에서 멈춰 뜨는 것을 막기 위해 목표값으로 직접 진행시킵니다.
    /// 같은 150ms 페이드 속도를 사용해 호스트와 동기화합니다.
    /// </summary>
    internal void SyncPanelOpacity(System.Windows.Window panel)
    {
        var target = GetCurrentTargetOpacity();
        panel.BeginAnimation(
            System.Windows.Window.OpacityProperty,
            new DoubleAnimation
            {
                From = panel.Opacity,
                To = target,
                Duration = TimeSpan.FromMilliseconds(150)
            });
    }

    /// <summary>마우스가 이 창(또는 연동된 스크래치패드)에 들어온 것 같을 때 — 유휴 타이머를 초기화하고 기본 투명도로 되돌립니다.</summary>
    public void ResetIdleFade()
    {
        _isIdleOpacityApplied = false;
        _fadeTimer.Stop();
        ApplyOpacityForCurrentState();
    }

    /// <summary>마우스가 떠난 것 같을 때 — 유휴 페이딩 타이머를 (켜져 있으면) 다시 시작하고 현재 상태의 투명도를 적용합니다.</summary>
    public void ResumeIdleFade()
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

    private void MainWindow_MouseEnter(object? sender, System.Windows.Input.MouseEventArgs e) => ResetIdleFade();

    private void MainWindow_MouseLeave(object? sender, System.Windows.Input.MouseEventArgs e) => ResumeIdleFade();

    /// <summary>
    /// 유휴 기능이 켜져 있으면 타이머를 (재)시작합니다.
    /// 창이 처음 표시되거나 트레이에서 다시 보일 때 '보이기 시작한 시점'부터 카운트를 시작하게 하는 공통 경로입니다.
    /// </summary>
    private void StartIdleTimerIfNeeded()
    {
        if (!WindowOpacityProfile.ShouldStartIdleTimer(_configService.Current))
        {
            _fadeTimer.Stop();
            return;
        }

        _fadeTimer.Stop();
        _fadeTimer.Start();
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
            OpacityStateApplied?.Invoke(targetOpacity, 0); // 스크래치패드도 같은 값으로 즉시 동기화
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
        OpacityStateApplied?.Invoke(targetOpacity, durationMs); // 스크래치패드가 같은 시간으로 함께 페이드
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
        OpacityStateApplied?.Invoke(GetCurrentTargetOpacity(), 280); // 열린 스크래치패드가 함께 페이드인
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

    /// <summary>포커스 대상이 메인 창 안의 KeyboardView(키·제안바 등) 아래인지 판별합니다.</summary>
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
