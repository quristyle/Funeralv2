using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class StatisticsPage
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    private static readonly int[] MonthOptions = [6, 12, 24];

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => $"최근 {_months}개월";

    private int _months = 12;
    private int _shownMonths = 12;
    private AdminStatistics? _stats;

    private IReadOnlyList<AdminMonthly> Monthly => _stats?.Monthly ?? [];
    private IReadOnlyList<AdminDelayCompany> TopDelay => _stats?.TopDelayCompanies ?? [];
    private IReadOnlyList<AdminFlaggedUser> Flagged => _stats?.FlaggedUsers ?? [];

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync()
    {
        var months = _months;

        return LoadOneAsync(
            () => Api.GetStatisticsAsync(months),
            stats =>
            {
                _stats = stats;
                _shownMonths = months;
            },
            "통계 자료를 받지 못했습니다.",
            "통계를 읽지 못했습니다");
    }
}
