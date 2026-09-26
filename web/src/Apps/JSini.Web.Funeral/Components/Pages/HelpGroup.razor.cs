namespace JSini.Web.Funeral.Components.Pages;

public partial class HelpGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/funeral/help/qna", "Q & A", "묻고 답하기"),
        ("/funeral/help/faq", "F.A.Q", "자주 묻는 질문"),
        ("/funeral/help/archive", "자료실", "내려받을 수 있는 자료"),
    ];
}
