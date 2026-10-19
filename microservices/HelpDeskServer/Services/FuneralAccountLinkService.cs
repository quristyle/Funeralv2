using System.Security.Claims;
using HelpDeskServer.Data;
using HelpDeskServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HelpDeskServer.Services;

/// <summary>funeralv2 계정 ↔ 헬프데스크 계정 자동 매칭 정책.</summary>
public class AccountLinkOptions {
  /// <summary>설정 섹션 이름.</summary>
  public const string SectionName = "AccountLink";

  /// <summary>로그인 아이디가 같으면 같은 사람으로 볼지(기본 false). 아이디만 같은 다른 사람이 있어 함부로 켜지 않는다.</summary>
  public bool MatchByLoginId { get; set; }

  /// <summary>포털 계정의 MsaSource(helpdesk:admin:4)로 원본 헬프데스크 레코드를 찾을지(기본 false). 이관 스크립트가 남긴 대응 근거라 추정이 아니다.
  /// 다만 켜면 이관 계정 전부가 한꺼번에 해석되므로 확인 후 켠다. 사람이 이은 연결(auth_user_links)이 항상 우선한다.</summary>
  public bool MatchByMsaSource { get; set; }

  /// <summary>이메일이 같으면 같은 사람으로 볼지(기본 false). 추정이라 아이디 대조와 같은 규칙을 따른다 — 데이터가 정리된 환경에서만 켠다.</summary>
  public bool MatchByEmail { get; set; }
}

/// <summary>funeralv2 계정으로 해석된 헬프데스크 사용자.</summary>
/// <param name="UserType">admin 또는 customer</param>
/// <param name="HelpdeskUserId">헬프데스크 내부 계정 ID</param>
/// <param name="CompanyId">고객인 경우 소속 회사 식별자(포털 <c>scom.companies.id</c>)</param>
/// <param name="UserName">표시용 이름</param>
public record HelpdeskIdentity(string UserType, int HelpdeskUserId, string? CompanyId, string UserName);

/// <summary>funeralv2(AuthServer) 계정을 헬프데스크 계정으로 해석한다.</summary>
public interface IFuneralAccountLinkService {
  /// <summary>AuthServer 사용자 식별자로 헬프데스크 계정을 찾는다. 못 찾으면 null.</summary>
  /// <param name="authUserId">포털 로그인 아이디</param>
  /// <param name="email">대표 이메일 (이메일 대조를 켠 경우에만 쓰인다)</param>
  /// <param name="msaSource">포털 계정의 출처(helpdesk:admin:4). 이관으로 만든 계정만 갖는다.</param>
  /// <param name="ct">취소 토큰</param>
  Task<HelpdeskIdentity?> ResolveAsync(string authUserId, string? email, string? msaSource = null, CancellationToken ct = default);

  /// <summary>이 포털 계정이 스스로 만든 고객 줄을 찾는다. 없으면 null이고 만들지는 않는다. 관리자에게는 부르지 않는다(관리자의 HelpdeskUserId 가 고객 번호가 되면 엉뚱한 줄을 센다).
  /// 열쇠는 로그인 아이디와 이름이 둘 다 같은 줄이다(아이디만으로는 남과 겹친다). 우리가 그 열쇠로 만든 줄을 되찾을 뿐 추정이 아니다.</summary>
  Task<HelpdeskIdentity?> ResolveSelfCustomerAsync(string authUserId, string? userName, CancellationToken ct = default);

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

  /// <summary>제 고객 줄 찾기의 캐시 열쇠. 이름까지 넣는다 — 이름만 고친 사람이 옛 결과를 받지 않도록.</summary>
  private static string SelfCacheKey(string authUserId, string userName) =>
      $"helpdesk:selfcustomer:{authUserId}:{userName}";

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
  public async Task<HelpdeskIdentity?> ResolveAsync(
      string authUserId, string? email, string? msaSource = null, CancellationToken ct = default) {
    if (string.IsNullOrWhiteSpace(authUserId)) return null;

    if (_cache.TryGetValue<HelpdeskIdentity?>(CacheKey(authUserId), out var cached)) {
      return cached;
    }

    var resolved = await ResolveFromDatabaseAsync(authUserId, email, msaSource, ct);

    // 못 찾은 경우도 캐싱한다. 매핑이 없는 계정이 매 요청마다 DB 를 3번씩 뒤지는 것을 막는다.
    _cache.Set(CacheKey(authUserId), resolved, TimeSpan.FromMinutes(resolved is null ? 1 : 10));
    return resolved;
  }

