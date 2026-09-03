using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NotificationServer.Endpoints;

namespace NotificationServer.Services;

/// <summary>카카오 알림톡 발송 (결정 D-G1b · 기본 꺼짐)</summary>
public interface IKakaoAlimtalkSender
{
    /// <summary>기상 이벤트를 알림톡으로 보낸다. 꺼져 있거나 수신 번호가 없으면 0.</summary>
    Task<int> SendWeatherAsync(WeatherEventDto ev, CancellationToken ct = default);
}

/// <summary>
/// 비즈뿌리오 알림톡 설정. **기본 꺼짐** — 켜려면 세 가지가 먼저 필요하다.
///   1) 비즈뿌리오 계정·발신프로필(SenderKey)·승인된 템플릿 코드 (자격증명은 Local.json 에만)
///   2) 수신 전화번호의 출처 — 포털 계정에는 아직 전화번호 칸이 없어
///      당분간 Recipients 목록으로 직접 준다
///   3) 템플릿 본문과 코드가 일치해야 한다 — 알림톡은 승인된 템플릿과 다른 본문을 거부한다
/// </summary>
public class KakaoOptions
{
    public const string SectionName = "Kakao";

    public bool Enabled { get; set; }
    public string ApiUrl { get; set; } = "https://api.bizppurio.com";
    /// <summary>비즈뿌리오 계정 (Local.json 에만)</summary>
    public string Account { get; set; } = string.Empty;
    /// <summary>비즈뿌리오 비밀번호 (Local.json 에만)</summary>
    public string Password { get; set; } = string.Empty;
    /// <summary>발신 번호</summary>
    public string From { get; set; } = string.Empty;
    /// <summary>카카오 발신프로필 키</summary>
    public string SenderKey { get; set; } = string.Empty;
    /// <summary>기상 알림용 승인 템플릿 코드</summary>
    public string WeatherTemplateCode { get; set; } = string.Empty;
    /// <summary>수신 전화번호 목록 (하이픈 없이). 포털 계정에 전화번호가 생기면 그쪽으로 옮긴다.</summary>
    public string[] Recipients { get; set; } = Array.Empty<string>();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Account) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(From) &&
        !string.IsNullOrWhiteSpace(SenderKey) &&
        !string.IsNullOrWhiteSpace(WeatherTemplateCode);
}

/// <summary>
/// 비즈뿌리오 REST 이식판. 전송 규약은 GHUB 원본(skgRestApi/Utilities/KakaoUtil.cs)과 같다 —
/// <c>/v1/token</c>(Basic) 으로 토큰을 받고 <c>/v3/message</c>(Bearer, type=at) 로 보낸다.
/// 원본의 토큰 캐시(만료 전 재사용)도 그대로 가져왔다.
/// </summary>
public class BizppurioAlimtalkSender : IKakaoAlimtalkSender
{
    private readonly HttpClient _http;
    private readonly KakaoOptions _options;
    private readonly ILogger<BizppurioAlimtalkSender> _logger;

    private static string? _cachedToken;
    private static DateTime _tokenExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public BizppurioAlimtalkSender(
        HttpClient http,
        IOptions<KakaoOptions> options,
        ILogger<BizppurioAlimtalkSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> SendWeatherAsync(WeatherEventDto ev, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return 0; // 기본 꺼짐 — 로그도 남기지 않는다 (기상 이벤트마다 시끄러워진다)
        }
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("알림톡이 켜져 있지만 설정이 비었다 — Local.json 의 Kakao 절을 확인할 것");
            return 0;
        }
        if (_options.Recipients.Length == 0)
        {
            _logger.LogWarning("알림톡이 켜져 있지만 수신 번호(Kakao:Recipients)가 없다");
            return 0;
        }

        var token = await GetTokenAsync(ct);
        if (token is null) return 0;

        // 주의: 알림톡은 승인된 템플릿과 본문이 정확히 일치해야 한다.
        // 이 본문 형식으로 템플릿을 승인받거나, 승인받은 템플릿에 이 형식을 맞춘다.
        var messageText = $"[기상] {ev.StandardName}\n{ev.Location} · 측정 {ev.MeasuredValue:0.#}{ev.Unit} — 기준 충족";
        var sent = 0;

        foreach (var to in _options.Recipients)
        {
            try
            {
                var payload = new
                {
                    account = _options.Account,
                    type = "at",
                    from = _options.From,
                    to,
                    country = "82",
                    content = new Dictionary<string, object>
                    {
                        ["at"] = new
                        {
                            senderkey = _options.SenderKey,
                            templatecode = _options.WeatherTemplateCode,
                            message = messageText,
                        },
                    },
                    refkey = $"weather-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40],
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiUrl}/v3/message");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var response = await _http.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    sent++;
                }
                else
                {
                    _logger.LogWarning("알림톡 발송 실패 ({To}): {Status} {Body}", to, (int)response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "알림톡 발송 예외 ({To})", to);
            }
        }

        return sent;
    }

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry) return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiry) return _cachedToken;

            var authValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.Account}:{_options.Password}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiUrl}/v1/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("비즈뿌리오 토큰 발급 실패: {Status}", (int)response.StatusCode);
                return null;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body);
            if (string.IsNullOrEmpty(tokenResponse?.AccessToken))
            {
                _logger.LogWarning("비즈뿌리오 토큰 응답이 비었다");
                return null;
            }

            _cachedToken = tokenResponse.AccessToken;
            // 원본과 같이 만료보다 조금 일찍 갱신한다 (응답의 expired 파싱 실패 시 20시간)
            _tokenExpiry = DateTime.UtcNow.AddHours(20);
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("accesstoken")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = "Bearer";
        [JsonPropertyName("expired")] public string Expired { get; set; } = string.Empty;
    }
}
