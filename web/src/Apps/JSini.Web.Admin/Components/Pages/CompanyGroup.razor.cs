namespace JSini.Web.Admin.Components.Pages;

public partial class CompanyGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/admin/company/list", "회사 목록", "회사를 등록하고 고친다"),
        ("/admin/company/dept", "부서 관리", "부서 트리를 다룬다"),
        ("/admin/company/user", "회사 사용자", "회사에 속한 사람을 본다"),
        ("/admin/company/org-chart", "조직도", "회사 · 부서 · 사람을 트리로 본다"),
    ];
}
