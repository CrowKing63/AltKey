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

    // T-1.6: 창 위치/크기 복원 (화면 경계 밖이면 중앙 하단으로 초기화)
    private void RestoreWindowPosition()
    {
        var cfg = _configService.Current.Window;

        Width  = Math.Max(400, cfg.Width);
        Height = Math.Max(200, cfg.Height);

        // 화면 작업 영역 (가상 스크린 전체)
        var screen = System.Windows.SystemParameters.WorkArea;

        double left = cfg.Left;
        double top  = cfg.Top;

        // -1이거나 화면 밖이면 화면 중앙 하단으로 초기화
        bool offScreen = left < 0 || top < 0
            || left + Width  > screen.Right  + 200   // 일부 걸쳐 있으면 허용
            || top  + Height > screen.Bottom + 200
            || left < screen.Left - 200
            || top  < screen.Top  - 200;

        if (offScreen)
        {
            Left = screen.Left + (screen.Width  - Width)  / 2;
            Top  = screen.Top  + (screen.Height - Height) * 0.75; // 화면 하단 쪽
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

    // T-4.9: 진입 애니메이션 (Window는 RenderTransform 미지원 → 페이드인만)
    private void PlayOpenAnimation()
    {
        Opacity = 0;
        BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(280))));
    }
}
