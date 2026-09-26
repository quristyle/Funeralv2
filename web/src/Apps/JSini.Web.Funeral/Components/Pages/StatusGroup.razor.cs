namespace JSini.Web.Funeral.Components.Pages;

public partial class StatusGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/funeral/status/funeral-info", "빈소 정보", "빈소별 고인·상주·시설 사용 정보"),
        ("/funeral/status/funeral-status", "빈소 현황", "호실별 사용 현황과 장비 상태"),
        ("/funeral/status/deceased-status", "고인 현황", "고인 목록과 진행 상태"),
        ("/funeral/status/simple", "간편 현황", "한 화면에 줄여 담은 현황"),
        ("/funeral/status/mobile", "모바일 현황", "휴대폰에서 보는 현황"),
    ];
}
