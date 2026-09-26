using Microsoft.AspNetCore.Components;

namespace JSini.PublicSite.Components;

public partial class App
{
    /// <summary>
    /// 주소의 언어 조각. 문서의 <c>lang</c> 특성에 넣는다 —
    /// 스크린 리더와 검색 엔진이 이 값을 읽는다.
    ///
    /// 라우트 매개변수를 여기서 볼 수 없으므로 경로에서 직접 뗀다.
    /// </summary>
    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;

    private string? Locale => HttpContext.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
}
