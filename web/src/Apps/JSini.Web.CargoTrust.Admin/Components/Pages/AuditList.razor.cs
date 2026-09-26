using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class AuditList
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.NameOf(CargoAdminCodes.AuditTarget.Filter, o => o.Value, o => o.Text, _target),
        SchSummary.Period(_from, _to));

    private IReadOnlyList<AdminAuditEntry> _rows = [];

    private string? _target;
    private DateTime? _from = DateTime.Today.AddDays(-7);
    private DateTime? _to = DateTime.Today;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetAuditAsync(_target, _from, _to);
        return _rows.Count;
    }, "조건에 맞는 감사 기록이 없습니다.", "감사 기록을 읽지 못했습니다");
}
