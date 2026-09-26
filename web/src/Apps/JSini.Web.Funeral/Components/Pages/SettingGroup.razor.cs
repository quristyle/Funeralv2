namespace JSini.Web.Funeral.Components.Pages;

public partial class SettingGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/funeral/setting/environment", "환경설정", "장례식장 기본 정보와 기능 스위치"),
        ("/funeral/setting/work-options", "업무 옵션", "업무 흐름에 관한 스위치"),
    ];
}
