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
  /// 포털 계정의 <c>MsaSource</c> 값으로 원본 헬프데스크 레코드를 찾을지 여부. 기본 false.
  ///
  /// <para>
  /// <b>아이디·이메일 대조와는 성격이 다르다.</b> 그 둘은 "값이 같으니 같은 사람이겠지" 라는
  /// 추정이고, 실제로 오탐이 있었다(포털 <c>admin</c> 과 헬프데스크 <c>admin</c> 은 다른 사람).
  /// 반면 <c>MsaSource</c>(<c>helpdesk:admin:4</c>)는 이관 스크립트가 그 계정을 만들 때
  /// "이 원본 레코드로 만들었다" 고 남긴 값이다 — 대응 관계가 만들어진 근거 그 자체다.
  /// </para>
  ///
  /// <para>
  /// 그런데도 기본을 끔으로 두는 이유는 <b>영향 범위</b> 때문이다. 켜면 이관 계정 34개가
  /// 한꺼번에 헬프데스크 담당자·고객으로 해석되기 시작한다. 지금은 사람이 이어 준 연결
  /// 하나만 살아 있으므로, 누가 무엇을 보게 되는지 확인한 뒤 켜는 편이 맞다.
  /// </para>
  ///
  /// <para>사람이 만든 연결(<c>auth_user_links</c>)이 언제나 우선한다. 이 값은 그다음이다.</para>
  /// </summary>
  public bool MatchByMsaSource { get; set; }

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
  /// <summary>
  /// AuthServer 사용자 식별자로 헬프데스크 계정을 찾는다. 못 찾으면 null.
  /// </summary>
  /// <param name="authUserId">포털 로그인 아이디</param>
  /// <param name="email">대표 이메일 (이메일 대조를 켠 경우에만 쓰인다)</param>
  /// <param name="msaSource">
  /// 포털 계정의 출처 (<c>helpdesk:admin:4</c> 형식). 이관으로 만들어진 계정만 갖고 있다.
  /// </param>
  /// <param name="ct">취소 토큰</param>
  Task<HelpdeskIdentity?> ResolveAsync(string authUserId, string? email, string? msaSource = null, CancellationToken ct = default);

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

    // 3순위: 로그인 아이디가 같은 계정.
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

    // 4순위: 이메일 일치. 아이디 대조보다는 낫지만 이것도 추정이다.
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

  /// <summary>
  /// <c>MsaSource</c> 값에서 헬프데스크 원본 레코드를 읽어낸다.
  /// 형식은 <c>&lt;서비스&gt;:&lt;테이블&gt;:&lt;원본키&gt;</c> — 예: <c>helpdesk:admin:4</c>.
  /// 헬프데스크가 아닌 출처(<c>projmng:dev_user:jskim</c>)는 무시한다.
  /// </summary>
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
      // 이관으로 만들어진 계정만 갖고 있다. 없으면 예전과 똑같이 동작한다.
      var msaSource = user.FindFirst("MsaSource")?.Value
                      ?? HeaderValue(context, "X-User-Msa-Source");

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

        // 포털 역할로 담당자 권한을 판정한다. 역할 목록은 설정에 있어 여기서만 읽을 수 있으므로
        // 결과를 클레임으로 남기고, 엔드포인트는 HelpdeskPrincipal 로 꺼내 쓴다.
        //
        // 이것이 없으면 포털에서 관리자 역할을 받은 계정도 계정 연결이 없는 한
        // 헬프데스크에서는 권한이 하나도 없는 사람이 된다.
        if (IsAdminByRole(context)) {
          jsiniClaims.Add(new Claim(HelpdeskPrincipalExtensions.AdminByRoleClaim, "true"));
        }

        context.User.AddIdentity(new ClaimsIdentity(jsiniClaims));

        var identity = await linkService.ResolveAsync(authUserId, email, msaSource, context.RequestAborted);

        if (identity is null) {
          // 연결이 없는 것은 오류가 아니다. 조회·관리는 포털 역할로 할 수 있고,
          // '내 것' 을 가리키는 일만 못 한다. 그래서 경고가 아니라 정보로 남긴다.
          _logger.LogInformation(
              "포털 계정 {AuthUserId} 에 연결된 헬프데스크 레코드가 없습니다(담당자 권한: {IsAdmin}). 경로: {Path}",
              authUserId, IsAdminByRole(context), context.Request.Path);
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

  /// <summary>
  /// 포털 역할이 담당자 권한에 해당하는가.
  ///
  /// 역할은 여러 개일 수 있다. 토큰 클레임을 먼저 보고, 없으면 게이트웨이가 붙인
  /// <c>X-User-Roles</c>(전체) → <c>X-User-Role</c>(첫 번째) 순으로 떨어진다.
  /// 단수 헤더만 보면 역할이 둘 이상인 계정의 두 번째 역할이 통째로 무시된다.
  /// </summary>
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
