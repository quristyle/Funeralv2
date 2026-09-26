using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class Wbs
{
    [Inject] private WbsClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _completeStateName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    /// <summary>고른 진행 상태의 이름.</summary>
    private string? _completeStateName;

    /// <summary>이 화면이 다루는 일정 종류. 일정표와 표를 나누는 유일한 값이다.</summary>
    private const string Kind = "WBS";

    private IReadOnlyList<WbsItemDto> _rows = [];

    private string? _projectCode;
    private string? _completeState;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string Hint => $"{_rows.Count(w => w.WbsState == "COMP")} / {_rows.Count}건 완료";

    // **화면을 열 때 여기서 조회하지 않는다.** 프로젝트 고르개가 첫 항목을
    // 스스로 고르면서 조회를 건다(`AutoSelectFirst`). 둘 다 하면 **프로젝트를
    // 안 건 조회와 건 조회가 같이 날아가고**, 늦게 돌아온 쪽이 화면에 남는다 —
    // 프로젝트를 골랐는데 목록은 전체인 상태가 된다. 실제로 그렇게 보였다.

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync(ProjectRid, _completeState, [Kind]);
        return _rows.Count;
    }, "일감이 없습니다.", "WBS 를 읽지 못했습니다");

    /// <summary>
    /// 새 일감은 <b>고른 프로젝트와 이 화면의 종류에 붙는다.</b> 안 붙이면
    /// 저장은 되는데 목록에서 사라진다 — 어느 프로젝트 것인지 모르기 때문이다.
    /// </summary>
    private void FillNew(WbsItemDto w)
    {
        w.PrjRid = ProjectRid;
        w.ScheduleType = Kind;
    }

    private async Task SaveAsync((WbsItemDto Item, bool IsNew) e)
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

    private Task DeleteAsync(WbsItemDto w) => Api.DeleteAsync(w.WbsId);
}
