using Microsoft.AspNetCore.Components;
using JSini.Web.CargoTrust.Api;
using JSini.Web.CargoTrust.Components.Shared;

namespace JSini.Web.CargoTrust.Components.Pages;

public partial class CompanyDetailPage
{
    [Inject] private CargoTrustClient Api { get; set; } = default!;

    [Parameter] public long CompanyId { get; set; }

    private sealed record PeriodOption(int? Days, string Text);

    /// <summary>기간 고르개. <c>null</c> 이 전체다(서버에는 <c>all</c> 로 간다).</summary>
    private static readonly PeriodOption[] Periods =
    [
        new(30, "30일"),
        new(90, "90일"),
        new(180, "180일"),
        new(365, "1년"),
        new(null, "전체"),
    ];

    private int? _period = 90;
    private long _loadedFor;
    private CompanyDetail? _detail;
    private IReadOnlyList<PublicReview> _reviews = [];
    private ReportPopup? _report;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == CompanyId)
        {
            return;
        }

        _loadedFor = CompanyId;
        _period = 90;
        await ReloadAsync(withReviews: true);
    }

    private Task ChangePeriodAsync(int? days)
    {
        _period = days;
        return ReloadAsync(withReviews: false);
    }

    private Task ReloadAsync(bool withReviews) => LoadAsync(async () =>
    {
        // 후기는 기간과 무관하다 — 기간을 바꿀 때마다 다시 읽지 않는다.
        var detail = Api.GetCompanyAsync(CompanyId, _period);
        var reviews = withReviews ? Api.GetCompanyReviewsAsync(CompanyId) : Task.FromResult(_reviews);

        _detail = await detail;
        _reviews = await reviews;
        return _detail is null ? 0 : -1;
    }, "거래처를 찾지 못했습니다.", "거래처를 읽지 못했습니다");
}
