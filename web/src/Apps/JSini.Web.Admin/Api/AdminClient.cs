using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>
/// 포털관리 화면들이 쓰는 조회·저장. 게이트웨이를 거친다.
///
/// [경로가 세 서비스로 갈린다]
///
/// 포털관리는 "관리 화면 묶음" 이지 한 서비스가 아니다. 계정·역할·부서·메뉴·
/// 다국어는 <c>auth/*</c>(AuthServer), 푸시 통계·이력은
/// <c>helpdesk/dashboard/*</c>(HelpDeskServer), 알림 설정은
/// <c>notify/*</c>(NotificationServer) 다.
///
/// <b>화면이 그 사정을 알 필요는 없다.</b> 여기서 경로만 맞춰 주고, 화면은
/// 메서드 이름으로 부른다. 서비스가 옮겨 다녀도 고칠 곳이 한 군데다.
///
/// [푸시 통계가 헬프데스크에 있는 이유]
///
/// 발송 이력 표를 헬프데스크가 들고 있다. 구독 주인을
/// <c>(int Admin.Id, UserType)</c> 로 잡아 두어 포털 로그인 아이디로는 맞출 수가
/// 없었고, 옮기려면 표를 새로 설계해야 해서 그대로 두었다
/// (결정 기록 29-notification-server "일부러 옮기지 않은 것").
/// </summary>
public sealed class AdminClient(GatewayClient gateway)
{
    // ── 공지 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<NoticeDto>> GetNoticesAsync(string? keyword = null, CancellationToken ct = default)
        => gateway.GetListAsync<NoticeDto>("auth/notices" + Query(("keyword", keyword)), ct);

    public Task<NoticeDto?> GetNoticeAsync(string id, CancellationToken ct = default)
        => gateway.GetOneAsync<NoticeDto>($"auth/notices/{id}", ct);

    public Task CreateNoticeAsync(SaveNoticeDto notice, CancellationToken ct = default)
        => gateway.PostAsync("auth/notices", notice, ct);

    public Task UpdateNoticeAsync(string id, SaveNoticeDto notice, CancellationToken ct = default)
        => gateway.PutAsync($"auth/notices/{id}", notice, ct);

    public Task DeleteNoticeAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/notices/{id}", ct);

    // ── 계정 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<AccountDto>> GetAccountsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<AccountDto>("auth/system/account/list", ct);

