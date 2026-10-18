using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class UserAvatar
{
    [Inject] private UserFaceClient Faces { get; set; } = default!;
    [Inject] private UserCard Cards { get; set; } = default!;

    /// <summary>그릴 사람의 포털 로그인 아이디. 사진을 안 주면 이것으로 얼굴을 묻는다.</summary>
    [Parameter] public string? LoginId { get; set; }

    /// <summary>
    /// 사람이 읽는 이름. <b>업무 자료가 이미 들고 있을 때만</b> 준다 —
    /// 얼굴이 아직 안 왔을 때 첫 글자를 여기서 딴다.
    /// </summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>
    /// 이미 아는 사진 주소(지금 로그인한 사람 · 조직도처럼 자료에 실려 온 것).
    /// 주면 <see cref="LoginId"/> 로 묻지 않는다. DB 저장값(<c>/api/file/…</c>)을 그대로 줘도 된다.
    /// </summary>
    [Parameter] public string? Photo { get; set; }

    /// <summary>
    /// <see cref="Photo"/> 로 준 사진을 못 받았을 때. 그 값을 들고 있는 쪽이
    /// 버리게 하려는 것이다(<c>CurrentUser.MarkAvatarUnavailable</c>).
    /// </summary>
    [Parameter] public EventCallback OnPhotoError { get; set; }

    /// <summary>덧붙일 모양 이름. 치수·색을 바꾸는 자리가 쓴다(<c>pm-face</c>).</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <summary>
    /// 누르면 사람 카드를 띄울까. <see cref="LoginId"/> 가 있을 때만 뜻이 있다.
    /// 단추 안에 놓인 얼굴은 끈다 — 줄을 누르려던 손이 카드를 연다.
    /// </summary>
    [Parameter] public bool Card { get; set; } = true;

    private bool Clickable => Card && !string.IsNullOrWhiteSpace(LoginId);

    /// <summary>말풍선이 붙을 자리. 카드가 이 아이디로 아바타를 찾는다.</summary>
    private readonly string _anchorId = $"jsini-face-{Guid.NewGuid():N}";

    private void OnClick()
    {
        if (Clickable)
        {
            Cards.Open(LoginId!, _anchorId, Who, PhotoUrl);
        }
    }

    private void OnKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " ")
        {
            OnClick();
        }
    }

    /// <summary>못 받은 사진 주소. 같은 주소가 다시 와도 깨진 그림을 또 그리지 않는다.</summary>
    private string? _broken;

    private UserFace? Face => Faces.Get(LoginId);

    private string? Given =>
        string.IsNullOrWhiteSpace(Photo) ? null : FileDownload.RelayUrl(Photo);

    private string? PhotoUrl
    {
        get
        {
            var url = Given ?? Face?.Photo;
            return string.Equals(url, _broken, StringComparison.Ordinal) ? null : url;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Given is null && !string.IsNullOrWhiteSpace(LoginId) && Face is null)
        {
            await Faces.WhenKnownAsync(LoginId);
        }
    }

    private async Task OnImageErrorAsync(string url)
    {
        _broken = url;

        if (Given is not null)
        {
            await OnPhotoError.InvokeAsync();
        }
        else
        {
            Faces.MarkPhotoUnavailable(LoginId);
        }
    }

    /// <summary>
    /// 마우스를 올렸을 때 뜨는 이름. 화면이 준 이름 → 얼굴에서 읽은 이름 →
    /// 아이디 차례다. <b>비워 두면 동그라미가 무엇을 가리키는지 알 길이 없다.</b>
    /// </summary>
    public string? Who =>
        !string.IsNullOrWhiteSpace(Name) ? Name : Face?.Name ?? LoginId;

    /// <summary>
    /// 사진이 없을 때 동그라미에 넣을 <b>글자 한 자</b>. 이모지처럼 두 칸을 쓰는
    /// 글자는 짝을 지켜 가져온다 — 반만 자르면 깨진 네모가 된다.
    /// </summary>
    private string Initial
    {
        get
        {
            var text = Who?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return "?";
            }

            return char.IsHighSurrogate(text[0]) && text.Length > 1
                ? text[..2]
                : text[..1].ToUpperInvariant();
        }
    }
}
