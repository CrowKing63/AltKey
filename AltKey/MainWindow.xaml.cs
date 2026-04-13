using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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

    private DispatcherTimer _fadeTimer = null!;

    /// T-5.6: 실제 종료(Shutdown) 여부 플래그
    public bool IsShuttingDown { get; set; }

    /// T-5.6: 트레이 풍선 알림이 이미 표시됐는지
    private bool _trayNotified;

    public MainWindow(
        WindowService  windowService,
        ConfigService  configService,
        TrayService    trayService,
        HotkeyService  hotkeyService,
        MainViewModel  viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        _windowService = windowService;
        _configService = configService;
        _trayService   = trayService;
        _hotkeyService = hotkeyService;
        _viewModel     = viewModel;

        // T-5.5: 트레이 초기화
        _trayService.Initialize(this);

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            PlayOpenAnimation();
        };

        SizeChanged += (_, e) =>
            _viewModel.Keyboard.OnWindowSizeChanged(e.NewSize.Width);
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
            // 실제 종료 — 설정 저장
            _configService.Update(c =>
            {
                c.Window.Left   = Left;
                c.Window.Top    = Top;
                c.Window.Width  = Width;
                c.Window.Height = Height;
            });
        }

        base.OnClosing(e);
    }

    // T-1.6: 창 위치/크기 복원
    private void RestoreWindowPosition()
    {
        var cfg = _configService.Current.Window;
        Left   = cfg.Left;
        Top    = cfg.Top;
        Width  = cfg.Width;
        Height = cfg.Height;
    }

    // T-1.7: 자동 페이딩
    private void SetupFadeTimer()
    {
        var config = _configService.Current;
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(config.FadeDelayMs)
        };
        _fadeTimer.Tick += FadeTimer_Tick;
    }

    private void MainWindow_MouseEnter(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        _fadeTimer.Stop();
        BeginAnimation(OpacityProperty,
            new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(150)));
    }

    private void MainWindow_MouseLeave(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        _fadeTimer.Start();
    }

    private void FadeTimer_Tick(object? s, EventArgs e)
    {
        _fadeTimer.Stop();
        var config = _configService.Current;
        BeginAnimation(OpacityProperty,
            new DoubleAnimation(config.OpacityIdle, TimeSpan.FromMilliseconds(400)));
    }

    // T-4.9: 진입 애니메이션
    private void PlayOpenAnimation()
    {
        Opacity         = 0;
        RenderTransform = new System.Windows.Media.TranslateTransform(0, 24);

        var sb = new Storyboard();

        var fadeIn = new DoubleAnimation(0, 1,
            new Duration(TimeSpan.FromMilliseconds(280)));
        Storyboard.SetTarget(fadeIn, this);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));

        var slideUp = new DoubleAnimation(24, 0,
            new Duration(TimeSpan.FromMilliseconds(300)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slideUp, this);
        Storyboard.SetTargetProperty(slideUp,
            new PropertyPath("RenderTransform.(TranslateTransform.Y)"));

        sb.Children.Add(fadeIn);
        sb.Children.Add(slideUp);
        sb.Begin();
    }
}
