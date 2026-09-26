using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class AuthShell
{
    /// <summary>카드 제목. 화면 이름이 그대로 온다 (「로그인」 · 「비밀번호 찾기」).</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    /// <summary>제목 밑 한 줄. 이 화면에서 무엇을 하는지 적는다. 비우면 여백만 남는다.</summary>
    [Parameter] public string? Sub { get; set; }

    /// <summary>
    /// 왼쪽 브랜드 판의 소개 글. 기본값은 포털을 소개하는 문장이고,
    /// 화면이 할 말이 따로 있으면 덮는다.
    /// </summary>
    [Parameter]
    public string Lead { get; set; } =
        "모두를 위한 업무시스템";

    /// <summary>칸이 많아 카드가 넓어야 하는 화면(가입 신청 등).</summary>
    [Parameter] public bool Wide { get; set; }

    /// <summary>카드 안에 들어갈 폼.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
