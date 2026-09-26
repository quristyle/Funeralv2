using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;

namespace JSini.Web.Components.Layout;

public partial class JSiniHead
{
    [Inject] private IReadOnlyList<IPortalModule> Modules { get; set; } = default!;

    /// <summary>
    /// 이 앱의 CSS isolation 번들 이름 (<c>JSini.Web.ProjMng.styles.css</c>).
    ///
    /// 업무 화면의 스타일은 각 앱의 <c>.razor.css</c> 에 둔다 — 공통 라이브러리에
    /// 쌓기 시작하면 앱을 나눈 의미가 스타일에서부터 사라진다. 그 번들을 싣는
    /// 자리가 여기다.
    ///
    /// <b>scoped CSS 가 하나도 없는 앱은 넘기지 않는다.</b> 번들 자체가 만들어지지
    /// 않으므로 <c>@@Assets[...]</c> 가 넣은 값을 그대로 돌려주고, 그러면 없는
    /// 파일을 가리키는 <c>&lt;link&gt;</c> 가 하나 붙어 404 를 한 번 낸다.
    /// </summary>
    [Parameter] public string? ScopedCssBundle { get; set; }
}
