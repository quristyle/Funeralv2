using System.Text;
using System.Text.Json;

using JSini.Shared.DTOs;

namespace AuthServer.Services;

/// <summary>생일 축하 메시지가 도착하면 받는 사람에게 NotificationServer 웹푸시를 보낸다.</summary>
/// 발송 정책은 NotificationServer 가 맡고 여기서는 push 를 부르기만 한다. 신원은 메시지 저장자 그대로 싣고, 실패해도 저장은 끝난 뒤라 로그만 남긴다.
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

                    // 알림구분. 받는 쪽 알림함이 이 값으로 분류한다(공통코드 NOTI_CATEGORY).
                    category = PushCategories.Birthday,
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
