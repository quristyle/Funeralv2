namespace HelpDeskServer.Services;

/// <summary>
/// 요청 등록에서 고를 수 있는 회사. 화면(요청 등록)의 고르개와 같은 규칙이다 —
/// 시스템관리자는 모든 회사, 헬프데스크 관리자는 「사용처 담당자」(HELP_DESK)로 맡은 회사, 그 밖에는 자기 회사뿐이다.
/// </summary>
public static class RequestCompanyPolicy {
  /// <summary>모든 회사로 올릴 수 있는 포털 역할.</summary>
  public const string SystemAdminRole = "SYSTEM_ADMINISTRATOR";

  /// <summary>맡은 회사로 올릴 수 있는 포털 역할.</summary>
  public const string ManagerRole = "HELPDESK_MANAGER";

  /// <summary>이 사람이 그 회사 이름으로 요청을 올릴 수 있는가.</summary>
  public static async Task<bool> CanPickAsync(
      HelpdeskPrincipal me, string companyId, IPortalCompanyDirectory companies, CancellationToken ct = default) {
    if (me.HasCompany && string.Equals(me.CompanyId, companyId, StringComparison.OrdinalIgnoreCase)) return true;

    if (HasRole(me, SystemAdminRole)) {
      // 회사 목록을 못 읽었으면 없는 회사인지 가릴 수 없다 — 시스템관리자의 등록까지 막지는 않는다.
      var names = await companies.GetNamesAsync(ct);
      return names.Count == 0 || names.ContainsKey(companyId);
    }

    if (HasRole(me, ManagerRole) && !string.IsNullOrWhiteSpace(me.JsiniUserId)) {
      var managed = await companies.GetManagedCompanyIdsAsync(me.JsiniUserId, ct);
      return managed.Contains(companyId, StringComparer.OrdinalIgnoreCase);
    }

    return false;
  }

  private static bool HasRole(HelpdeskPrincipal me, string role) =>
      me.JsiniRoles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
