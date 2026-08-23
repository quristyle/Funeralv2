namespace HelpDeskServer.Services;

/// <summary>
/// 헬프데스크 신원 해석 정책.
/// </summary>
public class HelpdeskIdentityOptions {
  /// <summary>설정 섹션 이름.</summary>
  public const string SectionName = "HelpdeskIdentity";

  /// <summary>
  /// 헬프데스크 담당자(admin)로 대우할 JSini 역할 목록.
  ///
  /// 인증·권한은 포털이 단독으로 맡는다. 그런데 헬프데스크의 담당자 판정은
  /// <c>jsini.auth_user_links</c> 에 사람이 직접 이어 준 연결에만 의존해 왔다.
  /// 그래서 포털에서 관리자 역할을 받은 계정도 연결이 없으면 헬프데스크에서는
  /// 아무 권한이 없는 사람이 된다 — 관리 업무를 볼 수 있어야 하는데 막힌다.
  ///
  /// 여기 적힌 역할을 가진 계정은 연결이 없어도 담당자 권한으로 조회·관리한다.
  /// <b>연결이 필요한 것과 필요하지 않은 것을 구분한다</b> — 자세한 것은
  /// <see cref="HelpdeskPrincipal"/> 주석 참고.
  /// </summary>
  public string[] AdminRoles { get; set; } = ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR"];
}

/// <summary>
/// 지금 요청을 보낸 사람. 헬프데스크가 신원을 판단할 때 보는 유일한 창구다.
///
/// 두 가지를 한 자리에 모은다.
///
/// <list type="bullet">
///   <item>
///     <b>포털 계정</b>(<see cref="JsiniUserId"/> · <see cref="JsiniRoles"/>) — 누구인가, 무엇을 할 수 있는가.
///     인증·권한의 정본이다.
///   </item>
///   <item>
///     <b>헬프데스크 내부 레코드</b>(<see cref="HelpdeskUserId"/>) — 기존 업무 데이터가 가리키는 대상.
///     요청 작성자·담당자·댓글 작성자가 모두 이 숫자 ID 를 참조하므로 버릴 수 없다.
///   </item>
/// </list>
///
/// <para>
/// 둘을 나눈 이유는 <b>연결이 없어도 할 수 있는 일이 있기 때문</b>이다.
/// </para>
///
/// <list type="table">
///   <item>
///     <term>연결이 필요 없다</term>
///     <description>
///       조회·집계·관리 — "무엇을 볼 수 있는가" 는 포털 역할이 정한다.
///       담당자 목록 조회, 전체 요청 현황, 고객 목록 같은 것들이다.
///     </description>
///   </item>
///   <item>
///     <term>연결이 필요하다</term>
///     <description>
///       내 것을 가리키는 일 — "내가 쓴 댓글", "나에게 배정된 요청", 내 알림 구독처럼
///       헬프데스크 내부 ID 로 행을 찾거나 만들어야 하는 것들이다.
///       이때는 <see cref="IsLinked"/> 가 false 면 할 수 없고, 그 사실을 화면에 알려야 한다.
///     </description>
///   </item>
/// </list>
///
/// <para>
/// 전에는 이 구분이 없어서 <b>연결이 없으면 조회조차 막혔다</b>. 포털 계정 46개 중 연결된 것은
/// 하나뿐이라, 사실상 한 사람만 헬프데스크를 쓸 수 있는 상태였다.
/// </para>
/// </summary>
/// <param name="JsiniUserId">포털 로그인 아이디. 헬프데스크 자체 토큰으로 들어온 요청이면 null</param>
/// <param name="DisplayName">표시 이름 (포털 실명 우선)</param>
/// <param name="Email">대표 이메일</param>
/// <param name="JsiniRoles">포털에서 배정된 역할 식별자 목록</param>
/// <param name="HelpdeskUserId">연결된 헬프데스크 내부 계정 ID. 연결이 없으면 null</param>
/// <param name="LinkedUserType">연결된 계정 종류 — <c>admin</c> / <c>customer</c> / null</param>
/// <param name="CompanyId">고객으로 연결된 경우의 소속 회사 ID</param>
/// <param name="IsAdmin">담당자 권한이 있는가 (연결이 admin 이거나 포털 역할이 관리자)</param>
public sealed record HelpdeskPrincipal(
    string? JsiniUserId,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> JsiniRoles,
    int? HelpdeskUserId,
    string? LinkedUserType,
    int? CompanyId,
    bool IsAdmin) {

  /// <summary>헬프데스크 내부 레코드에 이어져 있는가. 내 것을 가리키는 일에 필요하다.</summary>
  public bool IsLinked => HelpdeskUserId.HasValue;

  /// <summary>고객으로 연결된 계정인가. 회사 단위로 범위를 좁힐 때 쓴다.</summary>
  public bool IsCustomer =>
      string.Equals(LinkedUserType, "customer", StringComparison.OrdinalIgnoreCase);

  /// <summary>담당자로 연결된 계정인가 (레코드가 실제로 있는 경우).</summary>
  public bool IsLinkedAdmin =>
      string.Equals(LinkedUserType, "admin", StringComparison.OrdinalIgnoreCase);

  /// <summary>
  /// 담당자 권한은 있으나 헬프데스크 레코드는 없는 상태.
  /// 관리 조회는 되지만 "나에게 배정된 요청" 같은 것은 비어 있다 — 화면이 그 사실을 알려야 한다.
  /// </summary>
  public bool IsUnlinkedAdmin => IsAdmin && !IsLinked;

  /// <summary>화면·기록에 남길 사람 표기.</summary>
  public string Who =>
      JsiniUserId ?? (HelpdeskUserId?.ToString() ?? "unknown");
}

/// <summary>요청에서 <see cref="HelpdeskPrincipal"/> 을 꺼내는 확장 메서드.</summary>
public static class HelpdeskPrincipalExtensions {

  /// <summary>
  /// 포털 역할로 담당자 권한이 인정되었음을 표시하는 클레임.
  /// 역할 목록 설정을 읽어야 하므로 판정은 미들웨어(DI 가 있는 곳)에서 하고, 결과만 클레임으로 남긴다.
  /// </summary>
  public const string AdminByRoleClaim = "helpdesk_admin_by_role";

  /// <summary>지금 요청을 보낸 사람을 돌려준다.</summary>
  public static HelpdeskPrincipal GetHelpdeskPrincipal(this HttpContext context) {
    var jsini = context.GetJsiniUser();
    var principal = context.User;

    int? helpdeskUserId = int.TryParse(principal.FindFirst("uid")?.Value, out var uid) ? uid : null;
    var linkedUserType = principal.FindFirst("login_type")?.Value;
    int? companyId = int.TryParse(principal.FindFirst("company_id")?.Value, out var cid) ? cid : null;

    var isLinkedAdmin = string.Equals(linkedUserType, "admin", StringComparison.OrdinalIgnoreCase);
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
        IsAdmin: isLinkedAdmin || isAdminByRole);
  }
}
