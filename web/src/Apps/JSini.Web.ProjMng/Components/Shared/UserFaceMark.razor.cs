using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class UserFaceMark
{
    [Inject] private UserFaceClient Faces { get; set; } = default!;

    /// <summary>그릴 사람의 포털 로그인 아이디.</summary>
    [Parameter] public string? LoginId { get; set; }

    /// <summary>덧붙일 모양 이름. 크기를 바꾸는 자리가 쓴다(<c>pm-face--lg</c>).</summary>
    [Parameter] public string? CssClass { get; set; }

    private UserFace? Face => Faces.Get(LoginId);

    /// <summary>
    /// 마우스를 올렸을 때 뜨는 이름. <b>아직 못 읽었으면 아이디</b>다 —
    /// 비워 두면 동그라미가 무엇을 가리키는지 알 길이 없다.
    /// </summary>
    public string? Who => Face?.Name ?? LoginId;

    /// <summary>
    /// 사진이 없을 때 동그라미에 넣을 <b>글자 한 자</b>.
    /// </summary>
    /// <remarks>
    /// 얼굴을 아직 못 읽었거나(첫 그림 · 조회 실패) 지워진 계정이면
    /// <b>로그인 아이디</b>의 첫 글자로 대신한다. 그 자리를 비우면 얼굴이
    /// 있는 카드와 없는 카드의 <b>왼쪽 여백이 달라져</b> 목록이 들쭉날쭉해진다.
    /// </remarks>
    private string Initial
    {
        get
        {
            if (Face is not null)
            {
                return Face.Initial;
            }

            return string.IsNullOrWhiteSpace(LoginId) ? "?" : LoginId.Trim()[..1].ToUpperInvariant();
        }
    }
}
