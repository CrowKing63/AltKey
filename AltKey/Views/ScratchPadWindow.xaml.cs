using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using AltKey.Services;
using AltKey.ViewModels;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace AltKey.Views;

/// <summary>
/// [역할] 스크래치패드의 독립 창입니다 (D3 — 키보드 창과 같은 차원의 외부 확장, 격리된 top-level 윈도우).
/// [기능] 상단 드래그 핸들로 이동하고, 편집 숏컷 버튼(모두 선택/잘라내기/복사/붙여넣기)과 메모 TextBox를 제공합니다.
/// [참고] ✕는 창을 닫지 않고 숨김만 시킵니다 — Show/Hide는 MainViewModel이 관리합니다 (클립보드 패널 토글과 같은 패턴).
/// </summary>
public partial class ScratchPadWindow : Window
{
    private readonly ScratchPadViewModel _viewModel;

    // 드래그 시작 여부와 누른 위치 — 5px 이상 움직여야 이동으로 인정(클릭/더블클릭과 구별)
    private bool _isDragHandlePressed;
    private WpfPoint _dragHandlePressPoint;

    /// <summary>✕ 버튼 클릭 시 호출됩니다 (숨김 요청).</summary>
    public event Action? OnCloseRequested;

    public ScratchPadWindow(ScratchPadViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // 앱 종료(창 Closing) 시 아직 저장되지 않은 타이핑을 즉시 파일에 씁니다.
        Closing += (_, _) => _viewModel.SaveNow();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // XAML의 Topmost="True"를 SetWindowPos로 한 번 더 강화합니다 — MainWindow와 같은 패턴.
        App.Services?.GetService<WindowService>()?.SetTopmost(this, true);

        // 포커스 추적기에 현재 창을 등록해 보조 기능 모듈이 활성 창을 알 수 있게 한다.
        // (설정·레이아웃 편집기 등 다른 AltKey 창과 같은 패턴 — 없으면 키보드 입력 시
        //  재포커스 로직이 오래된 텍스트 상자를 찾아 포커스를 빼앗아 메모에 타이핑이 안 됨.)
        FocusTracker.Register(this);

        // 메인 창은 ShowActivated="False"라 클릭해도 패드의 Deactivated가
        // 발생하지 않을 수 있습니다. 입력 이벤트를 메인 창에서 직접 받아
        // 클릭 동작이 끝난 뒤 메모 TextBox에 포커스를 돌려줍니다.
        if (Owner is MainWindow host)
        {
            host.PreviewMouseDown += Host_PreviewMouseDown;
            Closed += (_, _) => host.PreviewMouseDown -= Host_PreviewMouseDown;
        }
    }

    private void Host_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            new Action(() =>
            {
                if (IsVisible && Owner is MainWindow host && host.IsVisible)
                    FocusTextBox();
            }));
    }

    /// <summary>열기 시 자동 포커스(D5): TextBox에 커서를 놓아 바로 타이핑할 수 있게 합니다.</summary>
    public void FocusTextBox()
    {
        NoteTextBox.Focus();
    }

    /// <summary>메모 입력 박스 — 키 입력 경로(KeyboardViewModel)가 포그라운드 판별·포커스에 씁니다.</summary>
    public System.Windows.Controls.TextBox InputBox => NoteTextBox;

    // ── 드래그 핸들: 키보드 창(KeyboardView)과 같은 패턴 ────────────────────

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            CenterWindowHorizontally(); // 더블 클릭 = 창 수평 중앙 배치 (D8)
            _isDragHandlePressed = false;
            e.Handled = true;
            return;
        }

        _isDragHandlePressed = true;
        _dragHandlePressPoint = e.GetPosition(this);
    }

    private void DragHandle_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isDragHandlePressed) return;

        // 더블 클릭으로 창이 이동한 뒤 마우스 단추 상태와 어긋난 MouseMove가 올 수 있음 —
        // 그때 DragMove()가 예외를 던지므로(2026-11-15 보고), 단추가 실제로 눌려 있을 때만 호출합니다.
        if (System.Windows.Input.Mouse.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            _isDragHandlePressed = false; // 상태가 어긋났으니 정리하고 다음 동작을 기다립니다
            return;
        }

        WpfPoint currentPos = e.GetPosition(this);
        if (!IsDragging && Math.Abs(currentPos.X - _dragHandlePressPoint.X) > 5)
        {
            IsDragging = true;
            DragMove(); // WPF가 마우스 캡처·이동을 알아서 처리합니다.
        }
    }

    private void DragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragHandlePressed = false;
        IsDragging = false;
    }

    private void DragHandle_LostMouseCapture(object sender, EventArgs e)
    {
        // 드래그 중 마우스 캡처를 잃으면(창 밖에서 놓음 등) 상태 정리
        _isDragHandlePressed = false;
        IsDragging = false;
    }

    /// <summary>더블클릭 동작: 창을 작업 영역 안에서 좌우 수평 중앙으로 배치합니다. 세로 위치는 그대로 유지 — 키보드 창(KbdView) 드래그 핸들과 같은 동작.</summary>
    private void CenterWindowHorizontally()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
    }

    // ── 헤더 버튼 ────────────────────────────────────────────────────────

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButtonSound();
        OnCloseRequested?.Invoke();
    }

    // 편집 숏컷: ViewModel이 TextBox 참조로 실행합니다 (D7 — 앱 간 자동화 없음).
    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButtonSound();
        _viewModel.SelectAll(NoteTextBox);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButtonSound();
        _viewModel.Clear(NoteTextBox);
    }

    private void CutButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButtonSound();
        _viewModel.Cut(NoteTextBox);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButtonSound();
        _viewModel.Copy(NoteTextBox);
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButtonSound();
        _viewModel.Paste(NoteTextBox);
    }

    /// <summary>빠른 메모의 버튼 입력에 키보드와 같은 효과음을 재생합니다.</summary>
    private static void PlayButtonSound() => App.Services?.GetService<SoundService>()?.Play();

    // 드래그 상태 플래드 — IsDragging은 XAML에서 두 번 쓰지 않으므로 코드에 둡니다.
    private bool IsDragging { get; set; }

    // ── 투명도 완전 연동: 패드에 마우스를 올리면 키보드 창처럼 타이머 초기화·기본 투명도로 복원 ──
    // (2026-11-15 사용자 요청 — 패드 호버가 "키보드에 손이 닿은 것"으로 취급됨)

    private void Window_MouseEnter(object sender, WpfMouseEventArgs e)
    {
        if (Owner is MainWindow mw)
            mw.ResetIdleFade();
    }

    private void Window_MouseLeave(object sender, WpfMouseEventArgs e)
    {
        if (Owner is MainWindow mw)
            mw.ResumeIdleFade();
    }
}
