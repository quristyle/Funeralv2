using System.Security.Claims;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HelpDeskServer.Services;

/// <summary>
/// funeralv2 계정 ↔ 헬프데스크 계정 자동 매칭 정책.
/// </summary>
public class AccountLinkOptions {
  /// <summary>설정 섹션 이름.</summary>
  public const string SectionName = "AccountLink";

  /// <summary>
  /// 로그인 아이디가 같으면 같은 사람으로 간주할지 여부. 기본 false.
  /// 아이디가 겹치지만 서로 다른 사람인 계정이 있으면 남의 계정으로 붙어버리므로 함부로 켜지 않는다.
  /// </summary>
  public bool MatchByLoginId { get; set; }

  /// <summary>
  /// 이메일이 같으면 같은 사람으로 간주할지 여부. 기본 false.
  ///
  /// 원래 기본값은 true 였지만 실제로는 한 번도 동작하지 않았다 — 포털 토큰에 이메일 클레임이
  /// 없어서 대조할 값 자체가 없었다. 토큰에 이메일을 싣게 되면서 비로소 동작하게 되었는데,
  /// 그대로 켜 두면 지금까지 명시적 연결로만 움직이던 신원 해석이 조용히 달라진다.
  ///
  /// 실제 데이터에도 위험이 있다. 포털 계정 quristyle(사용자A)의 이메일이
  /// 헬프데스크 <b>고객</b> 3번(사용자H)의 이메일과 같다. 명시적 연결이 있어 지금은 가려지지만,
  /// 연결이 없는 계정이라면 남의 고객 계정으로 붙는다.
  ///
  /// 그래서 아이디 대조와 같은 규칙을 적용한다 — <b>추정하지 않는다.</b>
  /// 데이터가 정리된 환경에서만 켠다.
  /// </summary>
  public bool MatchByEmail { get; set; }
}

/// <summary>
/// funeralv2 계정으로 해석된 헬프데스크 사용자.
/// </summary>
/// <param name="UserType">admin 또는 customer</param>
/// <param name="HelpdeskUserId">헬프데스크 내부 계정 ID</param>
/// <param name="CompanyId">고객인 경우 소속 회사 ID</param>
/// <param name="UserName">표시용 이름</param>
public record HelpdeskIdentity(string UserType, int HelpdeskUserId, int? CompanyId, string UserName);

/// <summary>
/// funeralv2(AuthServer) 계정을 헬프데스크 계정으로 해석한다.
/// </summary>
public interface IFuneralAccountLinkService {
  /// <summary>AuthServer 사용자 식별자로 헬프데스크 계정을 찾는다. 못 찾으면 null.</summary>
  Task<HelpdeskIdentity?> ResolveAsync(string authUserId, string? email, CancellationToken ct = default);

  /// <summary>매핑 캐시를 비운다. 매핑을 추가/삭제한 직후에 호출한다.</summary>
  void InvalidateCache(string authUserId);
}

/// <inheritdoc />
public class FuneralAccountLinkService : IFuneralAccountLinkService {
  private readonly AppDbContext _db;
  private readonly IMemoryCache _cache;
  private readonly ILogger<FuneralAccountLinkService> _logger;
  private readonly AccountLinkOptions _options;

  private static string CacheKey(string authUserId) => $"helpdesk:authlink:{authUserId}";

  /// <summary>서비스를 생성한다.</summary>
  public FuneralAccountLinkService(
      AppDbContext db,
      IMemoryCache cache,
      ILogger<FuneralAccountLinkService> logger,
      IOptions<AccountLinkOptions> options) {
    _db = db;
    _cache = cache;
    _logger = logger;
    _options = options.Value;
  }

  /// <inheritdoc />
  public void InvalidateCache(string authUserId) => _cache.Remove(CacheKey(authUserId));

