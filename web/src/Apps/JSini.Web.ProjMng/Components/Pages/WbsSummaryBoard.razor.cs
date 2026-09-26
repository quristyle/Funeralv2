using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsSummaryBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;

    private string? _projectCode;

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 접힌 조회줄에 적는다 — 코드(<c>_projectCode</c>)
    /// 는 번호라 그 자리에 적어 봐야 무엇을 보고 있는지 알 수 없다.
    /// 목록은 <c>CodeSelect</c> 안에 있어 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;
    private string _basis = "edt";
    private string _scope = "dev";
    private string _who = "plan";

    private WbsBoardSummaryDto? _summary;
    private IReadOnlyList<UserRow> _users = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    /// <summary>
    /// 휴대폰에서 접혔을 때 머리줄에 적는 글. 고르개가 넷이라 접어 두면
    /// 화면의 절반이 돌아오는데, 대신 <b>무엇으로 걸러 본 값인지</b>를
    /// 여기서 말해 줘야 한다(`CommSch` 머리말).
    /// </summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _basis),
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope),
        WbsBoardOptions.TextOf(WbsBoardOptions.Who, _who));

    private string Period =>
        _summary?.MinDt is null || _summary.MaxDt is null ? "—" : $"{_summary.MinDt} ~ {_summary.MaxDt}";

    // **화면을 열 때 여기서 조회하지 않는다.** 프로젝트 고르개가 첫 항목을
    // 스스로 고르면서 조회를 건다(`AutoSelectFirst`). 둘 다 하면 프로젝트를
    // 안 건 조회와 건 조회가 같이 날아가고, 늦게 돌아온 쪽이 화면에 남는다.
    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _summary = null;
            _users = [];
            return 0;
        }

        var summary = Api.SummaryAsync(rid, _basis, _scope);
        var buckets = Api.MonthlyByUserAsync(rid, _basis, _scope, _who);

        await Task.WhenAll(summary, buckets);

        _summary = summary.Result;

        // 월별로 나뉘어 오는 것을 사람 단위로 합친다. 요약 화면은 기간을
        // 나누지 않으므로 한 사람이 여러 줄로 보이면 안 된다.
        _users =
        [
            .. buckets.Result
                .GroupBy(b => (b.UserBpId, b.UserNm))
                .Select(g => new UserRow
                {
                    UserBpId = g.Key.UserBpId,
                    UserNm = g.Key.UserNm,
                    Cnt = g.Sum(x => x.Cnt),
                    Done = g.Sum(x => x.Done),
                })
                .OrderByDescending(r => r.Cnt)
                .ThenBy(r => r.UserNm, StringComparer.CurrentCulture)
        ];

        return _users.Count;
    }, "자료가 없습니다.", "요약을 읽지 못했습니다");

    /// <summary>사람 한 줄. 월별 자료를 합친 것이라 서버에서 오는 모양과 다르다.</summary>
    private sealed class UserRow
    {
        public string? UserBpId { get; set; }
        public string? UserNm { get; set; }
        public int Cnt { get; set; }
        public int Done { get; set; }

        public double Rate => Cnt == 0 ? 0 : Done * 100.0 / Cnt;
    }
}
