using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsUserBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _basis),
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope));

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    private string? _projectCode;
    private string _basis = "edt";
    private string _scope = "dev";

    private IReadOnlyList<WbsBoardUserBucketDto> _planMonth = [];
    private IReadOnlyList<WbsBoardUserBucketDto> _planWeek = [];
    private IReadOnlyList<WbsBoardUserBucketDto> _realMonth = [];
    private IReadOnlyList<WbsBoardUserBucketDto> _realWeek = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string BasisLabel => WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _basis);

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _planMonth = _planWeek = _realMonth = _realWeek = [];
            return 0;
        }

        var planMonth = Api.MonthlyByUserAsync(rid, _basis, _scope, "plan");
        var planWeek = Api.WeeklyByUserAsync(rid, _basis, _scope, "plan");
        var realMonth = Api.MonthlyByUserAsync(rid, _basis, _scope, "real");
        var realWeek = Api.WeeklyByUserAsync(rid, _basis, _scope, "real");

        await Task.WhenAll(planMonth, planWeek, realMonth, realWeek);

        _planMonth = planMonth.Result;
        _planWeek = planWeek.Result;
        _realMonth = realMonth.Result;
        _realWeek = realWeek.Result;

        return _planMonth.Count + _realMonth.Count;
    }, "개발자별 자료가 없습니다.", "개발자별 현황을 읽지 못했습니다");

    /// <summary>그 사람·그 기간으로 좁힌 상세 목록을 연다. 담당자와 개발자는 상세 목록의 조건 이름부터 다르다 — 바꿔 넘기면 빈 목록이 뜬다.</summary>
    private void Drill(string userKey, string period, (string User, string Bucket) pick)
    {
        if (string.IsNullOrWhiteSpace(pick.User)) return;

        Navigation.NavigateTo(
            $"/projmng/wbs/rows?{userKey}={Uri.EscapeDataString(pick.User)}"
            + $"&{period}={Uri.EscapeDataString(pick.Bucket)}"
            + $"&basis={_basis}&scope={_scope}");
    }
}
