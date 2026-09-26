using Microsoft.AspNetCore.Components;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Shared;

public partial class UserFaceMark
{
    [Inject] private UserFaceClient Faces { get; set; } = default!;

    /// <summary>그릴 사람의 포털 로그인 아이디.</summary>
    [Parameter] public string? LoginId { get; set; }

    /// <summary>사람이 읽는 이름. 얼굴이 아직 안 왔을 때 첫 글자를 여기서 딴다.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>덧붙일 모양 이름. 크기를 바꾸는 자리가 쓴다(<c>le-face--lg</c>).</summary>
    [Parameter] public string? CssClass { get; set; }

    private string? Photo => Faces.PhotoOf(LoginId);

    /// <summary>
    /// 사진이 없을 때 동그라미에 넣을 <b>글자 한 자</b>.
    /// </summary>
    /// <remarks>
    /// 그 자리를 비우면 얼굴이 있는 줄과 없는 줄의 <b>왼쪽 여백이 달라져</b>
    /// 목록이 들쭉날쭉해진다.
    /// </remarks>
    private string Initial
    {
        get
        {
            var text = string.IsNullOrWhiteSpace(Name) ? LoginId : Name;
            return string.IsNullOrWhiteSpace(text) ? "?" : text.Trim()[..1].ToUpperInvariant();
        }
    }
}
