using System.Text;
using System.Text.Json;

namespace AuthServer.Services;

/// <summary>
/// 가입 신청이 들어왔다는 것을 관리자에게 <b>앱푸시</b>로 알리는 클라이언트.
/// </summary>
/// <remarks>
/// <para>
/// 메일(<see cref="AccountMailClient"/>)과 짝이다. 메일은 자리를 비운 관리자에게
/// 남는 기록이고, 푸시는 그 자리에서 곧바로 승인하게 하는 길이다 — 신청자는
/// 승인을 기다리며 화면 앞에 있다.
/// </para>
/// <para>
/// NotificationServer 의 <b>기존</b> <c>/notifications/push</c> 를 부른다.
/// 역할 이름을 그대로 넘기면 그쪽이 사람으로 풀고, 푸시를 끈 사람도 그쪽이
/// 거른다(헬프데스크 요청 알림과 같은 길). 루프백 직접 호출이다.
/// </para>
/// <para>
/// [아이콘은 신청자의 얼굴이다]
/// </para>
/// <para>
/// 공급자가 준 사진(https)을 <c>icon</c> 에 그대로 싣는다. 다른 알림은
/// <c>iconOwnerKey</c> 로 계정 사진을 찾게 하지만, 신청자는 아직 계정 사진이
/// 없다 — 그 길로 보내면 언제나 기본 그림이 나온다. 사진이 없으면 그쪽
/// 기본값(앱 아이콘)에 맡긴다.
/// </para>
/// <para>
/// 실패해도 신청은 이미 저장되었고 메일도 따로 간다. 그래서 로그만 남긴다.
/// </para>
/// </remarks>
public class SignupNotifyClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SignupNotifyClient> _logger;

    public SignupNotifyClient(HttpClient http, IConfiguration configuration, ILogger<SignupNotifyClient> logger)
    {
        _http = http;
        _logger = logger;

        var baseUrl = configuration["Notify:BaseUrl"] ?? "http://127.0.0.1:5460";
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// 역할(<c>SYSTEM_ADMINISTRATOR</c>)에게 「가입 신청이 들어왔다」 푸시를 보낸다.
    /// </summary>
    /// <param name="role">받을 역할.</param>
    /// <param name="accountId">신청 계정 아이디. 같은 신청의 알림을 한 장으로 겹치는 데 쓴다.</param>
    /// <param name="title">알림 제목.</param>
    /// <param name="body">알림 본문.</param>
    /// <param name="iconUrl">아이콘으로 쓸 사진(https). 없으면 <c>null</c>.</param>
    /// <param name="sender"><c>X-User-Id</c> 에 실을 기능 이름.</param>
    /// <param name="ct">취소 토큰.</param>
    public async Task NotifyRoleAsync(
        string role, string accountId, string title, string body, string? iconUrl, string sender,
        CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                roles = new[] { role },
                message = new
                {
                    title,
                    body,

                    // 누르면 곧바로 승인 화면으로 간다.
                    url = "/admin/system/signup",
                    icon = iconUrl,
                    tag = $"signup-{accountId}",
                },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "/notifications/push")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-User-Id", sender);

            using var response = await _http.SendAsync(request, ct);

            // 202 = 받을 기기가 하나도 없었다(구독이 없거나 모두 껐다). 고장이 아니다.
            if ((int)response.StatusCode == 202)
            {
                _logger.LogInformation("가입 신청 푸시: 받을 기기가 없었다 ({Role}).", role);
            }
            else if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("가입 신청 푸시 요청 실패: HTTP {Status} ({Role})", (int)response.StatusCode, role);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "가입 신청 푸시 요청 예외 — NotificationServer 가 꺼져 있을 수 있다");
        }
    }
}
