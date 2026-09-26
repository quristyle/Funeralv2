namespace JSini.Web.Admin.Components.Pages;

public partial class CommonGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/admin/system/common-code", "공통코드", "코드 그룹과 항목"),
        ("/admin/system/i18n", "다국어 관리", "화면 문구의 번역"),
        ("/admin/system/metadata", "메타데이터 관리", "드롭다운이 무엇을 읽을지 정한다"),
    ];
}
