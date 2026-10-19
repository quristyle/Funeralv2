using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class Wbs
{
    [Inject] private WbsClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _completeStateName);

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
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

    // 화면을 열 때 여기서 조회하지 않는다 — 프로젝트 고르개가 첫 항목을 고르며 조회를 건다. 둘 다 하면 늦게 돌아온 쪽이 남아 목록이 전체로 보인다.

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync(ProjectRid, _completeState, [Kind]);
        return _rows.Count;
    }, "일감이 없습니다.", "WBS 를 읽지 못했습니다");

    /// <summary>새 일감은 고른 프로젝트와 이 화면의 종류에 붙는다 — 안 붙이면 저장은 되는데 어느 프로젝트 것인지 몰라 목록에서 사라진다.</summary>
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
