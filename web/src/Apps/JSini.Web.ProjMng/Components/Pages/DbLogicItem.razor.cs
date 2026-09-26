using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class DbLogicItem
{
    [Inject] private DbLogicClient Api { get; set; } = default!;

    private IReadOnlyList<DbLogicBaseDto> _rows = [];

    private string Hint => $"{_rows.Count(r => r.QueryCount == 0)}건은 질의가 없습니다 / 전체 {_rows.Count}건";

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync();
        return _rows.Count;
    }, "등록된 이름표가 없습니다.", "이름표를 읽지 못했습니다");

    private Task SaveAsync((DbLogicBaseDto Item, bool IsNew) e) => Api.SaveAsync(e.Item);
}
