namespace JSini.Web.Models;

// ============================================================
// 「사람을 짚어 알림 한 통 보내기」가 쓰는 자료.
//
// **`NotificationSettings.cs` 와 갈래가 다르다.** 그쪽은 「내가 받을 것」의
// 설정이고 여기는 「남에게 보내는 것」이다. 같은 서비스를 부르지만 화면도
// 권한도 달라서 한 파일에 섞으면 어느 쪽 값인지 읽을 수가 없다.
//
// **칸 이름은 NotificationServer 의 DTO 와 글자까지 맞추지 않는다** — 보내는
// 몸통은 `NotifySender` 가 그 자리에서 만든다. 여기 있는 것은 화면이 주고받는
// 모양뿐이다.
// ============================================================

/// <summary>
/// 알림을 받을 사람 하나.
/// </summary>
/// <param name="LoginId">
/// 포털 로그인 아이디(<c>scom.accounts.user_id</c>). <b>푸시도 메일도 이 값
/// 하나로 간다</b> — 푸시는 구독의 주인 키고, 메일은 서버가 이 아이디로
/// 주소를 푼다(<c>emails/send</c> 의 <c>toUser</c>).
/// </param>
/// <param name="Name">사람이 읽는 이름. 창에 누구에게 보내는지 적는 데만 쓴다.</param>
/// <param name="Photo">
/// 얼굴 사진 주소. 없으면 이름 첫 글자를 그린다 — <b>부르는 쪽이 채운다.</b>
/// 이 묶음이 스스로 얼굴을 묻지 않는 이유는 목록 화면이 이미 한 번에 묻고
/// 있어서다(같은 아이디를 두 번 묻게 된다).
/// </param>
public sealed record NotifyRecipient(string LoginId, string Name, string? Photo = null);

/// <summary>
/// 한 번 보낸 결과.
/// </summary>
/// <remarks>
/// <para>
/// <b>길마다 따로 담는다.</b> 푸시는 갔는데 메일이 막히는 일(주소가 없는
/// 계정)이 흔한데, 하나로 뭉쳐 「보냈습니다」나 「실패했습니다」로만 말하면
/// 둘 중 어느 쪽이 갔는지 아무도 모른다.
/// </para>
/// <para>
/// 푸시는 <b>보낸 것이 0 이어도 실패가 아니다</b> — 구독한 기기가 없거나
/// 본인이 꺼 두었을 뿐이다. 그 수가 <see cref="PushSendResultDto"/> 안에
/// 들어 있고, 화면은 그것을 그대로 옮겨야 한다.
/// </para>
/// </remarks>
public sealed class NotifySendOutcome
{
    /// <summary>푸시를 보내려 했는가. 거짓이면 아래 두 칸은 볼 것이 없다.</summary>
    public bool PushTried { get; init; }

    /// <summary>푸시 결과. 서버가 답을 안 주면 <c>null</c>.</summary>
    public PushSendResultDto? Push { get; init; }

    /// <summary>푸시가 막힌 까닭. 성공했으면 <c>null</c>.</summary>
    public string? PushError { get; init; }

    /// <summary>메일을 보내려 했는가.</summary>
    public bool EmailTried { get; init; }

    /// <summary>메일이 나갔는가. 서버가 성공으로 답했을 때만 참이다.</summary>
    public bool EmailSent { get; init; }

    /// <summary>메일이 막힌 까닭. 성공했으면 <c>null</c>.</summary>
    public string? EmailError { get; init; }

    /// <summary>둘 중 하나라도 막혔는가.</summary>
    public bool HasError => PushError is not null || EmailError is not null;
}
