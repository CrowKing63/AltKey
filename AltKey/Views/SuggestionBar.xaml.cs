namespace AltKey.Views;

/// <summary>
/// [역할] 키보드 상단에 나타나는 단어 추천 바(Suggestion Bar)의 화면 로직을 담당합니다.
/// [기능] 한글 조합 중인 단어나 다음에 올 법한 단어들을 보여주고 선택할 수 있게 합니다.
/// </summary>
public partial class SuggestionBar : System.Windows.Controls.UserControl
{
    public SuggestionBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 추천 단어 영역에서 마우스 우클릭을 했을 때의 이벤트를 처리합니다.
    /// 현재는 기본 메뉴가 뜨지 않도록 이벤트를 무시하고 있습니다.
    /// </summary>
    private void CurrentWordSlot_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