  private async Task<HelpdeskIdentity?> ResolveFromDatabaseAsync(
      string authUserId, string? email, string? msaSource, CancellationToken ct) {
    // 1순위: 명시적으로 등록된 매핑. 사람이 확인하고 이어 준 값이라 언제나 우선한다.
    var link = await _db.AuthUserLinks.AsNoTracking()
        .FirstOrDefaultAsync(l => l.AuthUserId == authUserId, ct);

    if (link is not null) {
      var byLink = await LoadAsync(link.UserType, link.HelpdeskUserId, ct);
      if (byLink is not null) return byLink;

      _logger.LogWarning(
          "auth_user_links 에 {AuthUserId} → {UserType}#{Id} 매핑이 있으나 대상 계정을 찾지 못했습니다.",
          authUserId, link.UserType, link.HelpdeskUserId);
    }

    // 2순위: 이 계정이 만들어진 출처. 추정이 아니라 이관 당시 기록이다(옵션 주석 참고).
    if (_options.MatchByMsaSource) {
      var fromSource = ParseHelpdeskSource(msaSource);
      if (fromSource is not null) {
        var (userType, sourceId) = fromSource.Value;
        var bySource = await LoadAsync(userType, sourceId, ct);
        if (bySource is not null) return bySource;

        _logger.LogWarning(
            "포털 계정 {AuthUserId} 의 MsaSource 가 {UserType}#{Id} 를 가리키지만 그 레코드가 없습니다.",
            authUserId, userType, sourceId);
      }
    }

    // 3순위: 로그인 아이디가 같은 계정. 기본은 끔 — 아이디만 같고 다른 사람인 경우가 운영 데이터에 있어 자동으로 이으면 남의 계정으로 로그인된다.
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

    // 4순위: 이메일 일치. 추정이라 기본은 꺼 둔다(같은 이메일을 쓰는 다른 사람이 실제로 있다).
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

  /// <summary>MsaSource 값(&lt;서비스&gt;:&lt;테이블&gt;:&lt;원본키&gt;)에서 헬프데스크 원본 레코드를 읽는다. 다른 서비스의 출처는 무시한다.</summary>
  private static (string UserType, int Id)? ParseHelpdeskSource(string? msaSource) {
    if (string.IsNullOrWhiteSpace(msaSource)) return null;

    var parts = msaSource.Split(':');
    if (parts.Length != 3) return null;
    if (!string.Equals(parts[0], "helpdesk", StringComparison.OrdinalIgnoreCase)) return null;

    var table = parts[1].ToLowerInvariant();
    if (table is not ("admin" or "customer")) return null;
    if (!int.TryParse(parts[2], out var id)) return null;

    return (table, id);
  }

  /// <inheritdoc />
  public async Task<HelpdeskIdentity?> ResolveSelfCustomerAsync(
      string authUserId, string? userName, CancellationToken ct = default) {
    if (string.IsNullOrWhiteSpace(authUserId)) return null;

    // 이름이 없으면 아이디로 만들어진 줄이다(`RequesterProvisioner.EnsureSelfAsync`).
    var name = string.IsNullOrWhiteSpace(userName) ? authUserId : userName!;

    if (_cache.TryGetValue<HelpdeskIdentity?>(SelfCacheKey(authUserId, name), out var cached)) {
      return cached;
    }

    var mine = await _db.Customers.AsNoTracking()
        .FirstOrDefaultAsync(c => c.LoginId == authUserId && c.UserName == name && !c.IsDeleted, ct);

    var resolved = mine is null
        ? null
        : new HelpdeskIdentity("customer", mine.Id, mine.CompanyId, mine.UserName);

    // 못 찾은 것도 짧게 캐싱한다. 요청을 처음 올리는 순간 줄이 생기므로, 오래 담으면 방금 쓴 글이 남의 것처럼 보인다.
    _cache.Set(SelfCacheKey(authUserId, name), resolved, TimeSpan.FromMinutes(resolved is null ? 1 : 10));
    return resolved;
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

/// <summary>funeralv2 토큰으로 들어온 요청에 헬프데스크 클레임(uid / login_type / company_id)을 채운다. 엔드포인트 코드를 고치지 않고 funeralv2 계정으로 헬프데스크를 쓰게 한다.</summary>
public class FuneralIdentityMiddleware {
  private readonly RequestDelegate _next;
  private readonly ILogger<FuneralIdentityMiddleware> _logger;
  private readonly HelpdeskIdentityOptions _identityOptions;

  /// <summary>미들웨어를 생성한다.</summary>
  public FuneralIdentityMiddleware(
      RequestDelegate next,
      ILogger<FuneralIdentityMiddleware> logger,
      IOptions<HelpdeskIdentityOptions> identityOptions) {
    _next = next;
    _logger = logger;
    _identityOptions = identityOptions.Value;
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
      // 이관으로 만들어진 계정만 갖고 있다. 없으면 그대로 동작한다.
      var msaSource = user.FindFirst("MsaSource")?.Value
                      ?? HeaderValue(context, "X-User-Msa-Source");

      if (!string.IsNullOrWhiteSpace(authUserId)) {
        // JSini 계정은 헬프데스크 연결이 없어도 먼저 심는다 — 누가 요청했는지는 화면 안내와 감사 기록에 필요하다.
        var jsiniClaims = new List<Claim> {
          new(JsiniUserExtensions.UserIdClaim, authUserId),
        };
        if (!string.IsNullOrWhiteSpace(userName)) {
          jsiniClaims.Add(new Claim(JsiniUserExtensions.UserNameClaim, userName));
        }
        if (!string.IsNullOrWhiteSpace(email)) {
          jsiniClaims.Add(new Claim(JsiniUserExtensions.EmailClaim, email));
        }

        // 포털 역할로 관리자를 판정한다. 역할 목록은 설정에만 있어 여기서 클레임으로 남기고, 엔드포인트는 HelpdeskPrincipal 로 꺼내 쓴다. 가진 사람이 관리자, 나머지는 전부 고객이다.
        var isAdmin = IsAdminByRole(context);
        if (isAdmin) {
          jsiniClaims.Add(new Claim(HelpdeskPrincipalExtensions.AdminByRoleClaim, "true"));
        }

        context.User.AddIdentity(new ClaimsIdentity(jsiniClaims));

        var identity = await linkService.ResolveAsync(authUserId, email, msaSource, context.RequestAborted);

        // 연결이 없는 고객은 제가 만든 고객 줄로 해석한다(없으면 null). 관리자에게는 하지 않는다 — 관리자의 uid 가 고객 번호가 되면 배정 요청을 엉뚱하게 센다.
        if (identity is null && !isAdmin) {
          identity = await linkService.ResolveSelfCustomerAsync(authUserId, userName, context.RequestAborted);
        }

        if (identity is null) {
          // 연결이 없는 것은 오류가 아니다. 조회·관리는 포털 역할로 되므로 정보로만 남긴다.
          _logger.LogInformation(
              "포털 계정 {AuthUserId} 에 연결된 헬프데스크 레코드가 없습니다(관리자: {IsAdmin}). 경로: {Path}",
              authUserId, isAdmin, context.Request.Path);
        }
        else {
          var claims = new List<Claim> {
            new("uid", identity.HelpdeskUserId.ToString()),
            new("login_type", identity.UserType),
            new("helpdesk_user_name", identity.UserName),
          };

          if (!string.IsNullOrWhiteSpace(identity.CompanyId)) {
            claims.Add(new Claim("company_id", identity.CompanyId));
          }

          context.User.AddIdentity(new ClaimsIdentity(claims));
        }
      }
    }

    await _next(context);
  }

  /// <summary>포털 역할이 담당자 권한에 해당하는지. 역할 클레임을 먼저 보고, 없으면 게이트웨이의 X-User-Roles(전체)를 본다 — 단수 헤더만 보면 둘째 역할이 무시된다.</summary>
  private bool IsAdminByRole(HttpContext context) {
    var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    if (roles.Count == 0) {
      var all = HeaderValue(context, "X-User-Roles") ?? HeaderValue(context, "X-User-Role");
      if (!string.IsNullOrWhiteSpace(all)) {
        roles = all.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
      }
    }

    return roles.Any(r => _identityOptions.AdminRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
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
