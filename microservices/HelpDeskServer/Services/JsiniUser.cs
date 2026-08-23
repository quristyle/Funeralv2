using System.Security.Claims;

namespace HelpDeskServer.Services;

/// <summary>
/// 요청을 보낸 JSini 포털 계정.
///
/// 헬프데스크는 원래 자기 사용자 테이블(<c>admin</c>/<c>customer</c>)만 알고 있었다.
/// 포털로 계정을 단일화한 뒤로 "지금 요청한 사람" 의 출처는 JSini 계정 하나다.
/// 헬프데스크 내부 ID 는 기존 데이터(요청 작성자·담당자)를 가리키기 위해서만 남는다.
/// </summary>
/// <param name="UserId">JSini 로그인 아이디 (scom.accounts.user_id)</param>
/// <param name="UserName">표시 이름 (real_name 우선)</param>
/// <param name="Email">대표 이메일</param>
/// <param name="CompanyId">소속 회사 식별자</param>
/// <param name="Roles">배정된 역할 식별자 목록 (ADMINISTRATOR 등)</param>
public sealed record JsiniUserInfo(
    string UserId,
    string? UserName,
    string? Email,
    string? CompanyId,
    IReadOnlyList<string> Roles);

/// <summary>JSini 계정 정보를 요청에서 꺼내는 확장 메서드.</summary>
public static class JsiniUserExtensions {

  /// <summary>JSini 로그인 아이디를 담는 클레임 이름.</summary>
  public const string UserIdClaim = "jsini_user_id";

  /// <summary>JSini 표시 이름을 담는 클레임 이름.</summary>
  public const string UserNameClaim = "jsini_user_name";

  /// <summary>JSini 이메일을 담는 클레임 이름.</summary>
  public const string EmailClaim = "jsini_email";

  /// <summary>
  /// 요청에 실린 JSini 계정을 돌려준다. 헬프데스크 자체 토큰으로 들어온 요청이면 null.
  ///
  /// 두 경로를 모두 본다.
  ///   1) 토큰 클레임 — 서비스가 직접 JWT 를 검증하므로 평소에는 이쪽이다.
  ///   2) 게이트웨이 헤더(<c>X-User-*</c>) — 토큰에 없던 값(역할 등)을 보완한다.
  ///      외부에서 보낸 같은 이름의 헤더는 게이트웨이가 먼저 지우므로 위조되지 않는다.
  /// </summary>
  public static JsiniUserInfo? GetJsiniUser(this HttpContext context) {
    var principal = context.User;

    // NameIdentifier 로는 구분할 수 없다. 헬프데스크 자체 토큰의 `sub`(로그인 아이디)도
    // JwtBearer 기본 매핑이 NameIdentifier 로 바꿔 놓기 때문이다.
    // 그래서 JSini 토큰임이 확실한 두 가지만 본다.
    //   - FuneralIdentityMiddleware 가 심어 준 jsini_user_id 클레임
    //   - 게이트웨이가 붙인 X-User-Id 헤더 (게이트웨이는 포털 키로 검증한 토큰에만 붙인다)
    var userId = principal.FindFirst(UserIdClaim)?.Value
                 ?? HeaderValue(context, "X-User-Id");

    if (string.IsNullOrWhiteSpace(userId)) return null;

    var userName = principal.FindFirst(UserNameClaim)?.Value
                   ?? principal.FindFirst("RealName")?.Value
                   ?? principal.FindFirst(ClaimTypes.Name)?.Value
                   ?? DecodeHeader(HeaderValue(context, "X-User-Name"));

    var email = principal.FindFirst(EmailClaim)?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value
                ?? principal.FindFirst("email")?.Value
                ?? HeaderValue(context, "X-User-Email");

    var companyId = principal.FindFirst("CompanyId")?.Value
                    ?? HeaderValue(context, "X-User-Company-Id");

    var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
    if (roles.Count == 0) {
      var header = HeaderValue(context, "X-User-Roles");
      if (!string.IsNullOrWhiteSpace(header)) {
        roles = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
      }
    }

    return new JsiniUserInfo(
        userId,
        string.IsNullOrWhiteSpace(userName) ? null : userName,
        string.IsNullOrWhiteSpace(email) ? null : email,
        string.IsNullOrWhiteSpace(companyId) ? null : companyId,
        roles);
  }

  /// <summary>감사 기록에 남길 사용자 표기. JSini 로그인 아이디를 우선한다.</summary>
  public static string AuditUser(this HttpContext? context) {
    if (context is null) return "system";

    var jsini = context.GetJsiniUser();
    if (jsini is not null) return jsini.UserId;

    // 헬프데스크 자체 토큰으로 들어온 요청(JinReception 등)은 예전대로 내부 ID 를 남긴다.
    return context.User.FindFirst("uid")?.Value
           ?? context.User.Identity?.Name
           ?? "system";
  }

  private static string? HeaderValue(HttpContext context, string name) {
    var value = context.Request.Headers[name].ToString();
    return string.IsNullOrWhiteSpace(value) ? null : value;
  }

  private static string? DecodeHeader(string? value) {
    if (string.IsNullOrWhiteSpace(value)) return null;
    try {
      return Uri.UnescapeDataString(value);
    }
    catch (UriFormatException) {
      return value;
    }
  }
}
