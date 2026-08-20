using System.Collections.Concurrent;
using System.Text.Json;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using HelpDeskServer.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;
using HtmlAgilityPack;

namespace HelpDeskServer.Services;

/// <summary>
/// Web Push 구독 정보를 관리하는 저장소 인터페이스
/// </summary>
public interface IPushSubscriptionStore {
  /// <summary>
  /// 새로운 구독 정보를 추가합니다.
  /// </summary>
  /// <param name="dto">구독 정보 DTO</param>
  /// <param name="userId">사용자 ID</param>
  /// <param name="userType">사용자 타입 ("Admin" 또는 "Customer")</param>
  void Add(PushSubscriptionDto dto, int userId, string userType);
  /// <summary>
  /// Endpoint URL을 기준으로 구독 정보를 삭제합니다.
  /// </summary>
  /// <param name="endpoint">삭제할 구독의 Endpoint URL</param>
  Task RemoveByEndpointAsync(string endpoint);
  /// <summary>
  /// Endpoint URL을 기준으로 구독 여부를 확인합니다.
  /// </summary>
  /// <param name="endpoint">확인할 구독의 Endpoint URL</param>
  /// <returns>구독 중이면 true, 아니면 false</returns>
  Task<bool> IsSubscribedAsync(string endpoint);
  /// <summary>
  /// 특정 사용자의 모든 구독 정보를 조회합니다.
  /// </summary>
  /// <param name="userId">사용자 ID</param>
  /// <param name="userType">사용자 타입</param>
  Task<IReadOnlyCollection<Models.PushSubscription>> GetSubscriptionsByUserAsync(int userId, string userType);
  /// <summary>
  /// 특정 고객사에 속한 모든 사용자의 구독 정보를 조회합니다.
  /// </summary>
  /// <param name="companyId">고객사 ID</param>
  Task<IReadOnlyCollection<Models.PushSubscription>> GetSubscriptionsByCompanyAsync(int companyId);
  /// <summary>
  /// 특정 팀에 속한 모든 관리자의 구독 정보를 조회합니다.
  /// </summary>
  /// <param name="teamId">팀 ID</param>
  Task<IReadOnlyCollection<Models.PushSubscription>> GetSubscriptionsByTeamAsync(int teamId);
  /// <summary>
  /// 모든 관리자의 구독 정보를 조회합니다.
  /// </summary>
  Task<IReadOnlyCollection<Models.PushSubscription>> GetAdminSubscriptionsAsync();
  /// <summary>
  /// 모든 구독 정보를 조회합니다.
  /// </summary>
  Task<IReadOnlyCollection<Models.PushSubscription>> GetAllAsync();
}

/// <summary>
/// 데이터베이스를 사용하여 Web Push 구독 정보를 영구적으로 관리하는 클래스
/// </summary>
public sealed class DbPushSubscriptionStore : IPushSubscriptionStore {
  private readonly AppDbContext _db;

  /// <summary>
  /// 생성자
  /// </summary>
  /// <param name="db">데이터베이스 컨텍스트</param>
  public DbPushSubscriptionStore(AppDbContext db) {
    _db = db;
  }

  /// <inheritdoc />
  public void Add(PushSubscriptionDto dto, int userId, string userType) {
    if (string.IsNullOrWhiteSpace(dto.Endpoint) || dto.Keys is null ||
        string.IsNullOrWhiteSpace(dto.Keys.P256dh) || string.IsNullOrWhiteSpace(dto.Keys.Auth)) {
      throw new ArgumentException("잘못된 구독 페이로드입니다.");
    }

    var existing = _db.PushSubscriptions.FirstOrDefault(s => s.Endpoint == dto.Endpoint);
    if (existing is null) {
      _db.PushSubscriptions.Add(new Models.PushSubscription {
        Endpoint = dto.Endpoint,
        P256dh = dto.Keys.P256dh,
        Auth = dto.Keys.Auth,
        UserId = userId,
        UserType = userType
      });
      _db.SaveChanges();
    }
  }

  /// <inheritdoc />
  public async Task RemoveByEndpointAsync(string endpoint) {
    if (string.IsNullOrWhiteSpace(endpoint)) return;
    var sub = await _db.PushSubscriptions.FindAsync(endpoint);
    if (sub is not null) {
      _db.PushSubscriptions.Remove(sub);
      await _db.SaveChangesAsync();
    }
  }

  /// <inheritdoc />
  public async Task<IReadOnlyCollection<Models.PushSubscription>> GetAllAsync() {
    return await _db.PushSubscriptions.ToListAsync();
  }

