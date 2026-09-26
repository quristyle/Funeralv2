using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Shared;

public partial class MenuGlyph
{
    /// <summary>메뉴 유형 — <c>MENU</c> · <c>CATALOG</c> · <c>BUTTON</c> · <c>EMBEDDED</c> · <c>LINK</c>.</summary>
    [Parameter] public string? Type { get; set; }

    /// <summary>DB 에 적힌 iconify 이름. 비어 있으면 아래 규칙으로 채운다.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>
    /// 이 메뉴가 여는 화면이 있는가. 문서와 폴더를 가르는 값이다.
    ///
    /// <para>
    /// 부르는 쪽이 판단해서 넘긴다 — <c>RouteKey</c> 가 정본이지만 아직 안
    /// 채운 메뉴가 26건 남아 있어 옛 <c>Component</c> 도 함께 봐야 한다.
    /// 그 판단을 여기 두면 DTO 두 개를 다 알아야 한다.
    /// </para>
    /// </summary>
    [Parameter] public bool OpensScreen { get; set; }

    /// <summary>보여 줄 이름. 이미 번역을 거친 글자를 넘긴다.</summary>
    [Parameter] public string? Text { get; set; }

    private string Icon()
    {
        if (string.Equals(Type, "BUTTON", StringComparison.OrdinalIgnoreCase))
        {
            return "carbon:security";
        }

        if (!string.IsNullOrWhiteSpace(Name))
        {
            return Name!;
        }

        return OpensScreen ? "lucide:file-text" : "lucide:folder";
    }
}
