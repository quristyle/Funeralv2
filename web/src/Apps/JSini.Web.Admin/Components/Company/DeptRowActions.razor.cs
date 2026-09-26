using JSini.Web.Admin.Api;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Company;

/// <summary>부서 나무의 관리 칸.</summary>
public partial class DeptRowActions
{
    [Parameter, EditorRequired] public DeptDto Dept { get; set; } = default!;

    [Parameter] public EventCallback<DeptDto> OnAddChild { get; set; }

    [Parameter] public EventCallback<DeptDto> OnEdit { get; set; }

    [Parameter] public EventCallback<DeptDto> OnDelete { get; set; }
}
