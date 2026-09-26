namespace JSini.Web.Funeral.Components.Pages;

public partial class StatGroup
{
    private static readonly (string Href, string Title, string Hint)[] Links =
    [
        ("/funeral/stat/billing", "요금 내역", "고인별 시설 사용 요금"),
        ("/funeral/stat/room-usage", "호실 사용 내역", "호실별 사용 일수와 금액"),
    ];
}
