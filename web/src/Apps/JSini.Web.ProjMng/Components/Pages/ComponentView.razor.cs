using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ComponentView
{
    [Inject] private ProjectClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    private IReadOnlyList<ProjectDto> _rows = [];
    private string? _projectCode;

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        var all = await Api.ListAsync();

        // 고른 프로젝트가 있으면 그 한 건만 — 옛 프로시저의 nvl(p_prj_rid,'') = '' 과 같은 뜻이다.
        _rows = int.TryParse(_projectCode, out var rid)
            ? [.. all.Where(p => p.PrjRid == rid)]
            : all;

        return _rows.Count;
    }, "조회 결과가 없습니다.", "조회하지 못했습니다");
}
