using System.Text;
using System.Text.Json;

namespace AuthServer.Services;

/// <summary>계정 관련 안내 메일을 NotificationServer 로 내보내는 클라이언트. 루프백으로 직접 부르며 신원은 X-User-Id 헤더에 서비스 이름을 싣는다.
/// 실패를 삼키지 않는다 — 재설정 메일이 못 나가도 화면은 「보냈습니다」를 띄우므로(계정 존재를 숨기려고), 결과를 돌려주고 부르는 쪽이 오류로 로그에 남긴다.</summary>
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
    /// <param name="sender"><c>X-User-Id</c> 에 실을 이름. 사람이 아니라 어느 기능이 보냈는지를 적는다(<c>AUTH_PASSWORD_RESET</c>).</param>
    /// <param name="eventCode">알림 이벤트 코드(<c>NotificationEvents</c>). 알림관리가 이 글자로 역할·채널을 가른다. 비우면 정책이 안 걸린다.</param>
    /// <param name="ct">보내는 도중 취소할 때 쓰는 토큰</param>
    public async Task<bool> SendAsync(
        string to, string subject, string body, string sender,
        string? eventCode = null, CancellationToken ct = default)
        => (await PostAsync(to, null, subject, body, sender, eventCode, ct)).Ok;

    /// <summary>주소 하나로 보내고 실패 사유까지 돌려준다. 보고서 「미리받아보기」가 쓴다 — 누른 사람 본인에게만 가므로 역할을 거치지 않으며, 누른 사람이 지금 화면을 보고 있어 「주소가 없다」와 「메일 서버가 거절했다」를 가릴 수 있어야 한다.</summary>
    public Task<(bool Ok, string Reason)> SendToAddressAsync(
        string to, string subject, string body, string sender,
        string? eventCode = null, CancellationToken ct = default)
        => PostAsync(to, null, subject, body, sender, eventCode, ct);

    /// <summary>역할을 받는 사람으로 보낸다 (<c>SYSTEM_ADMINISTRATOR</c>). 받는 주소는 NotificationServer 가 그 역할의 대표 이메일로 풀어 준다 — 관리자 주소를 설정 파일에 두지 않는 이유다.</summary>
    public async Task<bool> SendToRoleAsync(
        string role, string subject, string body, string sender,
        string? eventCode = null, CancellationToken ct = default)
        => (await PostAsync(null, role, subject, body, sender, eventCode, ct)).Ok;

    /// <summary>역할 여럿을 받는 사람으로 보내고 실패 사유까지 돌려준다. 보고서 배치가 쓴다 — 역할마다 따로 보내면 두 역할에 걸린 사람이 두 통을 받으므로 쉼표로 이어 한 번에 보낸다.
    /// 사유를 주는 것은 배치 화면이 「메일이 안 와요」에 답해야 하기 때문이다.</summary>
    public Task<(bool Ok, string Reason)> SendToRolesAsync(
        IEnumerable<string> roles, string subject, string body, string sender,
        string? eventCode = null, CancellationToken ct = default)
        => PostAsync(null, string.Join(",", roles), subject, body, sender, eventCode, ct);

    /// <summary>실제로 보낸다. 성공 여부와 사유를 함께 돌려준다 — 사유를 버리면 「받는 사람이 없다(400)」와 「메일 서버가 거절했다(502)」가 같은 줄이 된다.</summary>
    private async Task<(bool Ok, string Reason)> PostAsync(
        string? to, string? toRole, string subject, string body, string sender,
        string? eventCode, CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                to = to ?? string.Empty,
                toRole,
                subject,
                body,

                // 알림관리가 이 글자로 가른다 — 역할로 보내는 몫은 정책이 바꿀 수 있고, 비우면 정책이 안 걸린 채 그대로 나간다.
                eventCode,

                // 속성 이름은 isHtml 이 아니라 Html 이다 — 이름이 안 맞으면 조용히 기본값(false)이 쓰여 본문이 태그 그대로 보인다.
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
                // 200 이 곧 발송은 아니다 — 저쪽은 「받는 사람이 모두 꺼 둠」·「이벤트를 끔」을 200 과 data: false 로 답하므로, 상태 코드만 보면 안 나간 메일이 「보냈습니다」가 된다.
                return await OutcomeAsync(response, ct);
            }

            // 상태 코드만 남기면 「받는 사람이 없다(400)」와 「SMTP 가 거절했다(502)」가 같아 보이므로 사유를 함께 적는다.
            var why = await ReasonAsync(response, ct);

            _logger.LogError(
                "메일 발송 실패: HTTP {Status} ({Sender}) — {Why}. 사용자는 「보냈습니다」를 보고 기다리고 있다.",
                (int)response.StatusCode, sender, why);
            return (false, why);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "메일 발송 예외 ({Sender}) — NotificationServer 가 꺼져 있거나 SMTP 설정이 없을 수 있다.",
                sender);
            return (false, $"알림 서버에 닿지 못했습니다 — {ex.Message}");
        }
    }

    /// <summary>200 으로 온 응답이 정말로 보냈다는 뜻인지 가른다. 「꺼 둠」도 200 + <c>data: false</c> 로 오므로 data 가 false 면 안 나간 것이다. data 를 못 읽으면 보낸 것으로 본다 — 실패로 적으면 멀쩡히 나간 메일이 「실패」로 남는다.</summary>
    private static async Task<(bool Ok, string Reason)> OutcomeAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        string body;

        try
        {
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            return (true, "보냈습니다.");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.False)
            {
                var why = doc.RootElement.TryGetProperty("message", out var message)
                          && message.ValueKind == JsonValueKind.String
                    ? message.GetString()
                    : null;

                return (false, string.IsNullOrWhiteSpace(why) ? "보내지 않았습니다." : why);
            }
        }
        catch (JsonException)
        {
            // JSON 이 아니면 근거가 없으니 보낸 것으로 본다.
        }

        return (true, "보냈습니다.");
    }

    /// <summary>실패한 응답에서 사람이 읽을 사유를 뽑는다. JSON 이 아니거나 읽다 실패하면 앞부분만 잘라 남긴다 — 던지면 발송 실패 기록이 사라진다.</summary>
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
