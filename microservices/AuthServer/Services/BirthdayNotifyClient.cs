using System.Text;
using System.Text.Json;

namespace AuthServer.Services;

/// <summary>
/// 생일 축하 메시지 도착을 NotificationServer 웹푸시로 알리는 클라이언트 (결정 D-G1a · 2026-09-04).
/// </summary>
/// <remarks>
/// <para>
/// GHUB 원본은 축하 메시지가 등록되면 받는 사람에게 발송(푸시·알림톡)까지 했다.
/// 사용자 결정으로 발송은 NotificationServer 가 맡는다 — 여기는 그쪽의 **기존**
/// <c>/notifications/push</c> 를 부를 뿐, 새 기능이 필요 없다(받는 사람이 명확해서
/// 기상처럼 구독을 뒤질 일이 없다). 푸시를 끈 사람은 그쪽 발송기가 거른다.
/// </para>
/// <para>
/// 루프백 직접 호출(게이트웨이 미경유)이고, 신원은 메시지를 보낸 사용자의 아이디를
/// 그대로 싣는다. 실패해도 메시지 저장은 이미 끝났으므로 로그만 남긴다.
/// </para>
/// </remarks>
public class BirthdayNotifyClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BirthdayNotifyClient> _logger;
    private readonly bool _enabled;

    public BirthdayNotifyClient(HttpClient http, IConfiguration configuration, ILogger<BirthdayNotifyClient> logger)
    {
        _http = http;
        _logger = logger;
        _enabled = configuration.GetValue("Notify:BirthdayEnabled", true);
        var baseUrl = configuration["Notify:BaseUrl"] ?? "http://127.0.0.1:5460";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>받는 사람에게 "축하 메시지가 왔다" 웹푸시를 보낸다.</summary>
    public async Task NotifyAsync(string recipientId, string senderId, string senderName)
    {
        if (!_enabled) return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                owners = new[] { new { ownerType = "jsini", ownerKey = recipientId } },
                message = new
                {
                    title = "생일 축하 메시지가 도착했어요 🎂",
                    body = $"{(string.IsNullOrWhiteSpace(senderName) ? senderId : senderName)} 님이 축하 메시지를 보냈습니다.",
                    url = "/life/birthday",
                },
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, "/notifications/push")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-User-Id", senderId);

            using var response = await _http.SendAsync(request);
            // 202 = 구독이 없어 보낸 것이 없음 — 오류가 아니다 (받는 사람이 푸시를 안 켰을 뿐)
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 202)
            {
                _logger.LogWarning("생일 축하 푸시 요청 실패: HTTP {Status}", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "생일 축하 푸시 요청 예외 — NotificationServer 가 꺼져 있을 수 있다");
        }
    }
}
