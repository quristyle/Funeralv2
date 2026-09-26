using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class PageSpinner
{
    [Inject] private PageTransition Transition { get; set; } = default!;

    protected override void OnInitialized() => Transition.Changed += OnChanged;

    /// <summary>
    /// <b><c>InvokeAsync</c> 로 감싸면 안 된다.</b>
    ///
    /// <para>
    /// 켜라는 신호는 <c>LocationChanged</c> 안에서, 끄라는 신호는 화면의
    /// 조회가 끝나는 자리에서 온다 — 둘 다 이미 렌더러의 자리(dispatcher)다.
    /// 거기서 <c>InvokeAsync</c> 를 쓰면 <b>지금 만들고 있는 그림 다음으로
    /// 미뤄진다.</b>
    /// </para>
    ///
    /// <para>
    /// 그래서 덮개가 새 화면보다 늦게 켜졌다. 재 보니 본문은 30ms 에 갈리고
    /// 덮개는 199ms — <b>조회가 끝난 뒤</b>에야 켜졌다가 곧바로 꺼졌다.
    /// 덮으라고 만든 것이 덮을 일이 끝난 뒤에 나타난 셈이다.
    /// </para>
    /// </summary>
    private void OnChanged() => StateHasChanged();

    public void Dispose() => Transition.Changed -= OnChanged;
}
