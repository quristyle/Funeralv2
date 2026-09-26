using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.CargoTrust.Admin.Api;

namespace JSini.Web.CargoTrust.Admin.Components.Pages;

public partial class ReportList
{
    [Inject] private CargoAdminClient Api { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "status")] public string? StatusQuery { get; set; }

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        SchSummary.NameOf(CargoAdminCodes.ReportStatus.Filter, o => o.Value, o => o.Text, _status);

    private IReadOnlyList<AdminReport> _rows = [];

    private string? _status;

    private bool _editing;
    private AdminReport? _target;
    private AdminResolve _form = new();

    /// <summary>
    /// 체크칸 옆 글자. 대상 종류에 따라 **무엇이 숨는지**를 말한다 — 「대상 숨김」만으로는
    /// 거래를 숨기면 통계에서 빠진다는 것이 안 보인다.
    /// </summary>
    private string HideLabel => _target?.TargetType?.ToUpperInvariant() switch
    {
        "TRANSACTION" => "이 거래를 통계에서 뺀다",
        "REVIEW" => "이 후기를 숨긴다",
        "COMPANY" => "이 거래처를 검색에서 숨긴다",
        _ => "대상을 숨긴다",
    };

    protected override Task OnInitializedAsync()
    {
        _status = CargoAdminCodes.ReportStatus.Options
            .FirstOrDefault(o => string.Equals(o.Value, StatusQuery, StringComparison.OrdinalIgnoreCase))?.Value;

        return ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _rows = await Api.GetReportsAsync(_status);
        return _rows.Count;
    }, "조건에 맞는 신고가 없습니다.", "신고를 읽지 못했습니다");

    private void Open(AdminReport row)
    {
        _target = row;

        // 접수된 채로 열면 「검토중」으로 올려 둔다 — 창을 연 것 자체가 검토를 시작했다는
        // 뜻이고, 처리 내용만 적고 상태를 안 바꾼 채 저장하는 실수를 줄인다.
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

        var id = _target.ReportId;
        var body = _form;

        if (await RunAsync(() => Api.ResolveReportAsync(id, body), "처리했습니다.", "처리하지 못했습니다"))
        {
            _editing = false;
            await ReloadAsync();
        }
    }
}
