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
        SchSummary.NameOf(UnitOptions, o => o.Value, o => o.Text, _unit),
        WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _basis),
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope),
        WbsBoardOptions.TextOf(WbsBoardOptions.Who, _who));

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;
    private string _unit = "month";
    private string _basis = "edt";
    private string _scope = "dev";
    private string _who = "plan";

    private IReadOnlyList<string> _buckets = [];
    private IReadOnlyList<Row> _rows = [];

    /// <summary>가장 많은 칸의 건수. 색의 짙기를 이것에 맞춘다.</summary>
    private int _peak;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private bool Weekly => _unit == "week";

    private static readonly SchOption[] UnitOptions =
    [
        new("month", "월"),
        new("week", "주"),
    ];

    /// <summary>
    /// 칸의 배경. <b>가장 많은 칸을 1 로 두고 견준다</b> — 절대 건수로 칠하면
    /// 전체가 적은 프로젝트는 판이 통째로 하얗다.
    /// </summary>
    private string? Shade(int cnt)
    {
        if (cnt == 0 || _peak == 0) return null;

        // 0.12 를 바닥으로 둔다. 한 건짜리 칸이 배경과 구분되지 않으면
        // 「비었다」와 「하나 있다」가 같아 보인다.
        var ratio = 0.12 + 0.68 * cnt / _peak;

        return $"background: color-mix(in srgb, var(--jsini-accent) {ratio * 100:0}%, transparent)";
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _buckets = [];
            _rows = [];
            return 0;
        }

        var raw = Weekly
            ? await Api.WeeklyByUserAsync(rid, _basis, _scope, _who)
            : await Api.MonthlyByUserAsync(rid, _basis, _scope, _who);

        _buckets = [.. raw.Select(r => r.Bucket).Where(b => b is not null).Distinct().Order()!];

        _rows =
        [
            .. raw.GroupBy(r => (r.UserBpId, r.UserNm))
                  .Select(g => new Row
                  {
                      UserBpId = g.Key.UserBpId,
                      UserNm = g.Key.UserNm,
                      Cells = g.Where(x => x.Bucket is not null)
                               .ToDictionary(x => x.Bucket!, x => (x.Cnt, x.Done)),
                  })
                  .OrderByDescending(r => r.Total)
                  .ThenBy(r => r.UserNm, StringComparer.CurrentCulture)
        ];

        _peak = _rows.SelectMany(r => r.Cells.Values).Select(v => v.Cnt).DefaultIfEmpty(0).Max();

        return _rows.Count;
    }, "개발자별 자료가 없습니다.", "개발자별 현황을 읽지 못했습니다");

    /// <summary>그 사람·그 기간으로 좁힌 상세 목록을 연다.</summary>
    private Task DrillAsync(string? user, string bucket)
    {
        if (string.IsNullOrWhiteSpace(user)) return Task.CompletedTask;

        // 담당자 잣대와 개발자 잣대가 조건 이름부터 다르다. 잘못 넘기면
        // 목록이 빈 채로 뜨고, 그것을 「자료가 없다」로 읽게 된다.
        var userKey = _who == "real" ? "realUser" : "user";
        var period = Weekly ? "week" : "month";

        Navigation.NavigateTo(
            $"/projmng/wbs/rows?{userKey}={Uri.EscapeDataString(user)}"
            + $"&{period}={Uri.EscapeDataString(bucket)}"
            + $"&basis={_basis}&scope={_scope}");

        return Task.CompletedTask;
    }

    /// <summary>사람 한 줄. 기간마다 칸이 하나씩 붙는다.</summary>
    private sealed class Row
    {
        public string? UserBpId { get; set; }
        public string? UserNm { get; set; }

        public Dictionary<string, (int Cnt, int Done)> Cells { get; set; } = [];

        public int Total => Cells.Values.Sum(v => v.Cnt);

        public (int Cnt, int Done) Cell(string bucket) =>
            Cells.TryGetValue(bucket, out var v) ? v : (0, 0);
    }
}
