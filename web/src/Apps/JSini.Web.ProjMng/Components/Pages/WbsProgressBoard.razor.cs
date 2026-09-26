using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsProgressBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope),
        WbsBoardOptions.TextOf(WbsBoardOptions.Who, _who),
        SchSummary.NameOf(ViewOptions, o => o.Value, o => o.Text, _view));

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;
    private string _scope = "dev";
    private string _who = "plan";
    private string _view = "user";

    private WbsBoardProgressDto? _summary;
    private IReadOnlyList<WbsBoardProgressUserDto> _users = [];
    private IReadOnlyList<WbsBoardProgressModuleDto> _modules = [];
    private IReadOnlyList<WbsBoardProgressRowDto> _rows = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private static readonly SchOption[] ViewOptions =
    [
        new("user", "사람별"),
        new("module", "모듈별"),
        new("row", "항목별"),
    ];

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _summary = null;
            _users = [];
            _modules = [];
            _rows = [];
            return 0;
        }

        _summary = await Api.ProgressAsync(rid, _scope);

        // 고른 보기만 읽는다. 셋을 다 읽으면 왕복이 넷이 되고, 항목별은
        // 건수가 가장 많은 조회다.
        return _view switch
        {
            "module" => (_modules = await Api.ProgressByModuleAsync(rid, _scope)).Count,
            "row" => (_rows = await Api.ProgressRowsAsync(rid, _scope)).Count,
            _ => (_users = await Api.ProgressByUserAsync(rid, _scope, _who)).Count,
        };
    }, "진척률 자료가 없습니다.", "진척률을 읽지 못했습니다");
}
