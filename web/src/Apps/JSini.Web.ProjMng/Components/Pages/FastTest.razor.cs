using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class FastTest
{
    [Inject] private WbsClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private IReadOnlyList<WbsItemDto> _rows = [];

    /// <summary>
    /// 고른 프로젝트. <b>값을 미리 박아 두지 않는다.</b>
    ///
    /// 옛 화면은 <c>"7"</c> 을 적어 두었는데, 고르개는 <b>이미 고른 값이 목록에
    /// 있으면 다시 고르지 않는다</b>(그래야 다시 그릴 때마다 선택이 튀지 않는다).
    /// 그 말은 미리 박아 둔 값이 있으면 <c>OnChanged</c> 가 안 불리고
    /// <b>첫 조회가 영영 안 나간다</b>는 뜻이다 — 프로젝트 이름은 떠 있는데
    /// 표는 빈 채였다.
    /// </summary>
    private string? _projectCode;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    // **화면을 열 때 여기서 조회하지 않는다.** 프로젝트 고르개가 첫 항목을
    // 스스로 고르면서 조회를 건다(`AutoSelectFirst`). 둘 다 하면 **프로젝트를
    // 안 건 조회와 건 조회가 같이 날아가고**, 늦게 돌아온 쪽이 화면에 남는다 —
    // 프로젝트를 골랐는데 목록은 전체인 상태가 된다. 실제로 그렇게 보였다.

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync(ProjectRid);
        return _rows.Count;
    }, "돌아온 줄이 없습니다.", "부르지 못했습니다");
}
