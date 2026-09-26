using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsModuleBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Basis, _basis),
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope));

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;
    private string _basis = "edt";
    private string _scope = "dev";

    private IReadOnlyList<WbsBoardModuleDto> _rows = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _rows = [];
            return 0;
        }

        _rows = await Api.ByModuleAsync(rid, _basis, _scope);
        return _rows.Count;
    }, "모듈별 자료가 없습니다.", "모듈별 현황을 읽지 못했습니다");

    private void Drill(string? module)
    {
        if (string.IsNullOrWhiteSpace(module)) return;

        Navigation.NavigateTo(
            $"/projmng/wbs/rows?module={Uri.EscapeDataString(module)}&basis={_basis}&scope={_scope}");
    }
}