  /// <inheritdoc />
  public async Task<HelpdeskIdentity?> ResolveAsync(string authUserId, string? email, CancellationToken ct = default) {
    if (string.IsNullOrWhiteSpace(authUserId)) return null;

    if (_cache.TryGetValue<HelpdeskIdentity?>(CacheKey(authUserId), out var cached)) {
      return cached;
    }

    var resolved = await ResolveFromDatabaseAsync(authUserId, email, ct);

    // 못 찾은 경우도 캐싱한다. 매핑이 없는 계정이 매 요청마다 DB 를 3번씩 뒤지는 것을 막는다.
    _cache.Set(CacheKey(authUserId), resolved, TimeSpan.FromMinutes(resolved is null ? 1 : 10));
    return resolved;
  }

  private async Task<HelpdeskIdentity?> ResolveFromDatabaseAsync(string authUserId, string? email, CancellationToken ct) {
    // 1순위: 명시적으로 등록된 매핑
    var link = await _db.AuthUserLinks.AsNoTracking()
        .FirstOrDefaultAsync(l => l.AuthUserId == authUserId, ct);

    if (link is not null) {
      var byLink = await LoadAsync(link.UserType, link.HelpdeskUserId, ct);
      if (byLink is not null) return byLink;

      _logger.LogWarning(
          "auth_user_links 에 {AuthUserId} → {UserType}#{Id} 매핑이 있으나 대상 계정을 찾지 못했습니다.",
          authUserId, link.UserType, link.HelpdeskUserId);
    }

    // 2순위: 로그인 아이디가 같은 계정.
    // 기본값은 끔. 아이디만 같고 실제로는 다른 사람인 경우가 있어(운영 데이터에서 확인됨)
    // 자동으로 이어주면 남의 계정으로 로그인되는 사고가 난다. 데이터가 정리된 환경에서만 켠다.
    if (_options.MatchByLoginId) {
      var adminByLoginId = await _db.Admins.AsNoTracking()
          .FirstOrDefaultAsync(a => a.LoginId == authUserId, ct);
      if (adminByLoginId is not null) {
        return new HelpdeskIdentity("admin", adminByLoginId.Id, null, adminByLoginId.UserName);
      }

      var customerByLoginId = await _db.Customers.AsNoTracking()
          .FirstOrDefaultAsync(c => c.LoginId == authUserId, ct);
      if (customerByLoginId is not null) {
        return new HelpdeskIdentity("customer", customerByLoginId.Id, customerByLoginId.CompanyId, customerByLoginId.UserName);
      }
    }

    // 3순위: 이메일 일치. 아이디 대조보다는 낫지만 이것도 추정이다.
    // 실제 데이터에 같은 이메일을 쓰는 다른 사람이 있어 기본은 꺼 둔다(AccountLinkOptions 참고).
    if (_options.MatchByEmail && !string.IsNullOrWhiteSpace(email)) {
      var adminByEmail = await _db.Admins.AsNoTracking()
          .FirstOrDefaultAsync(a => a.Email == email, ct);
      if (adminByEmail is not null) {
        return new HelpdeskIdentity("admin", adminByEmail.Id, null, adminByEmail.UserName);
      }

      var customerByEmail = await _db.Customers.AsNoTracking()
          .FirstOrDefaultAsync(c => c.Email == email, ct);
      if (customerByEmail is not null) {
        return new HelpdeskIdentity("customer", customerByEmail.Id, customerByEmail.CompanyId, customerByEmail.UserName);
      }
    }

    return null;
  }

  private async Task<HelpdeskIdentity?> LoadAsync(string userType, int id, CancellationToken ct) {
    if (string.Equals(userType, "admin", StringComparison.OrdinalIgnoreCase)) {
      var admin = await _db.Admins.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
      return admin is null ? null : new HelpdeskIdentity("admin", admin.Id, null, admin.UserName);
    }

    var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    return customer is null ? null : new HelpdeskIdentity("customer", customer.Id, customer.CompanyId, customer.UserName);
  }
}

