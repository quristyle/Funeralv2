using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ProjectMonitoring
{
    [Inject] private WbsClient Wbs { get; set; } = default!;
    [Inject] private SourceInfoClient Sources { get; set; } = default!;
    [Inject] private ProjectUserClient Users { get; set; } = default!;
    [Inject] private ProjectDbClient Dbs { get; set; } = default!;
    [Inject] private ProjMngClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    /// <summary>볼 수 있는 각도. 순서가 곧 고르개의 순서다.</summary>
    private static readonly string[] Views = ["WBS", "소스", "담당자", "DB", "스캔된 화면"];

    private string _view = Views[0];

    private IReadOnlyList<WbsItemDto> _wbs = [];
    private IReadOnlyList<SourceInfoDto> _sources = [];
    private IReadOnlyList<ProjectUserDto> _users = [];
    private IReadOnlyList<ProjectDbDto> _dbs = [];
    private WbsSummaryDto? _summary;

    /// <summary>
    /// 소스 스캔 결과. <c>null</c> 은 「아직 안 눌렀다」는 뜻이다 — 0 건과
    /// 구분해야 한다. 스캔은 조회에 딸려 돌지 않는다.
    /// </summary>
    private IReadOnlyList<ScanRow>? _scanned;

    private string? _projectCode;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    /// <summary>진척 차트 한 점 = WBS 상태 하나.</summary>
    private sealed record StateCount(string Name, int Count);

    private IReadOnlyList<StateCount> _stateCounts = [];

    /// <summary>스캔 결과 한 줄. 소스마다 늘 있는 칸만 담는다(머리말).</summary>
    private sealed class ScanRow
    {
        public string Name { get; init; } = string.Empty;
        public string Dir { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Author { get; init; } = string.Empty;
        public string Credt { get; init; } = string.Empty;
    }

    private string TabTitle => _view;

    private string TabHint => _view switch
    {
        "WBS" => $"{_wbs.Count}건",
        "소스" => $"{_sources.Count}건",
        "담당자" => $"{_users.Count}명",
        "DB" => $"{_dbs.Count}건",
        _ => _scanned is null ? string.Empty : $"{_scanned.Count}건",
    };

    private string ProgressTitle
    {
        get
        {
            if (_summary is null || _summary.TotalTaskCount == 0)
            {
                return "WBS 진행";
            }

            return $"WBS 진행 — 완료 {_summary.CompletedTaskCount} / 전체 {_summary.TotalTaskCount}"
                 + $" ({_summary.CompletedTaskPct}%) · 지연 {_summary.DelayedTaskCount}";
        }
    }

    // **화면을 열 때 여기서 조회하지 않는다.** 프로젝트 고르개가 첫 항목을
    // 스스로 고르면서 조회를 건다(`AutoSelectFirst`). 둘 다 하면 **프로젝트를
    // 안 건 조회와 건 조회가 같이 날아가고**, 늦게 돌아온 쪽이 화면에 남는다 —
    // 프로젝트를 골랐는데 목록은 전체인 상태가 된다. 실제로 그렇게 보였다.

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _scanned = null;
        _stateCounts = [];
        _summary = null;

        if (ProjectRid is null)
        {
            _wbs = [];
            _sources = [];
            _users = [];
            _dbs = [];
            return 0;
        }

        // 다섯을 나란히. 서로 기다릴 이유가 없다.
        var wbs = Wbs.ListAsync(ProjectRid, scheduleTypes: ["WBS"]);
        var summary = Wbs.SummaryAsync(ProjectRid.Value);
        var sources = Sources.ListAsync(ProjectRid);
        var users = Users.ListAsync(ProjectRid);
        var dbs = Dbs.ListAsync(ProjectRid);

        await Task.WhenAll(wbs, summary, sources, users, dbs);

        _wbs = wbs.Result;
        _summary = summary.Result;
        _sources = sources.Result;
        _users = users.Result;
        _dbs = dbs.Result;

        BuildProgress();

        return _wbs.Count + _sources.Count + _users.Count + _dbs.Count;
    }, "그 프로젝트에 등록된 자료가 없습니다.", "진행 현황을 읽지 못했습니다");

    /// <summary>진척 차트의 재료. <b>서버가 센 값을 옮겨 담기만 한다</b>(머리말).</summary>
    private void BuildProgress()
    {
        if (_summary is null || _summary.TotalTaskCount == 0)
        {
            _stateCounts = [];
            return;
        }

        _stateCounts =
        [
            new StateCount("완료", _summary.CompletedTaskCount),
            new StateCount("진행", _summary.InProgressTaskCount),
            new StateCount("지연", _summary.DelayedTaskCount),
            new StateCount("미시작", _summary.NotStartedYetTaskCount),
        ];
    }

    /// <summary>
    /// 서버가 소스 경로를 훑어 화면 목록을 센다.
    ///
    /// 훑는 것은 <b>서버 장비의 디스크</b>라, 등록된 경로가 그 장비에 없으면
    /// 아무것도 안 나온다. 그것을 오류가 아니라 안내로 보여 준다.
    /// </summary>
    private Task ScanAsync() => LoadAsync(async () =>
    {
        _view = Views[^1];

        var result = await Client.MdContAsync("md_blazor_scan", new Dictionary<string, object?>
        {
            ["prj_rid"] = _projectCode ?? string.Empty,
        });

        if (result.ProcCode < 0)
        {
            throw new ApiException(result.Message ?? "소스를 훑지 못했습니다.");
        }

        _scanned =
        [
            .. (result.Rows ?? []).Select(row => new ScanRow
            {
                Name = Text(row, "name"),
                Dir = Text(row, "dir"),
                Url = Text(row, "url"),
                Title = Text(row, "title"),
                Author = Text(row, "author"),
                Credt = Text(row, "credt"),
            })
        ];

        return _scanned.Count;
    }, "훑어서 찾은 화면이 없습니다. 「소스 정보」에 경로가 등록돼 있는지 보십시오.", "소스를 훑지 못했습니다");

    private static string Text(ProjMngRow row, string key) =>
        row.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
}
