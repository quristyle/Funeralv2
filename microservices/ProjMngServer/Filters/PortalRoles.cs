using System.Net;

namespace ProjMngServer.Filters;

/// <summary>
/// 게이트웨이가 실어 준 역할 헤더로 포털 사용자의 자격을 가린다.
/// </summary>
/// X-User-Id 가 없는 요청(실행기·업무 비서·자체 배경 작업·루프백 운영 도구)은 포털 사용자가 아니라 여기서 판단하지 않는다.
public static class PortalRoles {

  /// <summary>프로젝트관리 메뉴 어느 하나라도 받는 역할. <c>PROJMNG_</c> 로 시작하는 역할도 포함한다.</summary>
  public static readonly string[] Module = [
    "ADMINISTRATOR", "SYSTEM_ADMINISTRATOR", "HIDDEN_ADMIN",
    "PROJECT_MANAGEMENT_DEVELOPER", "DEVELOPMENT_TEST1",
    // 머리 도구줄의 「빠른 지시」(AI 작업)를 쓰는 서버관리자.
    "SERVER_ADMIN",
  ];

  private const string ModulePrefix = "PROJMNG_";

  /// <summary>DB 접속 등록·AI 대상·관리자용 AI 작업처럼 운영에 닿는 쓰기.</summary>
  public static readonly string[] Admin = [
    "ADMINISTRATOR", "SYSTEM_ADMINISTRATOR", "PROJMNG_ADMIN", "HIDDEN_ADMIN",
  ];

  /// <summary>등록 질의(db-logics) 편집. /projmng/db/code 를 쓰는 팀 역할까지.</summary>
  public static readonly string[] DbEditor = [.. Admin, "PROJMNG_JSINITEAM", "PROJMNG_MNM_SMG"];

  /// <summary>서버 디스크를 읽는 소스 추적·Glue 훑기(/projmng/source/*).</summary>
  public static readonly string[] Source = [.. Admin, "PROJMNG_MNM_SMG"];

  /// <summary>게이트웨이를 거친 포털 사용자의 요청인가. 게이트웨이는 신원이 없으면 이 헤더를 지워 보낸다.</summary>
  public static bool IsPortalCall(HttpRequest request) => request.Headers.ContainsKey("X-User-Id");

  /// <summary>역할 전부. X-User-Role 에는 첫 역할만 담기므로 X-User-Roles 를 먼저 읽는다.</summary>
  public static string[] RolesOf(HttpRequest request) {
    var roles = request.Headers["X-User-Roles"].ToString()
                       .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (roles.Length > 0) return roles;

    var single = request.Headers["X-User-Role"].ToString().Trim();
    return string.IsNullOrEmpty(single) ? [] : [single];
  }

  public static bool HasAny(HttpRequest request, IReadOnlyCollection<string> allowed) =>
    RolesOf(request).Any(r => allowed.Contains(r, StringComparer.OrdinalIgnoreCase));

  public static bool IsModuleMember(HttpRequest request) =>
    RolesOf(request).Any(r =>
      Module.Contains(r, StringComparer.OrdinalIgnoreCase)
      || r.StartsWith(ModulePrefix, StringComparison.OrdinalIgnoreCase));

  /// <summary>표준 봉투로 403 을 쓴다.</summary>
  public static Task WriteForbiddenAsync(HttpContext context, string message) {
    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsJsonAsync(
      JSini.Shared.DTOs.ApiResponse<object>.Fail(message, code: "E403"));
  }
}