  /// <inheritdoc />
  public async Task<IReadOnlyCollection<Models.PushSubscription>> GetAdminSubscriptionsAsync() {
    var subs = await _db.PushSubscriptions
        .Where(s => s.UserType == "admin") // "Admin"으로 대소문자 일치
        .Include(s => s.Admin) // Admin 객체를 함께 로드
        .ToListAsync();
    return subs;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyCollection<Models.PushSubscription>> GetSubscriptionsByTeamAsync(int teamId) {
    // 1. 지정된 팀 ID에 속한 모든 관리자의 ID를 조회합니다.
    var adminIdsInTeam = await _db.Admins
        .Where(a => a.AdminTeams.Any(at => at.TeamId == teamId))
        .Select(a => a.Id)
        .ToListAsync();

    if (!adminIdsInTeam.Any()) return new List<Models.PushSubscription>();

    // 2. 조회된 관리자 ID 목록을 사용하여 해당 관리자들의 구독 정보를 가져옵니다.
    var subs = await _db.PushSubscriptions
        .Where(s => s.UserType == "Admin" && adminIdsInTeam.Contains(s.UserId))
        .ToListAsync();
    return subs;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyCollection<Models.PushSubscription>> GetSubscriptionsByCompanyAsync(int companyId) {
    // 1. 지정된 고객사 ID에 속한 모든 고객(사용자)의 ID를 조회합니다.
    var customerIdsInCompany = await _db.Customers
        .Where(c => c.CompanyId == companyId)
        .Select(c => c.Id)
        .ToListAsync();

    if (!customerIdsInCompany.Any()) return new List<Models.PushSubscription>();

    // 2. 조회된 고객 ID 목록을 사용하여 해당 고객들의 구독 정보를 가져옵니다.
    var subs = await _db.PushSubscriptions
        .Where(s => s.UserType == "Customer" && customerIdsInCompany.Contains(s.UserId))
        .Include(s => s.Customer) // Customer 객체를 함께 로드
        .ToListAsync();
    return subs;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyCollection<Models.PushSubscription>> GetSubscriptionsByUserAsync(int userId, string userType) {
    var query = _db.PushSubscriptions
        .Where(s => s.UserId == userId && s.UserType == userType);

    // 사용자 타입에 따라 적절한 탐색 속성을 Include 합니다.
    if (userType == "admin") {
      query = query.Include(s => s.Admin);
    }
    else if (userType == "customer") {
      query = query.Include(s => s.Customer);
    }

    var subs = await query.ToListAsync();
    return subs;
  }

  /// <inheritdoc />
  public async Task<bool> IsSubscribedAsync(string endpoint) {
    if (string.IsNullOrWhiteSpace(endpoint)) {
      return false;
    }
    return await _db.PushSubscriptions.AnyAsync(s => s.Endpoint == endpoint);
  }
}

/// <summary>
/// Web Push 알림 발송을 처리하는 서비스 인터페이스
/// </summary>
public interface IWebPushService {
  /// <summary>
  /// 단일 구독자에게 푸시 알림을 보냅니다.
  /// </summary>
  /// <param name="subscription"></param>
  /// <param name="message"></param>
  /// <param name="recipientId"></param>
  /// <param name="ct"></param>
  /// <returns></returns>
  Task SendAsync(WebPush.PushSubscription subscription, PushMessageDto message, int? recipientId, CancellationToken ct = default);
  /// <summary>
  /// 여러 구독자에게 푸시 알림을 브로드캐스트합니다.
  /// </summary>
  /// <param name="subscriptions">대상 구독 정보 목록</param>
  /// <param name="message">보낼 메시지 내용</param>
  /// <param name="ct">취소 토큰</param>
  /// <returns>성공적으로 발송된 알림의 수</returns>
  Task<int> BroadcastAsync(IEnumerable<Models.PushSubscription> subscriptions, PushMessageDto message, CancellationToken ct = default);
}

/// <summary>
/// Web Push 알림 발송을 처리하는 서비스 구현 클래스
/// </summary>
public sealed class WebPushService : IWebPushService {
  private readonly VapidOptions _vapid;
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly ILogger<WebPushService> _logger;

  /// <summary>
  /// 생성자
  /// </summary>
  /// <param name="options">VAPID 키 옵션</param>
  /// <param name="scopeFactory">서비스 스코프 팩토리</param>
  /// <param name="logger">로거</param>
  public WebPushService(IOptions<VapidOptions> options, IServiceScopeFactory scopeFactory, ILogger<WebPushService> logger) {
    _vapid = options.Value;
    _scopeFactory = scopeFactory;
    _logger = logger;
  }

  private VapidDetails GetVapid() {
    if (string.IsNullOrWhiteSpace(_vapid.PublicKey) || string.IsNullOrWhiteSpace(_vapid.PrivateKey)) {
      throw new InvalidOperationException("VAPID keys are not configured. Configure Vapid:PublicKey and Vapid:PrivateKey");
    }
    var subject = string.IsNullOrWhiteSpace(_vapid.Subject) ? "quristyle@jinnets.co.kr" : _vapid.Subject;
    return new VapidDetails(subject, _vapid.PublicKey, _vapid.PrivateKey);
  }

  /// <inheritdoc />
  public async Task SendAsync(WebPush.PushSubscription subscription, PushMessageDto message, int? recipientId, CancellationToken ct = default) {
    var payload = JsonSerializer.Serialize(new {
      title = message.Title ?? "Notification",
      body = message.Body ?? string.Empty,
      icon = message.Icon ?? "/icons/icon-192.png",
      url = message.Url,
      recipientId // 수신자 ID를 페이로드에 추가
    });

    var client = new WebPushClient();
    var vapid = GetVapid();

    await client.SendNotificationAsync(subscription, payload, vapid, ct);
  }


  private static string StripHtml(string html) {
    if (string.IsNullOrWhiteSpace(html)) return string.Empty;
    var doc = new HtmlDocument();
    doc.LoadHtml(html);
    return doc.DocumentNode.InnerText;
  }




  /// <inheritdoc />
  public async Task<int> BroadcastAsync(IEnumerable<Models.PushSubscription> subscriptions, PushMessageDto message, CancellationToken ct = default) {
    var success = 0;
    if (!subscriptions.Any()) {
      return 0;
    }


    var descriptionText = StripHtml(message.Body);
    if (descriptionText.Length > 50) {
      descriptionText = descriptionText.Substring(0, 50) + "...";
    }

    // 1. 메시지 정보 DB에 저장
    var pushMessage = new PushMessage {
      Title = message.Title ?? "Notification",
      Body = message.Body ?? string.Empty,
      Url = message.Url,
      CreatedAt = DateTime.UtcNow
    };
    // DB에 PushMessage를 먼저 저장하여 ID를 확보합니다.
    // 아직 Recipient가 없으므로 Add만 하고 SaveChanges는 루프 이후에 합니다.
    using var scope = _scopeFactory.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    foreach (var sub in subscriptions) {
      // 2. 수신자 정보와 함께 DB에 저장
      var recipient = new PushMessageRecipient {
        PushMessage = pushMessage, // 참조 설정
        UserId = sub.UserId,
        UserType = sub.UserType,
        Endpoint = sub.Endpoint,
        IsDelivered = false, // 기본값
        CreatedAt = DateTime.UtcNow
      };
      pushMessage.Recipients.Add(recipient);

      // 루프 내에서 각 Recipient를 먼저 저장하여 ID를 생성합니다.
      dbContext.PushMessageRecipients.Add(recipient);
      await dbContext.SaveChangesAsync(ct);



      try {
        var webPushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
        // SendAsync에 recipient.Id를 전달합니다.
        await SendAsync(webPushSubscription, message, recipient.Id, ct);

        await LogPushAttemptAsync(sub.Endpoint, true, null);
        // IsDelivered는 이제 클라이언트의 delivered 엔드포인트 호출로 처리되므로 여기서 true로 설정하지 않습니다.
        success++;
      }
      // 3. 예외 처리 및 상태 기록
      catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound) {
        _logger.LogInformation("만료된 구독을 삭제합니다: {Endpoint}", sub.Endpoint);
        await LogPushAttemptAsync(sub.Endpoint, false, $"Expired subscription: {ex.StatusCode}");
        // 구독 정보 삭제는 백그라운드에서 처리
        _ = Task.Run(async () => {
          using var scope = _scopeFactory.CreateScope();
          var store = scope.ServiceProvider.GetRequiredService<IPushSubscriptionStore>();
          await store.RemoveByEndpointAsync(sub.Endpoint);
        }, ct);
      }
      catch (WebPushException ex) {
        await LogPushAttemptAsync(sub.Endpoint, false, $"WebPushException: {ex.StatusCode}");
        _logger.LogError(ex, "WebPushException 발생 (StatusCode: {StatusCode}). Endpoint: {Endpoint}", ex.StatusCode, sub.Endpoint);
      }
      catch (Exception ex) {
        await LogPushAttemptAsync(sub.Endpoint, false, $"Generic Exception: {ex.GetType().Name}");
        _logger.LogError(ex, "알림 발송 중 예상치 못한 오류 발생. Endpoint: {Endpoint}", sub.Endpoint);
      }
    }
    return success;
  }

  private async Task LogPushAttemptAsync(string endpoint, bool isSuccess, string? failureReason) {
    // 로깅은 fire-and-forget으로 처리하여 알림 발송 흐름을 막지 않도록 합니다.
    _ = Task.Run(async () => {
      using var scope = _scopeFactory.CreateScope();
      var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      dbContext.PushNotificationLogs.Add(new PushNotificationLog {
        Endpoint = endpoint,
        IsSuccess = isSuccess,
        FailureReason = failureReason,
        CreatedAt = DateTime.UtcNow
      });
      await dbContext.SaveChangesAsync();
    });
  }
}
