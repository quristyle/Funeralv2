using System.Text;
using System.Text.Json;

namespace AuthServer.Services;

/// <summary>
/// 계정 관련 안내 메일을 NotificationServer 로 내보내는 클라이언트.
/// </summary>
/// <remarks>
/// <para>
/// 자리는 <see cref="BirthdayNotifyClient"/> 와 같다 — 루프백 직접 호출이고
/// (게이트웨이를 거치지 않는다) 신원은 <c>X-User-Id</c> 헤더에 서비스 이름을
/// 적어 보낸다. SiteServer 가 문의 접수를 <c>SITE_INQUIRY</c> 로 보내는 것과
/// 같은 규약이다.
/// </para>
///
/// <para>
/// [실패를 삼키지 <b>않는다</b> — 이 클라이언트만 다르다]
/// </para>
///
/// <para>
/// 생일 푸시는 실패해도 메시지가 이미 저장돼 있어 로그만 남기면 됐다.
/// 재설정 메일은 반대다 — <b>메일이 못 나가면 사용자가 얻는 것이 아무것도
/// 없다.</b> 그런데 화면에는 「보냈습니다」가 뜬다(아이디가 있는지 알려 주지
/// 않으려고 언제나 성공으로 답하기 때문이다). 조용히 실패하면 사용자는
/// 오지 않는 메일을 기다리고, 우리는 그런 일이 있었는지도 모른다.
/// 그래서 보낸 결과를 돌려주고, 부르는 쪽이 <b>오류로 로그에 남긴다</b>.
/// </para>
/// </remarks>
public class AccountMailClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AccountMailClient> _logger;

    public AccountMailClient(HttpClient http, IConfiguration configuration, ILogger<AccountMailClient> logger)
    {
        _http = http;
        _logger = logger;

        var baseUrl = configuration["Notify:BaseUrl"] ?? "http://127.0.0.1:5460";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    /// <summary>메일 한 통을 보낸다. 보냈으면 <c>true</c>.</summary>
    /// <param name="to">받는 사람 주소</param>
    /// <param name="subject">제목</param>
    /// <param name="body">본문 (HTML)</param>
    /// <param name="sender">
    /// <c>X-User-Id</c> 에 실을 이름. 사람이 아니라 <b>어느 기능이 보냈는지</b>를
    /// 적는다(<c>AUTH_PASSWORD_RESET</c>). 익명 요청이라 사람 아이디가 없다.
    /// </param>
    /// <param name="ct">보내는 도중 취소할 때 쓰는 토큰</param>
    public Task<bool> SendAsync(
        string to, string subject, string body, string sender, CancellationToken ct = default)
        => PostAsync(to, null, subject, body, sender, ct);

    /// <summary>
    /// 역할을 받는 사람으로 보낸다 (<c>SYSTEM_ADMINISTRATOR</c>).
    /// 받는 주소를 NotificationServer 가 그 역할의 대표 이메일로 풀어 준다.
    /// </summary>
    /// <remarks>
    /// 관리자 주소를 설정 파일에 적어 두지 않는 이유다 — 담당자가 바뀔 때
    /// 고칠 곳이 늘면 반드시 옛 주소가 남는다.
    /// </remarks>
    public Task<bool> SendToRoleAsync(
        string role, string subject, string body, string sender, CancellationToken ct = default)
        => PostAsync(null, role, subject, body, sender, ct);

    private async Task<bool> PostAsync(
        string? to, string? toRole, string subject, string body, string sender, CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                to = to ?? string.Empty,
                toRole,
                subject,
                body,

                // **`isHtml` 이 아니다.** 저쪽 DTO(`SendEmailDto`)의 속성 이름은
                // `Html` 이고, 이름이 안 맞으면 붙지 않고 조용히 기본값(false)이
                // 쓰인다 — 그러면 본문이 태그 그대로 보인다. 실제로 비밀번호
                // 재설정 메일이 그렇게 나가고 있었다.
                // ProjMngServer 의 AiTaskNotifier 가 같은 덫에 걸린 적이 있다.
                html = true,
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "/emails/send")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-User-Id", sender);

            using var response = await _http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // **까닭까지 적는다.** 상태 코드만 남기면 「받는 사람이 없다(400)」와
            // 「SMTP 가 거절했다(502)」가 로그에서 같은 줄로 보인다 — 앞엣것은
            // 계정에 적힌 주소를 고칠 일이고 뒤엣것은 메일 서버를 볼 일이라,
            // 가리지 못하면 「안 온다」를 쫓는 자리에서 다시 처음으로 돌아간다.
            // 같은 길로 메일을 보내는 ProjMngServer 의 AI 작업 알림은 처음부터
            // 본문을 읽어 사유로 적어 두고 있었다 — 이쪽만 그러지 않았다.
            var why = await ReasonAsync(response, ct);

            _logger.LogError(
                "메일 발송 실패: HTTP {Status} ({Sender}) — {Why}. 사용자는 「보냈습니다」를 보고 기다리고 있다.",
                (int)response.StatusCode, sender, why);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "메일 발송 예외 ({Sender}) — NotificationServer 가 꺼져 있거나 SMTP 설정이 없을 수 있다.",
                sender);
            return false;
        }
    }

    /// <summary>
    /// 실패한 응답에서 사람이 읽을 사유를 뽑는다.
    /// </summary>
    /// <remarks>
    /// 저쪽은 <c>ApiResponse</c> 꼴(<c>{ "message": "…" }</c>)로 답하므로 그
    /// 한 줄이면 충분하다. JSON 이 아니거나 읽다 실패하면 <b>앞부분만 잘라</b>
    /// 남긴다 — 사유를 뽑다가 던져서 정작 발송 실패 기록이 사라지면 안 된다.
    /// </remarks>
    private static async Task<string> ReasonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string body;

        try
        {
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            return "(응답 본문을 읽지 못했다)";
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return "(응답 본문이 비어 있다)";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("message", out var message)
                && message.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // JSON 이 아니면 아래에서 앞부분만 자른다.
        }

        body = body.Trim();
        return body[..Math.Min(body.Length, 200)];
    }
}
