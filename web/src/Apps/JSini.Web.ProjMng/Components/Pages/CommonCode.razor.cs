using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class CommonCode
{
    [Inject] private DevCommonCodeClient Api { get; set; } = default!;

    private IReadOnlyList<DevCommonCodeDto> _groups = [];
    private IReadOnlyList<DevCommonCodeDto> _codes = [];

    private DevCommonCodeDto? _group;

    protected override Task OnInitializedAsync() => LoadGroupsAsync();

    private Task LoadGroupsAsync() => LoadAsync(async () =>
    {
        _groups = await Api.GroupsAsync();

        // 고른 묶음이 사라졌으면(지웠거나) 오른쪽을 비운다.
        if (_group is not null && _groups.All(g => g.CmRid != _group.CmRid))
        {
            _group = null;
            _codes = [];
        }

        return _groups.Count;
    }, "등록된 묶음이 없습니다.", "묶음을 읽지 못했습니다");

    private Task PickGroupAsync(DevCommonCodeDto? group)
    {
        _group = group;

        if (group is null)
        {
            _codes = [];
            return Task.CompletedTask;
        }

        return LoadCodesAsync();
    }

    private Task LoadCodesAsync() => _group is null ? Task.CompletedTask : LoadAsync(async () =>
    {
        _codes = await Api.ChildrenAsync(_group.CmCd ?? string.Empty);
        return _codes.Count;
    }, "이 묶음에 코드가 없습니다.", "코드를 읽지 못했습니다");

    /// <summary>새 묶음은 상위 코드가 없다 — 그것이 묶음의 정의다.</summary>
    private void FillNewGroup(DevCommonCodeDto c) => c.CmPcd = null;

    /// <summary>
    /// 새 코드는 <b>고른 묶음에 붙는다.</b> 사용자가 적게 두면 어느 묶음에도
    /// 안 붙은 줄이 생기고, 그 줄은 어느 화면에도 안 나온다.
    /// </summary>
    private void FillNewCode(DevCommonCodeDto c) => c.CmPcd = _group?.CmCd;

    private async Task SaveAsync((DevCommonCodeDto Item, bool IsNew) e)
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

    /// <summary>묶음 삭제. 딸린 코드가 있으면 서버가 막는다(머리말).</summary>
    private async Task DeleteGroupAsync(DevCommonCodeDto g)
    {
        await Api.DeleteAsync(g.CmRid, g.CmCd);

        if (_group?.CmRid == g.CmRid)
        {
            _group = null;
            _codes = [];
        }
    }

    private Task DeleteCodeAsync(DevCommonCodeDto c) => Api.DeleteAsync(c.CmRid);
}
