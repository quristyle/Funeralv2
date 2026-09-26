using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class ReviewList
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        SchSummary.NameOf(CargoAdminCodes.ReviewVisibility.Filter, o => o.Value, o => o.Text, _status);

    private IReadOnlyList<AdminReview> _rows = [];

    private string? _status;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private static bool IsHidden(AdminReview r) =>
        string.Equals(r.Status, "HIDDEN", StringComparison.OrdinalIgnoreCase);

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetReviewsAsync(_status);
        return _rows.Count;
    }, "조건에 맞는 후기가 없습니다.", "후기를 읽지 못했습니다");

    private async Task ToggleAsync(AdminReview row)
    {
        var hide = !IsHidden(row);
        var id = row.ReviewId;

        if (await RunAsync(
                () => Api.SetReviewStatusAsync(id, hide ? "HIDDEN" : "VISIBLE"),
                hide ? "후기를 숨겼습니다." : "후기를 다시 보이게 했습니다.",
                "바꾸지 못했습니다"))
        {
            await ReloadAsync();
        }
    }
}
