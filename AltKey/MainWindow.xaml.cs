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
    private readonly WindowService _windowService;
    private readonly ConfigService _configService;
    private DispatcherTimer _fadeTimer = null!;

    public MainWindow(WindowService windowService, ConfigService configService, MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        _windowService = windowService;
        _configService = configService;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // T-1.3: WS_EX_NOACTIVATE 적용
        _windowService.ApplyNoActivate(hwnd);

        // T-1.4: 배경 설정 (반투명 단색, Acrylic은 추후)
        _windowService.ApplyBackground(this);

        // T-1.6: 창 위치/크기 복원
        RestoreWindowPosition();

        // T-1.7: 자동 페이딩 타이머 설정
        SetupFadeTimer();

        // 마우스 진입/이탈 이벤트 등록
        MouseEnter += MainWindow_MouseEnter;
        MouseLeave += MainWindow_MouseLeave;
    }

    // Esc 키로 닫기
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
        }
        base.OnKeyDown(e);
    }

    // T-1.6: 창 위치/크기 복원
    private void RestoreWindowPosition()
    {
        var cfg = _configService.Current.Window;
        Left = cfg.Left;
        Top = cfg.Top;
        Width = cfg.Width;
        Height = cfg.Height;
    }

    // T-1.6: 창 위치/크기 저장
    protected override void OnClosing(CancelEventArgs e)
    {
        _configService.Update(c =>
        {
            c.Window.Left = (int)Left;
            c.Window.Top = (int)Top;
            c.Window.Width = (int)Width;
            c.Window.Height = (int)Height;
        });

        base.OnClosing(e);
    }

    // T-1.7: 자동 페이딩 타이머 설정
    private void SetupFadeTimer()
    {
        var config = _configService.Current;
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(config.FadeDelaySeconds)
        };
        _fadeTimer.Tick += FadeTimer_Tick;
    }

    private void MainWindow_MouseEnter(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        _fadeTimer.Stop();
        // 즉시 복귀 (애니메이션)
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
}
