namespace HelpDeskServer.Services;

/// <summary>
/// 헬프데스크 신원 해석 정책.
/// </summary>
public class HelpdeskIdentityOptions {
  /// <summary>설정 섹션 이름.</summary>
  public const string SectionName = "HelpdeskIdentity";

  /// <summary>
  /// 헬프데스크 <b>관리자</b>로 대우할 JSini 역할 목록. <b>이 목록이 관리자의 정의다.</b>
  ///
  /// <para>
  /// 인증·권한은 포털이 단독으로 맡는다. 관리자는 포털의 역할 관리
  /// (<c>/admin/auth/role</c>)에서 정하는 것이고, 헬프데스크는 그 결과를 받아 쓸 뿐이다 —
  /// <b>여기 적힌 역할을 가졌으면 관리자, 그 밖의 모든 사람은 고객이다</b>
  /// (2026-10-05 규칙). 역할을 가진 사람은 <b>고객이면서 관리자</b>이기도 하다 —
  /// 관리자도 요청을 올리기 때문이다. 둘을 갈라 보여 주는 자리에서는 관리자로 적는다.
  /// </para>
  ///
  /// <para>
  /// <b>연결 종류(<c>auth_user_links.user_type</c>)는 더 이상 관리자 판정에 쓰지 않는다.</b>
  /// 그 값은 「이 포털 계정이 헬프데스크의 어느 줄을 가리키는가」일 뿐이고 권한이 아니다.
  /// 전에는 연결이 <c>admin</c> 이기만 하면 관리자였는데, 그러면 관리자를 세우고 거두는
  /// 자리가 포털 역할표와 헬프데스크 연결표 둘이 되어 <b>어느 쪽이 맞는지 아무도 말할 수
  /// 없다.</b> 연결이 여전히 정하는 것은 「무엇이 내 것인가」 하나다(<see cref="HelpdeskPrincipal.IsLinked"/>).
  /// </para>
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

  /// <summary>
  /// 고객인가 — <b>관리자가 아닌 모든 사람</b>이다.
  /// </summary>
  /// <remarks>
  /// 연결 종류가 <c>customer</c> 인가로 가르던 것을 2026-10-05 에 뒤집었다. 연결은
  /// 운영에 <b>한 줄뿐</b>이라, 그것으로 가르면 포털 계정 마흔몇이 고객도 관리자도
  /// 아닌 상태가 되어 <b>회사 단위로 좁히는 조건이 통째로 안 걸렸다</b> — 좁히지
  /// 않은 쪽은 언제나 「남의 것까지 보인다」로 틀린다.
  /// 고객 번호가 필요한 일은 <see cref="IsLinked"/> 를 따로 본다.
  /// </remarks>
  public bool IsCustomer => !IsAdmin;

  /// <summary>
  /// 헬프데스크 <b>담당자 줄</b>(<c>admin</c>)에 이어져 있는가.
  /// </summary>
  /// <remarks>
  /// <b>권한이 아니다</b> — 「나에게 배정된 요청」처럼 <c>admin.id</c> 로 행을 찾아야
  /// 하는 일에만 쓴다. 관리자인지는 <see cref="IsAdmin"/> 이 정한다.
  /// </remarks>
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

  /// <summary>비어 있지 않은 첫 값. 없으면 null.</summary>
  private static string? FirstNonBlank(params string?[] values) =>
      values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

  /// <summary>지금 요청을 보낸 사람을 돌려준다.</summary>
  public static HelpdeskPrincipal GetHelpdeskPrincipal(this HttpContext context) {
    var jsini = context.GetJsiniUser();
    var principal = context.User;

    int? helpdeskUserId = int.TryParse(principal.FindFirst("uid")?.Value, out var uid) ? uid : null;
    var linkedUserType = principal.FindFirst("login_type")?.Value;
    // 회사는 포털이 단독으로 관리한다. 그래서 **포털 토큰이 실어 주는 회사를
    // 먼저 믿는다** — 헬프데스크의 `company_id` 클레임은 연결된 고객 줄에 적힌
    // 값이고, 그 값 자체도 이제 포털 회사 아이디다.
    var companyId = FirstNonBlank(
        jsini?.CompanyId,
        principal.FindFirst("company_id")?.Value);

    // **관리자는 포털 역할 하나로 정해진다**(`HelpdeskIdentityOptions.AdminRoles`).
    // 연결이 `admin` 이라는 것만으로 관리자로 치던 것을 2026-10-05 에 걷었다 —
    // 관리자를 세우고 거두는 자리가 둘이면 어느 쪽이 맞는지 아무도 말할 수 없다.
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
