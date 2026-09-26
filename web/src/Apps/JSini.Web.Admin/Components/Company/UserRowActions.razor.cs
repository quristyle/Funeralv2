using JSini.Web.Admin.Api;

using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Company;

/// <summary>회사 사용자 표의 관리 칸.</summary>
public partial class UserRowActions
{
    [Parameter, EditorRequired] public AccountDto User { get; set; } = default!;

    [Parameter] public EventCallback<AccountDto> OnRemove { get; set; }
}
