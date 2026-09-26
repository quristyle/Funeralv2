namespace JSini.Web.Admin.Components.Pages;

public partial class AuthGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/admin/auth/role", "권한 그룹 관리", "역할을 만들고 지운다"),
        ("/admin/auth/user-role", "사용자별 권한", "사람에게 역할을 준다"),
        ("/admin/auth/menu-role", "메뉴별 권한", "역할이 볼 수 있는 메뉴와 버튼을 정한다"),
    ];
}
