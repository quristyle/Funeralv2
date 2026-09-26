using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class ProjectGrid
{
    [Inject] private ProjectClient Api { get; set; } = default!;

    /// <summary>보여 줄 줄. 화면이 골라 넘긴다.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<ProjectDto> Rows { get; set; } = [];

    /// <summary>엑셀 파일 이름.</summary>
    [Parameter] public string ExportName { get; set; } = "프로젝트";

    /// <summary>자료가 없을 때의 안내. 화면마다 뜻이 달라 받는다.</summary>
    [Parameter] public string EmptyText { get; set; } = "조회된 자료가 없습니다.";

    /// <summary>아래 띠의 「다시 읽기」.</summary>
    [Parameter] public EventCallback Reload { get; set; }
}
