using System.Text;
using System.Text.Json;

using JSini.Shared.DTOs;

namespace AuthServer.Services;

/// <summary>가입 신청이 들어왔다는 것을 관리자에게 앱푸시로 알리는 클라이언트. 메일(<see cref="AccountMailClient"/>)과 짝이다 — 메일은 자리를 비운 관리자에게 남는 기록이고, 푸시는 그 자리에서 바로 승인하게 하는 길이다.</summary>
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

    /// <summary>역할(<c>SYSTEM_ADMINISTRATOR</c>)에게 「가입 신청이 들어왔다」 푸시를 보낸다.</summary>
    /// <param name="role">받을 역할.</param>
    /// <param name="accountId">신청 계정 아이디. 같은 신청의 알림을 한 장으로 겹치는 데 쓴다.</param>
    /// <param name="title">알림 제목.</param>
    /// <param name="body">알림 본문.</param>
    /// <param name="iconOwnerKey">아이콘으로 쓸 사진의 주인(신청자 로그인 아이디).</param>
    /// <param name="sender"><c>X-User-Id</c> 에 실을 기능 이름.</param>
    /// <param name="ct">취소 토큰.</param>
    public async Task NotifyRoleAsync(
        string role, string accountId, string title, string body, string? iconOwnerKey, string sender,
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

                    // 알림구분(공통코드 NOTI_CATEGORY). 기록에만 남고 알림창에는 안 뜬다.
                    category = PushCategories.Signup,
                    iconOwnerKey,
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

    /// <summary>특정 사용자들(로그인 아이디 목록)에게 앱 푸시 알림을 발송합니다.</summary>
    public async Task NotifyOwnersAsync(
        IEnumerable<string> ownerKeys, string title, string body, string? url, string? iconOwnerKey, string sender,
        CancellationToken ct = default)
    {
        try
        {
            var ownersList = ownerKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(k => new { ownerType = "jsini", ownerKey = k })
                .ToList();

            if (ownersList.Count == 0) return;

            var payload = JsonSerializer.Serialize(new
            {
                owners = ownersList,
                message = new
                {
                    title,
                    body,
                    url = url ?? "/admin/system/account",
                    category = "SECURITY",
                    iconOwnerKey,
                    tag = $"notify-{Guid.NewGuid():N}",
                },
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "/notifications/push")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-User-Id", sender);

            using var response = await _http.SendAsync(request, ct);
            if ((int)response.StatusCode == 202)
            {
                _logger.LogInformation("앱 푸시: 대상자 구독 기기가 없었습니다 ({Owners}).", string.Join(",", ownersList.Select(o => o.ownerKey)));
            }
            else if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("앱 푸시 요청 실패: HTTP {Status}", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "앱 푸시 요청 예외 — NotificationServer 가 꺼져 있을 수 있다");
        }
    }
}