    public Task CreateAccountAsync(SaveAccountDto account, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/account", account, ct);

    public Task UpdateAccountAsync(string id, SaveAccountDto account, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/account/{id}", account, ct);

    public Task DeleteAccountAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/account/{id}", ct);

    // ── 역할 ────────────────────────────────────────────────────

    public Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<RoleDto>("auth/system/role/list", ct);

    public Task CreateRoleAsync(SaveRoleDto role, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/role", role, ct);

    public Task UpdateRoleAsync(string id, SaveRoleDto role, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/role/{id}", role, ct);

    public Task DeleteRoleAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/role/{id}", ct);

    /// <summary>역할에 속한 사용자.</summary>
    public Task<IReadOnlyList<AccountDto>> GetRoleUsersAsync(string roleId, CancellationToken ct = default)
        => gateway.GetListAsync<AccountDto>($"auth/system/role-permission/roles/{roleId}/users", ct);

    /// <summary>역할에 넣을 수 있는 사용자 (아직 안 속한 사람).</summary>
    public Task<IReadOnlyList<AccountDto>> GetRoleEligibleUsersAsync(string roleId, CancellationToken ct = default)
        => gateway.GetListAsync<AccountDto>($"auth/system/role-permission/roles/{roleId}/eligible-users", ct);

    public Task AssignRoleUsersAsync(string roleId, IReadOnlyList<string> userIds, CancellationToken ct = default)
        => gateway.PostAsync($"auth/system/role-permission/roles/{roleId}/users/assign", new { userIds }, ct);

    public Task RemoveRoleUserAsync(string roleId, string userId, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/role-permission/roles/{roleId}/users/{userId}", ct);

    /// <summary>역할이 볼 수 있는 메뉴와 그 권한.</summary>
    public Task<IReadOnlyList<RoleMenuDto>> GetRoleMenusAsync(string roleId, CancellationToken ct = default)
        => gateway.GetListAsync<RoleMenuDto>($"auth/system/role-permission/roles/{roleId}/menus", ct);

    /// <summary>역할-메뉴 권한을 통째로 저장한다. 목록에 없는 메뉴는 권한이 풀린다.</summary>
    public Task SaveRoleMenusAsync(string roleId, IReadOnlyList<RoleMenuDto> menus, CancellationToken ct = default)
        => gateway.PostAsync($"auth/system/role-permission/roles/{roleId}/menus/save", new { menus }, ct);

    // ── 회사 · 부서 ─────────────────────────────────────────────

    public Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(CancellationToken ct = default)
        => gateway.GetListAsync<CompanyDto>("auth/system/companies", ct);

    public Task CreateCompanyAsync(SaveCompanyDto company, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/companies", company, ct);

    public Task UpdateCompanyAsync(string id, SaveCompanyDto company, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/companies/{id}", company, ct);

    public Task DeleteCompanyAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/companies/{id}", ct);

    public Task<IReadOnlyList<AccountDto>> GetCompanyUsersAsync(string companyId, CancellationToken ct = default)
        => gateway.GetListAsync<AccountDto>($"auth/system/companies/{companyId}/users", ct);

    public Task<IReadOnlyList<DeptDto>> GetDeptsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<DeptDto>("auth/system/dept/list", ct);

    public Task CreateDeptAsync(SaveDeptDto dept, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/dept", dept, ct);

    public Task UpdateDeptAsync(string id, SaveDeptDto dept, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/dept/{id}", dept, ct);

    public Task DeleteDeptAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/dept/{id}", ct);

    public Task<IReadOnlyList<AccountDto>> GetDeptUsersAsync(string deptId, CancellationToken ct = default)
        => gateway.GetListAsync<AccountDto>($"auth/system/dept/{deptId}/users", ct);

    // ── 다국어 ──────────────────────────────────────────────────

    public Task<IReadOnlyList<I18nResourceDto>> GetI18nAsync(string locale, CancellationToken ct = default)
        => gateway.GetListAsync<I18nResourceDto>($"auth/system/i18n/{locale}", ct);

    public Task<IReadOnlyList<I18nResourceDto>> GetAllI18nAsync(CancellationToken ct = default)
        => gateway.GetListAsync<I18nResourceDto>("auth/system/i18n/list", ct);

    public Task CreateI18nAsync(I18nResourceDto item, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/i18n", item, ct);

    public Task UpdateI18nAsync(int id, I18nResourceDto item, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/i18n/{id}", item, ct);

    public Task DeleteI18nAsync(int id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/i18n/{id}", ct);

    // ── 메타데이터 (biz-select 설정) ────────────────────────────

    public Task<IReadOnlyList<BizSelectConfigDto>> GetBizSelectConfigsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<BizSelectConfigDto>("auth/system/biz-select/configs", ct);

    public Task CreateBizSelectConfigAsync(BizSelectConfigDto config, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/biz-select/config", config, ct);

    public Task UpdateBizSelectConfigAsync(string id, BizSelectConfigDto config, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/biz-select/config/{id}", config, ct);

    public Task DeleteBizSelectConfigAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/biz-select/config/{id}", ct);

    // ── 상태 ────────────────────────────────────────────────────

    /// <summary>
    /// 배포 현황. GitHub 실행 이력과 도커 컨테이너 상태를 서버가 합쳐 준다.
    ///
    /// <b>목록이 아니라 단건이다</b> — 응답이 <c>{ github, docker }</c> 한 덩어리다.
    /// </summary>
    public Task<DeployStatusDto?> GetDeployStatusAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<DeployStatusDto>("auth/deploy-status", ct);

    public Task<PlayerReleaseDto?> GetPlayerReleaseStatusAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<PlayerReleaseDto>("auth/system/player-release/status", ct);

    // ── 푸시 (HelpDeskServer 의 대시보드 그룹) ──────────────────

    public Task<PushStatsDto?> GetPushStatsAsync(int days = 7, CancellationToken ct = default)
        => gateway.GetOneAsync<PushStatsDto>($"helpdesk/dashboard/push-stats?days={days}", ct);

    public Task<IReadOnlyList<PushTrendPointDto>> GetPushTrendAsync(
        string interval = "daily", int days = 30, CancellationToken ct = default)
        => gateway.GetListAsync<PushTrendPointDto>(
            $"helpdesk/dashboard/push-success-trend?interval={interval}&days={days}", ct);

    public Task<IReadOnlyList<PushFailureReasonDto>> GetPushFailureReasonsAsync(
        int days = 7, int topN = 5, CancellationToken ct = default)
        => gateway.GetListAsync<PushFailureReasonDto>(
            $"helpdesk/dashboard/push-failure-reasons?days={days}&topN={topN}", ct);

    public Task<IReadOnlyList<PushLogDto>> GetPushLogsAsync(
        int page = 1, int pageSize = 50, string? reason = null, CancellationToken ct = default)
        => gateway.GetListAsync<PushLogDto>(
            "helpdesk/dashboard/push-logs" + Query(("page", page), ("pageSize", pageSize), ("failureReason", reason)), ct);

    /// <summary>내 알림 이력 (알림함).</summary>
    public Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
        => gateway.GetListAsync<NotificationDto>(
            "helpdesk/push/notifications" + Query(("page", page), ("pageSize", pageSize)), ct);

    public Task MarkNotificationReadAsync(int id, CancellationToken ct = default)
        => gateway.PostAsync($"helpdesk/push/notifications/{id}/read", new { }, ct);

    // ── 알림 설정 (NotificationServer) ─────────────────────────

    public Task<NotificationPreferenceDto?> GetMyPreferencesAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<NotificationPreferenceDto>("notify/preferences/me", ct);

    public Task SaveMyPreferencesAsync(NotificationPreferenceDto pref, CancellationToken ct = default)
        => gateway.PutAsync("notify/preferences/me", pref, ct);

    public Task<IReadOnlyList<PushSubscriptionDto>> GetMySubscriptionsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<PushSubscriptionDto>("notify/subscriptions/me", ct);

    /// <summary>시험 발송. 내 기기로 한 통 보낸다.</summary>
    public Task SendTestPushAsync(CancellationToken ct = default)
        => gateway.PostAsync("notify/push/test", new { }, ct);

    // ── 내 정보 ─────────────────────────────────────────────────

    public Task<UserInfoDto?> GetMyInfoAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<UserInfoDto>("auth/user/info", ct);

    public Task<IReadOnlyList<UserActivityDto>> GetMyActivityAsync(CancellationToken ct = default)
        => gateway.GetListAsync<UserActivityDto>("auth/user/activity", ct);

    /// <summary>쿼리스트링을 만든다. 값이 null 이거나 빈 문자열이면 뺀다.</summary>
    private static string Query(params (string Key, object? Value)[] parameters)
    {
        var parts = new List<string>();

        foreach (var (key, value) in parameters)
        {
            var text = value switch
            {
                null => null,
                string s => string.IsNullOrWhiteSpace(s) ? null : s,
                _ => value.ToString(),
            };

            if (text is not null)
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(text)}");
            }
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
