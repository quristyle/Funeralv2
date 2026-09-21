using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Settings;

/// <summary>
/// <b>사람을 짚어 알림을 보낸다</b> — 앱 푸시 · 메일.
/// </summary>
/// <remarks>
/// <para>
/// [왜 업무 모듈이 아니라 여기 있나]
/// </para>
///
/// <para>
/// 한동안 포털관리의 <c>AdminClient</c> 에만 있었고 그 자리에
/// 「관리 권한이 있는 화면만 부르므로 공용 자리로 올리지 않는다」고 적혀
/// 있었다. 그 전제가 깨졌다 — <b>생일 목록에서 축하 알림을 보낸다.</b>
/// 관리자만 하는 일이 아니고, 앞으로도 「이 사람에게 한 통」은 업무 화면
/// 어디서나 생긴다.
/// </para>
///
/// <para>
/// 포털관리의 「메시지 발송」(<c>/admin/push/send</c>)은 그대로 둔다. 그쪽은
/// 명단을 골라 서식 편집기와 첨부까지 싣는 <b>발송 콘솔</b>이고, 이것은
/// <b>이미 아는 사람에게 짧은 한 통</b>이다. 하나로 묶으면 생일 화면이
/// 첨부·문자·카카오 칸까지 지고 간다.
/// </para>
///
/// <para>
/// [열쇠가 로그인 아이디 하나다]
/// </para>
///
/// <para>
/// 푸시는 구독의 주인 키가 로그인 아이디고, 메일은 <c>toUser</c> 로 넘기면
/// 서버가 주소를 푼다. <b>부르는 쪽이 이메일 주소를 알 필요가 없다</b> —
/// 주소를 들고 다니면 목록 화면마다 전 직원의 연락처가 브라우저까지 간다.
/// </para>
///
/// <para>
/// [주소에 <c>notification</c> 이 두 번 나오는 것]
/// </para>
///
/// <para>
/// 앞엣것은 게이트웨이 접두사(어느 서비스냐)고 뒤엣것은 서비스 안의 묶음이다.
/// 메일만 <c>emails</c> 인 것은 그쪽이 <b>큐를 거치지 않는 직발송</b>이라
/// 묶음이 따로이기 때문이다 — <c>notifications/email</c> 로 보내면 배포
/// 장비의 스크립트가 나중에 처리해서 <b>갔는지 끝내 모른다.</b>
/// </para>
/// </remarks>
public sealed class NotifySender(GatewayClient gateway)
{
    /// <summary>
    /// 한 번에 보낼 수 있는 사람 수. 넘으면 앞에서 자른다.
    /// </summary>
    /// <remarks>
    /// 서버가 세는 값이 아니다 — <b>실수로 전사 목록을 통째로 넘기는 것</b>을
    /// 막는 자리다. 이 부품이 서는 화면은 「고른 몇 사람」이 전제고, 정말로
    /// 수백 명에게 보내야 하면 포털관리의 「메시지 발송」이 그 화면이다.
    /// </remarks>
    public const int MaxRecipients = 50;

    /// <summary>
    /// 앱 푸시를 보낸다.
    /// </summary>
    /// <param name="loginIds">받는 사람들의 포털 로그인 아이디.</param>
    /// <param name="title">알림 제목. 비면 서버가 막는다.</param>
    /// <param name="body">알림 본문.</param>
    /// <param name="url">알림을 눌렀을 때 열 주소. 비우면 서비스워커가 첫 화면을 연다.</param>
    /// <param name="iconOwnerKey">
    /// 아이콘에 얼굴을 쓸 사람의 로그인 아이디. <b>받는 사람이 아니라 보내는
    /// 사람</b>이다 — 알림을 받은 쪽이 먼저 묻는 것은 「누가 보냈나」다.
    /// </param>
    /// <param name="ct">그만두기.</param>
    /// <returns>
    /// 서버가 준 결과. <b><c>Sent</c> 가 0 이어도 실패가 아니다</b> —
    /// 그 까닭이 같은 객체 안에 들어 있다.
    /// </returns>
    public Task<PushSendResultDto?> SendPushAsync(
        IEnumerable<string> loginIds,
        string title,
        string? body = null,
        string? url = null,
        string? iconOwnerKey = null,
        CancellationToken ct = default)
    {
        var owners = Keys(loginIds)
            .Select(id => new { ownerType = "jsini", ownerKey = id })
            .ToArray();

        return gateway.PostAsync<PushSendResultDto>(
            "notification/notifications/push",
            new
            {
                owners,
                message = new { title, body, url, iconOwnerKey },
            },
            ct);
    }

    /// <summary>
    /// 메일을 <b>SMTP 로 바로</b> 보낸다. 성공·실패를 그 자리에서 안다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 결과를 받지 않고 <see cref="GatewayClient.PostAsync(string, object?, CancellationToken)"/>
    /// 를 쓴다. 서버가 성공일 때 돌려주는 것은 <c>true</c> 하나뿐이라 담을
    /// 것이 없고, <b>실패는 <see cref="ApiException"/> 이 까닭을 들고 온다</b> —
    /// 「이메일이 등록되지 않은 아이디입니다: …」처럼 사람에게 그대로 보여 줄
    /// 수 있는 문장이다.
    /// </para>
    /// </remarks>
    public Task SendEmailAsync(
        IEnumerable<string> loginIds,
        string subject,
        string body,
        bool html = false,
        CancellationToken ct = default)
        => gateway.PostAsync(
            "notification/emails/send",
            new
            {
                // **아이디를 `to` 에 싣지 않는다.** 주소 꼴이 아니라 그대로
                // 걸러지고 `NO_RECIPIENT` 로 돌아온다.
                toUser = string.Join(',', Keys(loginIds)),
                subject,
                body,
                html,
            },
            ct);

    /// <summary>
    /// 보낼 아이디를 추린다 — 빈 값을 빼고, 겹친 것을 지우고, 상한에서 자른다.
    /// </summary>
    /// <remarks>
    /// <b>겹친 것을 지우는 것이 핵심이다.</b> 목록 화면은 같은 사람이 두 번
    /// 들어 있을 수 있는데(부서를 겸임하는 계정), 그대로 보내면 그 사람만
    /// 알림을 두 통 받는다.
    /// </remarks>
    private static IEnumerable<string> Keys(IEnumerable<string> loginIds)
        => loginIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecipients);
}
