using System.Globalization;
using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsSummaryBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;

    private string? _projectCode;

    /// <summary>고른 프로젝트의 이름. 접힌 조회줄에 적는다 — 코드는 번호라 그 자리에 적어서는 무엇을 보는지 알 수 없다.</summary>
    private string? _projectName;
    private string _basis = "edt";
    private string _scope = "dev";

    private WbsBoardSummaryDto? _summary;
    private WbsBoardDelayDto? _delay;
    private Dictionary<string, WbsBoardDelayUserDto> _delayByUser = [];
    private IReadOnlyList<UserRow> _users = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    /// <summary>휴대폰에서 접혔을 때 머리줄에 적는 글.</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _basis),
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope));

    private string Num(int v) => _summary is null ? "-" : v.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>원본과 같이 소수 한 자리로 반올림한 비율. 분모가 0 이면 0%.</summary>
    private static string PctText(int part, int whole) =>
        $"{(whole == 0 ? 0 : Math.Round(part * 1000.0 / whole) / 10):0.#}%";

    private static string Width(int part, int whole) =>
        (whole == 0 ? 0 : part * 100.0 / whole).ToString("0.##", CultureInfo.InvariantCulture);

    private static string? Blank(int? v) => v is > 0 ? v.Value.ToString(CultureInfo.InvariantCulture) : null;

    private WbsBoardDelayUserDto? DelayOf(string id) => _delayByUser.GetValueOrDefault(id);

    // 화면을 열 때 여기서 조회하지 않는다 — 프로젝트 고르개가 첫 항목을 고르며 조회를 건다. 둘 다 하면 늦게 돌아온 쪽이 남는다.
    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _summary = null;
            _delay = null;
            _delayByUser = [];
            _users = [];
            return 0;
        }

        var summary = Api.SummaryAsync(rid, _basis, _scope);
        var buckets = Api.MonthlyByUserAsync(rid, _basis, _scope, "plan");
        var delay = Api.DelayAsync(rid, _scope);
        var delayByUser = Api.DelayByUserAsync(rid, _scope, "plan");

        await Task.WhenAll(summary, buckets, delay, delayByUser);

        _summary = summary.Result;
        _delay = delay.Result;
        _delayByUser = delayByUser.Result
            .Where(r => r.UserBpId is not null)
            .GroupBy(r => r.UserBpId!)
            .ToDictionary(g => g.Key, g => g.First());

        // 월별로 나뉘어 오는 것을 사람 단위로 합친다. 완료는 담당자 잣대, 개발완료는 개발자 잣대 — 같은 줄을 두 잣대로 나란히 센다.
        _users =
        [
            .. buckets.Result
                .GroupBy(b => b.UserBpId ?? "")
                .Select(g => new UserRow(
                    g.Key,
                    g.First().UserNm ?? g.Key,
                    g.Sum(x => x.Cnt),
                    g.Sum(x => x.DonePlan),
                    g.Sum(x => x.DoneReal)))
                .OrderBy(r => r.Name, StringComparer.Create(new CultureInfo("ko-KR"), false))
        ];

        return _summary?.Total ?? 0;
    }, "자료가 없습니다.", "요약을 읽지 못했습니다");

    private sealed record UserRow(string Id, string Name, int Cnt, int Done, int Dev);
}
