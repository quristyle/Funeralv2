using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class ComTest
{
    [Inject] private ProjectClient Api { get; set; } = default!;

    private IReadOnlyList<ProjectDto> _rows = [];

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync();
        return _rows.Count;
    }, "조회 결과가 없습니다.", "조회하지 못했습니다");
}