/// <summary>
/// funeralv2 토큰으로 들어온 요청에 헬프데스크 내부 클레임(uid / login_type / company_id)을 채워 넣는 미들웨어.
///
/// 기존 엔드포인트들은 이 세 클레임만 보고 동작한다. 여기서 채워주면 엔드포인트 코드를 하나도 고치지 않고
/// funeralv2 계정으로 헬프데스크를 쓸 수 있다.
/// </summary>
public class FuneralIdentityMiddleware {
  private readonly RequestDelegate _next;
  private readonly ILogger<FuneralIdentityMiddleware> _logger;

  /// <summary>미들웨어를 생성한다.</summary>
  public FuneralIdentityMiddleware(RequestDelegate next, ILogger<FuneralIdentityMiddleware> logger) {
    _next = next;
    _logger = logger;
  }

  /// <summary>요청을 처리한다.</summary>
  public async Task InvokeAsync(HttpContext context, IFuneralAccountLinkService linkService) {
    var user = context.User;

    // 헬프데스크가 직접 발급한 토큰은 이미 uid/login_type 을 갖고 있다. 손대지 않는다.
    var needsMapping = user.Identity?.IsAuthenticated == true
                       && user.FindFirst("uid") is null;

    if (needsMapping) {
      var authUserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? user.FindFirst("sub")?.Value
                       ?? HeaderValue(context, "X-User-Id");
      var email = user.FindFirst(ClaimTypes.Email)?.Value
                  ?? user.FindFirst("email")?.Value
                  ?? HeaderValue(context, "X-User-Email");
      var userName = user.FindFirst("RealName")?.Value
                     ?? user.FindFirst(ClaimTypes.Name)?.Value
                     ?? Decode(HeaderValue(context, "X-User-Name"));

      if (!string.IsNullOrWhiteSpace(authUserId)) {
        // JSini 계정 자체를 먼저 심는다. 헬프데스크 계정 연결이 없어도
        // "누가 요청했는지" 는 알아야 화면 안내와 감사 기록이 제대로 남는다.
        var jsiniClaims = new List<Claim> {
          new(JsiniUserExtensions.UserIdClaim, authUserId),
        };
        if (!string.IsNullOrWhiteSpace(userName)) {
          jsiniClaims.Add(new Claim(JsiniUserExtensions.UserNameClaim, userName));
        }
        if (!string.IsNullOrWhiteSpace(email)) {
          jsiniClaims.Add(new Claim(JsiniUserExtensions.EmailClaim, email));
        }
        context.User.AddIdentity(new ClaimsIdentity(jsiniClaims));

        var identity = await linkService.ResolveAsync(authUserId, email, context.RequestAborted);

        if (identity is null) {
          _logger.LogWarning(
              "funeralv2 계정 {AuthUserId} 에 연결된 헬프데스크 계정이 없습니다. 경로: {Path}",
              authUserId, context.Request.Path);
        }
        else {
          var claims = new List<Claim> {
            new("uid", identity.HelpdeskUserId.ToString()),
            new("login_type", identity.UserType),
            new("helpdesk_user_name", identity.UserName),
          };

          if (identity.CompanyId.HasValue) {
            claims.Add(new Claim("company_id", identity.CompanyId.Value.ToString()));
          }

          context.User.AddIdentity(new ClaimsIdentity(claims));
        }
      }
    }

    await _next(context);
  }

  private static string? HeaderValue(HttpContext context, string name) {
    var value = context.Request.Headers[name].ToString();
    return string.IsNullOrWhiteSpace(value) ? null : value;
  }

  private static string? Decode(string? value) {
    if (string.IsNullOrWhiteSpace(value)) return null;
    try {
      return Uri.UnescapeDataString(value);
    }
    catch (UriFormatException) {
      return value;
    }
  }
}

/// <summary>미들웨어 등록 확장 메서드.</summary>
public static class FuneralIdentityMiddlewareExtensions {
  /// <summary>funeralv2 토큰 → 헬프데스크 클레임 매핑 미들웨어를 파이프라인에 추가한다.</summary>
  public static IApplicationBuilder UseFuneralIdentityMapping(this IApplicationBuilder builder) {
    return builder.UseMiddleware<FuneralIdentityMiddleware>();
  }
}
