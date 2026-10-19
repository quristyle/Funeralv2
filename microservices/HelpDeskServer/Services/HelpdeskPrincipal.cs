namespace HelpDeskServer.Services;

/// <summary>헬프데스크 신원 해석 정책.</summary>
public class HelpdeskIdentityOptions {
  /// <summary>설정 섹션 이름.</summary>
  public const string SectionName = "HelpdeskIdentity";

  /// <summary>헬프데스크 관리자로 대우할 포털 역할 목록. 가진 사람이 관리자이고 나머지는 전부 고객이다(역할을 가진 관리자도 요청을 올린다).
  /// 연결 종류(auth_user_links.user_type)는 권한에 쓰지 않고, 「무엇이 내 것인가」만 정한다.</summary>
  public string[] AdminRoles { get; set; } = ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR"];
}

/// <summary>지금 요청을 보낸 사람. 포털 계정(누구인가·권한의 정본)과 헬프데스크 내부 레코드(작성자·담당자가 참조하는 숫자 ID)를 한 자리에 모은다.
/// 연결이 없어도 조회·집계는 되고, 내 것을 가리키는 일(내 댓글·배정 요청·구독)에만 IsLinked 가 필요하다.</summary>
/// <param name="JsiniUserId">포털 로그인 아이디. 헬프데스크 자체 토큰으로 들어온 요청이면 null</param>
/// <param name="DisplayName">표시 이름 (포털 실명 우선)</param>
/// <param name="Email">대표 이메일</param>
/// <param name="JsiniRoles">포털에서 배정된 역할 식별자 목록</param>
/// <param name="HelpdeskUserId">연결된 헬프데스크 내부 계정 ID. 연결이 없으면 null</param>
/// <param name="LinkedUserType">연결된 계정 종류 — <c>admin</c> / <c>customer</c> / null</param>
/// <param name="CompanyId">
/// 소속 회사 식별자 — <b>포털</b>(<c>scom.companies.id</c>)의 값이다.
/// 포털 토큰이 실어 주는 값을 먼저 보고, 없으면 연결된 고객 줄에 적힌 값을 쓴다.
/// </param>
/// <param name="IsAdmin">
/// 관리자인가 — <b>포털 역할이 <c>ADMINISTRATOR</c> · <c>SYSTEM_ADMINISTRATOR</c> 중
/// 하나인가</b>가 전부다(<see cref="HelpdeskIdentityOptions.AdminRoles"/>).
/// 연결 종류는 보지 않는다.
/// </param>
public sealed record HelpdeskPrincipal(
    string? JsiniUserId,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> JsiniRoles,
    int? HelpdeskUserId,
    string? LinkedUserType,
    string? CompanyId,
    bool IsAdmin) {

  /// <summary>헬프데스크 내부 레코드에 이어져 있는가. 내 것을 가리키는 일에 필요하다.</summary>
  public bool IsLinked => HelpdeskUserId.HasValue;

  /// <summary>소속 회사를 알 수 있는가. 회사 단위로 범위를 좁힐 때 먼저 본다.</summary>
  public bool HasCompany => !string.IsNullOrWhiteSpace(CompanyId);

  /// <summary>고객인가 — 관리자가 아닌 모든 사람이다. 연결 종류로 가르면 연결은 운영에 한 줄뿐이라 회사 범위 조건이 안 걸리므로 IsAdmin 의 뒤집기로 정했다.</summary>
  public bool IsCustomer => !IsAdmin;

  /// <summary>헬프데스크 담당자 줄(admin)에 이어져 있는가. 권한이 아니다 — admin.id 로 행을 찾는 일에만 쓴다.</summary>
  public bool IsLinkedAdmin =>
      string.Equals(LinkedUserType, "admin", StringComparison.OrdinalIgnoreCase);

  /// <summary>담당자 권한은 있으나 헬프데스크 레코드는 없는 상태. 관리 조회는 되지만 「나에게 배정된 요청」은 비어 있다.</summary>
  public bool IsUnlinkedAdmin => IsAdmin && !IsLinked;

  /// <summary>화면·기록에 남길 사람 표기.</summary>
  public string Who =>
      JsiniUserId ?? (HelpdeskUserId?.ToString() ?? "unknown");
}

/// <summary>요청에서 <see cref="HelpdeskPrincipal"/> 을 꺼내는 확장 메서드.</summary>
public static class HelpdeskPrincipalExtensions {

  /// <summary>포털 역할로 담당자 권한이 인정됐음을 나타내는 클레임. 역할 설정은 DI 가 있는 미들웨어에서 읽고 결과만 클레임으로 남긴다.</summary>
  public const string AdminByRoleClaim = "helpdesk_admin_by_role";

  /// <summary>비어 있지 않은 첫 값. 없으면 null.</summary>
  private static string? FirstNonBlank(params string?[] values) =>
      values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

  /// <summary>지금 요청을 보낸 사람을 돌려준다.</summary>
  public static HelpdeskPrincipal GetHelpdeskPrincipal(this HttpContext context) {
    var jsini = context.GetJsiniUser();
    var principal = context.User;

    int? helpdeskUserId = int.TryParse(principal.FindFirst("uid")?.Value, out var uid) ? uid : null;
    var linkedUserType = principal.FindFirst("login_type")?.Value;
    // 회사는 포털이 관리한다 — 포털 토큰이 실어 주는 회사를 먼저 믿는다. company_id 클레임도 이제 포털 회사 아이디다.
    var companyId = FirstNonBlank(
        jsini?.CompanyId,
        principal.FindFirst("company_id")?.Value);

    // 관리자는 포털 역할 하나로 정해진다(HelpdeskIdentityOptions.AdminRoles). 연결 종류로 판정하던 것은 걷었다.
    var isAdminByRole = string.Equals(
        principal.FindFirst(AdminByRoleClaim)?.Value, "true", StringComparison.OrdinalIgnoreCase);

    return new HelpdeskPrincipal(
        JsiniUserId: jsini?.UserId,
        DisplayName: jsini?.UserName ?? principal.FindFirst("helpdesk_user_name")?.Value,
        Email: jsini?.Email,
        JsiniRoles: jsini?.Roles ?? [],
        HelpdeskUserId: helpdeskUserId,
        LinkedUserType: linkedUserType,
        CompanyId: companyId,
        IsAdmin: isAdminByRole);
  }
}
