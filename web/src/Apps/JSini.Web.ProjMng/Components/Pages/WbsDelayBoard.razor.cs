using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsDelayBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope),
        WbsBoardOptions.TextOf(WbsBoardOptions.Who, _who),
        SchSummary.NameOf(KindOptions, o => o.Value, o => o.Text, _kind));

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;
    private string _scope = "dev";
    private string _who = "plan";
    private string _kind = "all";

    private WbsBoardDelayDto? _summary;
    private IReadOnlyList<WbsBoardDelayUserDto> _users = [];
    private IReadOnlyList<WbsBoardDelayRowDto> _rows = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private static readonly SchOption[] KindOptions =
    [
        new("all", "하나라도"),
        new("start", "착수지연"),
        new("finish", "종료지연"),
    ];

    private static string Days(int? days) => days is null ? "—" : $"{days}일";

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _summary = null;
            _users = [];
            _rows = [];
            return 0;
        }

        var summary = Api.DelayAsync(rid, _scope);
        var users = Api.DelayByUserAsync(rid, _scope, _who);
        var rows = Api.DelayRowsAsync(rid, _scope, _kind);

        await Task.WhenAll(summary, users, rows);

        _summary = summary.Result;
        _users = users.Result;
        _rows = rows.Result;

        return _rows.Count;
    }, "지연된 항목이 없습니다.", "지연 현황을 읽지 못했습니다");

    /// <summary>그 사람의 지연 건으로 좁힌 [상세 목록]을 연다.</summary>
    private void Drill(string? user)
    {
        if (string.IsNullOrWhiteSpace(user)) return;

        // 갈래 이름이 목록 쪽과 다르다 — 여기서는 `all` 이 「하나라도」인데
        // 목록의 필터는 `any` 다. 그대로 넘기면 조건이 안 걸린다.
        var late = _kind == "all" ? "any" : _kind;
        var userKey = _who == "real" ? "realUser" : "user";

        Navigation.NavigateTo(
            $"/projmng/wbs/rows?{userKey}={Uri.EscapeDataString(user)}&late={late}&scope={_scope}");
    }
}
