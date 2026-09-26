using Microsoft.AspNetCore.Components;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Shared;

public partial class TargetBlock
{
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    /// <summary>해제 요청에 실어 보낼 대상 종류 — <c>company</c> · <c>department</c> · <c>account</c>.</summary>
    [Parameter, EditorRequired] public string Kind { get; set; } = string.Empty;

    [Parameter, EditorRequired] public IReadOnlyList<MenuRoleTargetDto> Targets { get; set; } = [];

    /// <summary>해제를 눌렀다. 실제 호출과 확인은 화면이 한다.</summary>
    [Parameter] public EventCallback<(string Kind, string TargetId, string RoleId, string Name)> OnDetach { get; set; }
}
