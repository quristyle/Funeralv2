namespace JSini.Web.Funeral.Components.Pages;

public partial class InfoGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/funeral/info/room-history", "호실 사용 이력", "호실별 사용 이력과 사용 일수"),
        ("/funeral/info/deceased-search", "고인 정보 조회", "이름·기간으로 고인을 찾는다"),
        ("/funeral/info/my-info", "내 정보", "내 권한과 담당 건물"),
        ("/funeral/info/preview", "미리보기", "빈소 화면에 지금 무엇이 나오는지"),
    ];
}
