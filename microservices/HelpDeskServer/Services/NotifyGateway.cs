using System.Net.Http.Json;

namespace HelpDeskServer.Services;

/// <summary>알림 서비스(<c>NotificationServer</c>)에 한 통 건네는 통로. 헬프데스크가 알림 서비스를 부르는 자리가 여럿이다(요청 등록 · 댓글 · 처리 상태).</summary>
public interface INotifyGateway {
  /// <summary>알림 서비스의 <paramref name="path"/> 에 <paramref name="payload"/> 를 보낸다.</summary>
  /// <param name="path">알림 서비스 안의 경로(<c>/notifications/push</c> 따위).</param>
  /// <param name="payload">그대로 JSON 이 되는 값.</param>
  /// <param name="callerId">부른 이로 적을 이름(<c>X-User-Id</c>).</param>
  /// <param name="what">로그에 적을 「무엇을 보냈나」. 사람이 읽는 글이다.</param>
  /// <param name="tag">로그에 함께 적을 꼬리표(요청 번호 따위).</param>
  /// <param name="ct">끊김표.</param>
  /// <returns>보냈으면 참. <b>실패해도 던지지 않는다</b>.</returns>
  Task<bool> PostAsync(
      string path, object payload, string callerId, string what, string tag,
      CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class NotifyGateway : INotifyGateway {
  /// <summary>설정에서 알림 서비스 주소를 읽는 자리.</summary>
  public const string BaseUrlKey = "Notify:BaseUrl";

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IConfiguration _configuration;
  private readonly ILogger<NotifyGateway> _logger;

  /// <summary>서비스를 생성한다.</summary>
  public NotifyGateway(
      IHttpClientFactory httpClientFactory,
      IConfiguration configuration,
      ILogger<NotifyGateway> logger) {
    _httpClientFactory = httpClientFactory;
    _configuration = configuration;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task<bool> PostAsync(
      string path, object payload, string callerId, string what, string tag,
      CancellationToken ct = default) {

    var baseUrl = _configuration[BaseUrlKey] ?? "http://127.0.0.1:5460";

    using var client = _httpClientFactory.CreateClient();
    client.Timeout = TimeSpan.FromSeconds(15);

    using var message = new HttpRequestMessage(
        HttpMethod.Post, $"{baseUrl.TrimEnd('/')}{path}") {
      Content = JsonContent.Create(payload)
    };

    // 게이트웨이를 거치지 않는 서비스 간 호출이라 부른 이를 직접 적는다 —
    // 저쪽은 이 헤더가 없으면 401 로 답한다(`UserContext`).
    message.Headers.Add("X-User-Id", callerId);

    try {
      using var response = await client.SendAsync(message, ct);
      var body = await response.Content.ReadAsStringAsync(ct);

      if (!response.IsSuccessStatusCode) {
        _logger.LogError(
            "{Tag} {What} 발송에 실패했습니다 ({StatusCode}): {Response}",
            tag, what, response.StatusCode, body);
        return false;
      }

      _logger.LogInformation("{Tag} {What} 를 보냈습니다: {Response}", tag, what, body);
      return true;
    }
    catch (HttpRequestException ex) {
      _logger.LogError(ex, "{Tag} {What} — 알림 서비스에 연결할 수 없습니다.", tag, what);
      return false;
    }
    catch (TaskCanceledException ex) {
      _logger.LogError(ex, "{Tag} {What} — 발송 시간이 초과됐습니다.", tag, what);
      return false;
    }
  }
}
