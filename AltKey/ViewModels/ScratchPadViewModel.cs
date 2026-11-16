using AltKey.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace AltKey.ViewModels;

/// <summary>
/// [역할] 스크래치패드 창(ScratchPadWindow)의 뷰모델입니다.
/// [기능] 메모 텍스트를 관리하고, 타이핑을 디바운스로 자동 저장하며, 편집 숏컷(모두 선택/잘라내기/복사/붙여넣기)을 실행합니다.
/// [참고] 앱 간 자동화(Ctrl+C/V 전송)는 하지 않습니다 — TextBox 안에서만 동작하는 일반 편집 버튼입니다.
/// </summary>
public partial class ScratchPadViewModel : ObservableObject
{
    private readonly ScratchPadService _service;

    /// <summary>스크래치패드의 메모 내용입니다.</summary>
    [ObservableProperty]
    private string text = "";

    public ScratchPadViewModel(ScratchPadService service)
    {
        _service = service;
        Text = service.Load();
    }

    // 타이핑마다 디바운스 저장 — 500ms 뒤에 파일이 실제로 쓰입니다.
    partial void OnTextChanged(string value) => _service.ScheduleSave(value);

    /// <summary>
    /// 닫기(✕)·앱 종료 경로에서 호출합니다 — 아직 저장되지 않은 타이핑을 즉시 파일에 씁니다.
    /// </summary>
    public void SaveNow() => _service.SaveNow();

    // ── 편집 숏컷: TextBox 참조로 실행 (D7 — 버튼만, 앱 간 자동화 없음) ──────

    /// <summary>모두 선택 (Ctrl+A와 동일).</summary>
    public void SelectAll(WpfTextBox? textBox) => textBox?.SelectAll();

    /// <summary>메모 내용을 모두 지우고 입력 위치를 문서 시작으로 옮깁니다.</summary>
    public void Clear(WpfTextBox? textBox)
    {
        if (textBox is null) return;
        textBox.Clear();
        textBox.Focus();
    }

    /// <summary>잘라내기 (선택 영역을 클립보드에 두고 지움).</summary>
    public void Cut(WpfTextBox? textBox) => textBox?.Cut();

    /// <summary>복사 (선택 영역을 클립보드에 두기만 함).</summary>
    public void Copy(WpfTextBox? textBox) => textBox?.Copy();

    /// <summary>
    /// 붙여넣기: 클립보드를 직접 읽어서 커서 위치에 삽입합니다.
    /// ClipboardHelper.GetTextWithRetry를 재사용해 클립보드 COM 오류로 실패해도 조용히 무시됩니다.
    /// </summary>
    public void Paste(WpfTextBox? textBox)
    {
        if (textBox is null) return;
        string? text = ClipboardHelper.GetTextWithRetry();
        if (text is null) return;

        // 선택 영역이 있으면 교체하고, 없으면 커서 위치에 삽입 — Windows 붙여넣기와 같은 동작.
        textBox.SelectedText = text;

        // WPF는 SelectedText를 넣은 뒤 선택을 자동으로 풀지 않습니다(실제 Ctrl+V와 다름) —
        // 붙여넣은 텍스트 끝까지 커서를 옮겨 선택을 없앱니다 (2026-11-15 사용자 보고).
        int caretPos = textBox.SelectionStart + textBox.SelectionLength;
        textBox.Select(caretPos, 0);
    }
}
