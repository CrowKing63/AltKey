using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using AltKey.Services;
using AltKey.Services.InputLanguage;
using AltKey.ViewModels;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;

namespace AltKey.Views;

/// <summary>
/// [역할] 스크래치패드의 독립 창입니다 (D3 — 키보드 창과 같은 차원의 외부 확장, 격리된 top-level 윈도우).
/// [기능] 상단 드래그 핸들로 이동하고, 편집 숏컷 버튼(모두 선택/잘라내기/복사/붙여넣기)과 메모 TextBox를 제공합니다.
/// [참고] ✕는 창을 닫지 않고 숨김만 시킵니다 — Show/Hide는 MainViewModel이 관리합니다 (클립보드 패널 토글과 같은 패턴).
/// 드래그 핸들·포그라운드 Win32는 공용 기반 클래스(AnchoredPanelWindowBase)에서 제공합니다.
/// </summary>
public partial class ScratchPadWindow : AnchoredPanelWindowBase
{
    private readonly ScratchPadViewModel _viewModel;
    // 메인 키보드의 가/A 키와 같은 모듈을 쓰기 위해 자동완성 서비스를 공유한다.
    private readonly AutoCompleteService? _autoComplete;

    /// <summary>✕ 버튼 클릭 시 호출됩니다 (숨김 요청).</summary>
    public event Action? OnCloseRequested;

    public ScratchPadWindow(ScratchPadViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // 서브모드가 바뀌면 메인 키보드와 같은 시점에 헤더 버튼 라벨도 갱신한다.
        _autoComplete = App.Services?.GetService<AutoCompleteService>();
        if (_autoComplete is not null)
        {
            UpdateKoreanSubmodeLabel(_autoComplete.ActiveSubmode);
            _autoComplete.SubmodeChanged += OnSubmodeChanged;
            Closed += (_, _) => _autoComplete.SubmodeChanged -= OnSubmodeChanged;
        }

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

    // ── 드래그 핸들 — 공용 기반 클래스(AnchoredPanelWindowBase)에서 제공합니다:
    //    더블클릭 = 수평 중앙, 누르면 이동. XAML의 DragHandle_* 이벤트가 그대로 베이스 메서드에 바인딩됩니다.

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

    /// <summary>헤더의 가/A 버튼 — 메인 키보드 토글 키와 같은 서브모드를 전환합니다.</summary>
    private void KoreanSubmodeButton_Click(object sender, RoutedEventArgs e)
    {
        PlayButtonSound();
        _autoComplete?.ToggleKoreanSubmode();
    }

    private void OnSubmodeChanged(InputSubmode submode) => UpdateKoreanSubmodeLabel(submode);

    /// <summary>라벨·툴팁·스크린리더 이름을 현재 입력 상태에 맞춰 바꿉니다.</summary>
    private void UpdateKoreanSubmodeLabel(InputSubmode submode)
    {
        bool isKorean = submode == InputSubmode.HangulJamo;
        KoreanSubmodeLabel.Text = isKorean ? "가" : "A";
        KoreanSubmodeButton.ToolTip = isKorean ? "영어 입력으로 전환" : "한국어 입력으로 전환";
        AutomationProperties.SetName(KoreanSubmodeButton, isKorean ? "영어로 전환" : "한국어로 전환");
    }

    /// <summary>빠른 메모의 버튼 입력에 키보드와 같은 효과음을 재생합니다.</summary>
    private static void PlayButtonSound() => App.Services?.GetService<SoundService>()?.Play();

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
