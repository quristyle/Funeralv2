namespace JSini.Web.Models;

public sealed class NotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool IsRead { get; set; }
    public System.DateTime? CreatedAt { get; set; }
    public string? Category { get; set; }
    public string? Url { get; set; }
    public bool Delivered { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>
    /// 앱 알림이 띄웠던 아이콘 주소 — 대개 그 일을 시킨 사람의 프로필 사진이다
    /// (<c>/files/avatar/{파일아이디}</c>). 사진이 없는 계정이면 사람 형상
    /// 그림자, 사람을 지목하지 않은 알림(날씨 특보 등)이면 <c>null</c> 이다.
    /// </summary>
    /// <remarks>
    /// 알림함이 <b>앱 알림과 같은 그림</b>을 보여 주려고 서버가 보내 준다.
    /// <b>열쇠(<c>?t=</c>)는 떼여 있다</b> — 붙은 채로 두면 한 시간 뒤부터
    /// 그림자로 떨어진다(<c>PushSender.IconForLog</c> 머리말).
    /// 되짚어 만들 수 없어 발송할 때 기록에 남긴 값이다
    /// (<c>NotificationServer</c> 의 <c>PushSendLog.Icon</c> 머리말).
    /// <b>칸이 생기기 전에 보낸 줄은 비어 있다</b> — 그때 무엇을 그릴지는
    /// <c>NotificationInboxDrawer.IconOf</c> 가 정한다.
    /// </remarks>
    public string? Icon { get; set; }
}

public sealed class NotificationUnreadDto
{
    public int Unread { get; set; }
}
