using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class DisputeList
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "status")] public string? StatusQuery { get; set; }

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        SchSummary.NameOf(CargoAdminCodes.DisputeStatus.Filter, o => o.Value, o => o.Text, _status);

    private IReadOnlyList<AdminDispute> _rows = [];

    private string? _status;

    private bool _editing;
    private AdminDispute? _target;
    private AdminResolve _form = new();

    private bool IsAccepted => string.Equals(_form.Status, "ACCEPTED", StringComparison.OrdinalIgnoreCase);

    protected override Task OnInitializedAsync()
    {
        _status = CargoAdminCodes.DisputeStatus.Options
            .FirstOrDefault(o => string.Equals(o.Value, StatusQuery, StringComparison.OrdinalIgnoreCase))?.Value;

        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetDisputesAsync(_status);
        return _rows.Count;
    }, "조건에 맞는 이의제기가 없습니다.", "이의제기를 읽지 못했습니다");

    private void Open(AdminDispute row)
    {
        _target = row;

        // 접수된 채로 열면 「검토중」으로 올려 둔다(신고 처리와 같은 까닭).
        _form = new AdminResolve
        {
            Status = string.Equals(row.Status, "RECEIVED", StringComparison.OrdinalIgnoreCase) ? "REVIEWING" : row.Status,
            Resolution = row.Resolution,
            HideTarget = false,
        };
        _editing = true;
    }

    private async Task SaveAsync()
    {
        if (_target is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_form.Status))
        {
            Say("상태를 고르십시오.", NoticeTone.Warning);
            return;
        }

        var id = _target.DisputeId;

        // 인용이 아니면 숨김을 싣지 않는다 — 체크칸은 잠겨 있지만, 인용으로 켜 둔 뒤
        // 상태만 기각으로 바꾸면 켜진 값이 그대로 남는다.
        var body = new AdminResolve
        {
            Status = _form.Status,
            Resolution = _form.Resolution,
            HideTarget = IsAccepted && _form.HideTarget,
        };

        if (await RunAsync(() => Api.ResolveDisputeAsync(id, body), "처리했습니다.", "처리하지 못했습니다"))
        {
            _editing = false;
            await ReloadAsync();
        }
    }
}
