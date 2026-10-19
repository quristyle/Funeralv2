using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Settings;

/// <summary>사람을 짚어 알림을 보낸다 — 앱 푸시 · 메일. 생일 목록의 축하처럼 업무 화면 어디서나 「이 사람에게 한 통」이 생겨 포털관리 밖에 둔다. 메시지 발송(명단·서식·첨부)은 발송 콘솔이라 따로 둔다.
/// 열쇠는 로그인 아이디 하나다 — 부르는 쪽이 이메일을 알 필요가 없다(주소를 들고 다니면 목록마다 전 직원의 연락처가 브라우저까지 간다). 메일의 주소 경로만 emails 인 것은 큐를 거치지 않는 직발송이라 결과를 바로 알 수 있기 때문이다.</summary>
public sealed class NotifySender(GatewayClient gateway)
{
    /// <summary>한 번에 보낼 수 있는 사람 수. 넘으면 앞에서 자른다 — 서버가 세는 값이 아니라 전사 목록을 실수로 통째로 넘기는 것을 막는 자리다. 수백 명에게 보내야 하면 포털관리의 메시지 발송이 그 화면이다.</summary>
    public const int MaxRecipients = 50;

    /// <summary>앱 푸시를 보낸다.</summary>
    /// <param name="loginIds">받는 사람들의 포털 로그인 아이디.</param>
    /// <param name="title">알림 제목. 비면 서버가 막는다.</param>
    /// <param name="body">알림 본문.</param>
    /// <param name="url">알림을 눌렀을 때 열 주소. 비우면 서비스워커가 첫 화면을 연다.</param>
    /// <param name="iconOwnerKey">아이콘에 얼굴을 쓸 사람의 로그인 아이디. 받는 사람이 아니라 보내는 사람이다 — 받은 쪽이 먼저 묻는 것은 누가 보냈나다.</param>
    /// <param name="ct">그만두기.</param>
    /// <returns>서버가 준 결과. Sent 가 0 이어도 실패가 아니다 — 그 까닭이 같은 객체 안에 있다.</returns>
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

    /// <summary>메일을 SMTP 로 바로 보낸다. 성공·실패를 그 자리에서 안다. 결과는 받지 않는다 — 성공은 true 하나뿐이고 실패는 ApiException 이 사람에게 보여 줄 문장을 들고 온다.</summary>
    /// <param name="loginIds">받는 사람들의 포털 로그인 아이디.</param>
    /// <param name="subject">메일 제목. 본문 카드의 큰 글씨가 된다.</param>
    /// <param name="body">보낼 글. 평문이다.</param>
    /// <param name="html">참이면 본문을 완성된 HTML 문서로 보낸다. 거짓이면 서버가 회사 메일 틀을 입힌다 — 화면마다 HTML 을 짜면 틀이 복제되어 어긋난 것은 메일함에서만 보인다.</param>
    /// <param name="senderName">보낸 사람으로 적을 이름. 틀의 서명 줄이 된다 — 보내는 계정은 하나라 메일함에는 늘 JSini 포털로 뜨므로 이것이 없으면 받은 쪽은 누가 보냈는지 모른다.</param>
    /// <param name="ct">그만두기.</param>
    public Task SendEmailAsync(
        IEnumerable<string> loginIds,
        string subject,
        string body,
        bool html = false,
        string? senderName = null,
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
                senderName,
            },
            ct);

    /// <summary>보낼 아이디를 추린다 — 빈 값을 빼고, 겹친 것을 지우고, 상한에서 자른다. 겹친 것을 지우는 것이 핵심이다 — 부서를 겸임하는 계정은 목록에 두 번 들어와 그대로 보내면 두 통을 받는다.</summary>
    private static IEnumerable<string> Keys(IEnumerable<string> loginIds)
        => loginIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecipients);
}
