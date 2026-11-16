namespace ProjMngServer.Filters;

/// <summary>
/// 포털 사용자(X-User-Id 가 실린 요청)의 /api/** 를 역할로 가른다. 게이트웨이는 로그인 여부만 본다.
/// </summary>
/// 1) 모듈 역할이 없으면 막는다 — 다른 업무 화면이 부르는 길(<see cref="IsOpenToAllUsers"/>)만 뺀다.
/// 2) 운영에 닿는 쓰기는 관리자 역할로 한 번 더 좁힌다.
/// 신원 헤더가 없는 서버 간 호출은 손대지 않는다.
public sealed class ProjMngRoleGateMiddleware(RequestDelegate next, ILogger<ProjMngRoleGateMiddleware> logger) {

  public async Task InvokeAsync(HttpContext context) {
    var request = context.Request;

    if (!PortalRoles.IsPortalCall(request) || !request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)) {
      await next(context);
      return;
    }

    if (!IsOpenToAllUsers(request) && !PortalRoles.IsModuleMember(request)) {
      await DenyAsync(context, "프로젝트관리 권한이 없습니다.");
      return;
    }

    if (RequiredRolesForWrite(request) is { } required && !PortalRoles.HasAny(request, required)) {
      await DenyAsync(context, "이 작업을 할 권한이 없습니다.");
      return;
    }

    await next(context);
  }

  /// <summary>프로젝트관리 메뉴 밖에서 부르는 길. 업무 비서(포털관리)의 「내 프로젝트」 고르개가 쓴다.</summary>
  private static bool IsOpenToAllUsers(HttpRequest request) =>
    HttpMethods.IsGet(request.Method)
    && string.Equals(request.Path.Value?.TrimEnd('/'), "/api/projects/mine", StringComparison.OrdinalIgnoreCase);

  /// <summary>쓰기에 더 좁은 역할이 필요하면 그 목록을, 아니면 null.</summary>
  private static string[]? RequiredRolesForWrite(HttpRequest request) {
    if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)) {
      return null;
    }

    var path = request.Path;
    const StringComparison ic = StringComparison.OrdinalIgnoreCase;

    // 접속 정보와 그 DB 에 매단 질의(props) — props 는 /api/Dev 가 그대로 실행한다.
    if (path.StartsWithSegments("/api/project-dbs", ic)) return PortalRoles.Admin;

    // 대상 하나가 저장소 경로와 자동 push 를 정한다.
    if (path.StartsWithSegments("/api/ai-targets", ic)) return PortalRoles.Admin;

    // 등록 질의는 /api/Dev 가 실행하는 SQL 본문이다.
    if (path.StartsWithSegments("/api/db-logics", ic)) return PortalRoles.DbEditor;

    // AI 작업 만들기·보내기는 「빠른 지시」로 모듈 역할 누구나 쓴다. 대상(저장소·자동 push)을 정하는 것만 위에서 관리자로 묶는다.

    return null;
  }

  private Task DenyAsync(HttpContext context, string message) {
    logger.LogWarning("[RoleGate] 거부: {Method} {Path} user={UserId} roles={Roles}",
      context.Request.Method, context.Request.Path,
      context.Request.Headers["X-User-Id"].ToString(),
      string.Join(',', PortalRoles.RolesOf(context.Request)));

    return PortalRoles.WriteForbiddenAsync(context, message);
  }
}

public static class ProjMngRoleGateMiddlewareExtensions {
  public static IApplicationBuilder UseProjMngRoleGate(this IApplicationBuilder app)
    => app.UseMiddleware<ProjMngRoleGateMiddleware>();
}
