using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class FastTest
{
    [Inject] private WbsClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    private IReadOnlyList<WbsItemDto> _rows = [];

    /// <summary>고른 프로젝트. 값을 미리 박아 두지 않는다 — 고르개는 이미 고른 값이 목록에 있으면 다시 고르지 않으므로, 미리 박은 값이 있으면 OnChanged 가 안 불려 첫 조회가 영영 안 나간다.</summary>
    private string? _projectCode;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    // 화면을 열 때 여기서 조회하지 않는다 — 프로젝트 고르개가 첫 항목을 고르며 조회를 건다. 둘 다 하면 늦게 돌아온 쪽이 남아 목록이 전체로 보인다.

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync(ProjectRid);
        return _rows.Count;
    }, "돌아온 줄이 없습니다.", "부르지 못했습니다");
}
