using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class CodeView
{
    [Inject] private SourceInfoClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private IReadOnlyList<SourceInfoDto> _rows = [];
    private string? _projectCode;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync(ProjectRid);
        return _rows.Count;
    }, "등록된 소스가 없습니다.", "소스를 읽지 못했습니다");

    /// <summary>새 소스는 고른 프로젝트에 붙는다.</summary>
    private void FillNew(SourceInfoDto s) => s.PrjRid = ProjectRid;

    private async Task SaveAsync((SourceInfoDto Item, bool IsNew) e)
    {
        if (e.IsNew)
        {
            await Api.CreateAsync(e.Item);
        }
        else
        {
            await Api.UpdateAsync(e.Item);
        }
    }
}
