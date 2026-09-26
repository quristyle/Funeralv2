using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class GroupLinks
{
    /// <summary>
    /// 하위 화면들. 주소는 <b>전체 경로</b>다 (<c>/funeral/status/simple</c>).
    ///
    /// 권한으로 거르지 않는다 — 여기서 거르면 사이드바의 판정과 갈라져서
    /// "목록에는 있는데 길잡이에는 없다" 가 생긴다. 실제 통제는 서버가 한다.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<(string Href, string Title, string Hint)> Links { get; set; } = [];
}
