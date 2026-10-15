using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class UserFaceMark
{
    [Inject] private UserFaceClient Faces { get; set; } = default!;

    /// <summary>그릴 사람의 포털 로그인 아이디.</summary>
    [Parameter] public string? LoginId { get; set; }

    /// <summary>
    /// 사람이 읽는 이름. <b>업무 자료가 이미 들고 있을 때만</b> 준다 —
    /// 얼굴이 아직 안 왔을 때 첫 글자를 여기서 딴다.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>덧붙일 모양 이름. 치수·색을 바꾸는 자리가 쓴다(<c>pm-face</c>).</summary>
    [Parameter] public string? CssClass { get; set; }

    private UserFace? Face => Faces.Get(LoginId);

    private string? Photo => Face?.Photo;

    /// <summary>
    /// 마우스를 올렸을 때 뜨는 이름. 화면이 준 이름 → 얼굴에서 읽은 이름 →
    /// 아이디 차례다. <b>비워 두면 동그라미가 무엇을 가리키는지 알 길이 없다.</b>
    /// </summary>
    public string? Who =>
        !string.IsNullOrWhiteSpace(Name) ? Name : Face?.Name ?? LoginId;

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
            var text = Who;
            return string.IsNullOrWhiteSpace(text) ? "?" : text.Trim()[..1].ToUpperInvariant();
        }
    }
}
