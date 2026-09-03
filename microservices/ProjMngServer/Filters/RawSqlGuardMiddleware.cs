using System.Net;

namespace ProjMngServer.Filters;

/// <summary>
/// <c>POST /api/Dev/sql</c> 은 요청 본문의 문자열을 지정된 DB 에 그대로 실행한다.
/// 개발자용 DB 도구 화면이 쓰는 통로인데, 사실상 임의 SQL 실행이다.
///
/// 이식 전에는 이 서비스가 인증 없이 열려 있었다. 포털에 붙으면 게이트웨이가 JWT 를
/// 요구하므로 익명 접근은 막히지만, 그것만으로는 <b>로그인한 모든 사용자</b>가
/// 임의 SQL 을 실행할 수 있다. 그래서 역할을 한 번 더 확인한다.
///
/// 허용 역할은 <c>DevTools:RawSqlRoles</c> 설정으로 바꿀 수 있고,
/// <c>DevTools:AllowRawSql</c> 을 false 로 두면 경로 자체를 닫는다.
/// </summary>
public class RawSqlGuardMiddleware {

  private const string GuardedPath = "/api/dev/sql";

  private readonly RequestDelegate _next;
  private readonly ILogger<RawSqlGuardMiddleware> _logger;
  private readonly bool _allow;
  private readonly string[] _allowedRoles;

  public RawSqlGuardMiddleware(RequestDelegate next, ILogger<RawSqlGuardMiddleware> logger, IConfiguration configuration) {
    _next = next;
    _logger = logger;
    _allow = configuration.GetValue("DevTools:AllowRawSql", true);
    _allowedRoles = configuration.GetSection("DevTools:RawSqlRoles").Get<string[]>()
                    ?? new[] { "SYSTEM_ADMINISTRATOR", "ADMINISTRATOR" };
  }

  public async Task InvokeAsync(HttpContext context) {

    if (!context.Request.Path.StartsWithSegments(GuardedPath, StringComparison.OrdinalIgnoreCase)) {
      await _next(context);
      return;
    }

    if (!_allow) {
      await Deny(context, "직접 쿼리 실행이 이 환경에서는 비활성화되어 있습니다. (DevTools:AllowRawSql)");
      return;
    }

    var userId = context.Request.Headers["X-User-Id"].ToString();

    // 역할은 여러 개일 수 있다. 게이트웨이가 X-User-Roles 에 전부 실어 보내고,
    // X-User-Role 에는 첫 번째만 담는다. 예전에는 단수 헤더만 보느라
    // 역할이 둘 이상인 계정은 두 번째 역할로 얻은 권한이 통째로 무시됐다.
    var roles = context.Request.Headers["X-User-Roles"].ToString()
                       .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (roles.Length == 0) {
      var single = context.Request.Headers["X-User-Role"].ToString();
      roles = string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single };
    }

    // 게이트웨이를 지나지 않은 직접 호출은 신원이 없다. 개발 편의를 위해 통과시키되 기록은 남긴다.
    if (string.IsNullOrEmpty(userId)) {
      _logger.LogWarning("[RawSqlGuard] 신원 헤더 없는 직접 호출을 허용했습니다. 게이트웨이를 경유하지 않은 요청입니다.");
      await _next(context);
      return;
    }

    if (!roles.Any(r => _allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase))) {
      _logger.LogWarning("[RawSqlGuard] 직접 쿼리 실행 거부: user={UserId} roles={Roles}", userId, string.Join(',', roles));
      await Deny(context, "직접 쿼리 실행 권한이 없습니다.");
      return;
    }

    _logger.LogInformation("[RawSqlGuard] 직접 쿼리 실행 허용: user={UserId} roles={Roles}", userId, string.Join(',', roles));
    await _next(context);
  }

  private static async Task Deny(HttpContext context, string message) {
    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
    context.Response.ContentType = "application/json";
    // 표준 봉투로 거절한다 (결정 D-A1 — 옛 `{ code: -403, ... }` 봉투는 2026-09-04 에 내렸다).
    await context.Response.WriteAsJsonAsync(
      JSini.Shared.DTOs.ApiResponse<object>.Fail(message, code: "E403"));
  }
}

public static class RawSqlGuardMiddlewareExtensions {
  public static IApplicationBuilder UseRawSqlGuard(this IApplicationBuilder app)
    => app.UseMiddleware<RawSqlGuardMiddleware>();
}
