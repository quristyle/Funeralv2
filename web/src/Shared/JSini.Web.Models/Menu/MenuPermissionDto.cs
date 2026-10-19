using System.Text.Json.Serialization;
using JSini.Web.Abstractions;

namespace JSini.Web.Models.Menu;

/// <summary>GET /auth/menu/permissions 한 줄. 화면 하나에 대한 권한이다. 권한은 포털 한 곳에서만 관리하고 모든 업무 앱이 그 결과를 따른다(앱마다 두면 어느 쪽이 맞는지 모르게 된다). 서버가 역할들을 OR 로 합치고 꺼진 동작을 이미 꺼서 내려준다.</summary>
public sealed class MenuPermissionDto
{
    /// <summary>메뉴 식별자 (<c>scom.system_menus.id</c>).</summary>
    [JsonPropertyName("menuId")]
    public string MenuId { get; set; } = string.Empty;

    /// <summary>메뉴의 라우트 경로. 화면이 자기 권한을 찾는 연결 고리이며, 업무 앱의 @page 와 같은 값이어야 한다.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("canView")] public bool CanView { get; set; }
    [JsonPropertyName("canSearch")] public bool CanSearch { get; set; }
    [JsonPropertyName("canCreate")] public bool CanCreate { get; set; }
    [JsonPropertyName("canUpdate")] public bool CanUpdate { get; set; }
    [JsonPropertyName("canDelete")] public bool CanDelete { get; set; }
    [JsonPropertyName("canPrint")] public bool CanPrint { get; set; }
    [JsonPropertyName("canExcel")] public bool CanExcel { get; set; }
    [JsonPropertyName("canCust1")] public bool CanCust1 { get; set; }
    [JsonPropertyName("canCust2")] public bool CanCust2 { get; set; }
    [JsonPropertyName("canCust3")] public bool CanCust3 { get; set; }
    [JsonPropertyName("canCust4")] public bool CanCust4 { get; set; }
    [JsonPropertyName("canCust5")] public bool CanCust5 { get; set; }
    [JsonPropertyName("canCust6")] public bool CanCust6 { get; set; }
    [JsonPropertyName("canCust7")] public bool CanCust7 { get; set; }
    [JsonPropertyName("canCust8")] public bool CanCust8 { get; set; }

    /// <summary>이 동작이 허용되는가. 서버가 내려준 값을 그대로 옮기고 여기서 다시 계산하지 않는다(계산이 두 곳이면 어긋난다).</summary>
    public bool Allows(MenuAction action) => action switch
    {
        MenuAction.View => CanView,
        MenuAction.Search => CanSearch,
        MenuAction.Create => CanCreate,
        MenuAction.Update => CanUpdate,
        MenuAction.Delete => CanDelete,
        MenuAction.Print => CanPrint,
        MenuAction.Excel => CanExcel,
        MenuAction.Cust1 => CanCust1,
        MenuAction.Cust2 => CanCust2,
        MenuAction.Cust3 => CanCust3,
        MenuAction.Cust4 => CanCust4,
        MenuAction.Cust5 => CanCust5,
        MenuAction.Cust6 => CanCust6,
        MenuAction.Cust7 => CanCust7,
        MenuAction.Cust8 => CanCust8,
        _ => false,
    };
}
