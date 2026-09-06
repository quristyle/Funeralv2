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

    /// <summary>
    /// 계정을 만든다.
    ///
    /// <b>돌려받는 값을 버리면 안 된다.</b> 서버가 발급한 첫 비밀번호가
    /// <see cref="AccountDto.InitialPassword"/> 에 실려 오는데, 저장은 해시로
    /// 되어 있어 <b>이 응답을 놓치면 아무도 그 값을 알 수 없다.</b>
    /// </summary>
    public Task<AccountDto?> CreateAccountAsync(SaveAccountDto account, CancellationToken ct = default)
        => gateway.PostAsync<AccountDto>("auth/system/account", account, ct);

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

    /// <summary>
    /// 역할에 걸린 사람.
    ///
    /// <b>돌려주는 모양이 <see cref="AccountDto"/> 와 다르다</b> —
    /// <c>roleNames</c> 가 목록이 아니라 문자열 하나다. 그래서 전용 타입으로 받는다.
    /// </summary>
    public Task<IReadOnlyList<RoleUserDto>> GetRoleUsersAsync(string roleId, CancellationToken ct = default)
        => gateway.GetListAsync<RoleUserDto>($"auth/system/role-permission/roles/{roleId}/users", ct);

    /// <summary>역할에 넣을 수 있는 사용자 (아직 안 속한 사람).</summary>
    public Task<IReadOnlyList<RoleUserDto>> GetRoleEligibleUsersAsync(string roleId, CancellationToken ct = default)
        => gateway.GetListAsync<RoleUserDto>($"auth/system/role-permission/roles/{roleId}/eligible-users", ct);

    /// <summary>
    /// 역할에 사람을 건다.
    ///
    /// <b>본문 이름이 <c>accountIds</c> 다.</b> 한동안 <c>userIds</c> 로 보내고
    /// 있었는데, 서버는 그 이름을 모르므로 빈 목록으로 읽고 아무도 걸지 않은 채
    /// 200 을 돌려준다 — 화면에는 성공으로 보이고 아무 일도 안 일어난다.
    /// </summary>
    public Task AssignRoleUsersAsync(string roleId, IReadOnlyList<string> accountIds, CancellationToken ct = default)
        => gateway.PostAsync($"auth/system/role-permission/roles/{roleId}/users/assign", new { accountIds }, ct);

    public Task RemoveRoleUserAsync(string roleId, string userId, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/role-permission/roles/{roleId}/users/{userId}", ct);

    /// <summary>역할이 볼 수 있는 메뉴와 그 권한.</summary>
    public Task<IReadOnlyList<RoleMenuDto>> GetRoleMenusAsync(string roleId, CancellationToken ct = default)
        => gateway.GetListAsync<RoleMenuDto>($"auth/system/role-permission/roles/{roleId}/menus", ct);

    /// <summary>역할-메뉴 권한을 통째로 저장한다. 목록에 없는 메뉴는 권한이 풀린다.</summary>
    public Task SaveRoleMenusAsync(string roleId, IReadOnlyList<RoleMenuDto> menus, CancellationToken ct = default)
        => gateway.PostAsync($"auth/system/role-permission/roles/{roleId}/menus/save", new { menus }, ct);

    /// <summary>
    /// 메뉴 하나를 기준으로 본 권한 현황 — <b>「이 메뉴는 누가 쓸 수 있나」</b>.
    ///
    /// 읽기만 있다. 저장은 <see cref="SaveRoleMenusAsync"/> 와
    /// <see cref="RemoveRoleScopeAsync"/> 를 그대로 쓴다 — 같은 일을 하는
    /// 저장 경로를 둘로 만들면 한쪽에만 규칙이 붙는다.
    /// </summary>
    public Task<MenuRoleDto?> GetMenuRoleAsync(string menuId, CancellationToken ct = default)
        => gateway.GetOneAsync<MenuRoleDto>($"auth/system/menu-role/{menuId}", ct);

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

    /// <summary>
    /// 아직 어느 회사에도 안 속한 사람.
    ///
    /// <b>회사를 가리지 않는다</b> — 「소속이 없는 사람」이라 어느 회사에 넣든
    /// 후보가 같다. 서버가 회사 식별자를 받지 않는 이유가 그것이다.
    /// </summary>
    public Task<IReadOnlyList<AccountDto>> GetCompanyEligibleUsersAsync(CancellationToken ct = default)
        => gateway.GetListAsync<AccountDto>("auth/system/companies/eligible-users", ct);

    /// <summary>회사에 사람을 넣는다. 여럿을 한 번에 보낸다.</summary>
    public Task AssignCompanyUsersAsync(
        string companyId, IReadOnlyList<string> userIds, CancellationToken ct = default)
        => gateway.PostAsync($"auth/system/companies/{companyId}/users", userIds, ct);

    /// <summary>
    /// 소속을 푼다. <b>회사 식별자를 받지 않는다</b> — 사람에게서 소속을 떼는
    /// 일이라 어느 회사였는지는 서버가 안다. 여럿을 한 번에 보낸다.
    /// </summary>
    public Task RemoveCompanyUsersAsync(IReadOnlyList<string> userIds, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/companies/users/remove", userIds, ct);

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

    /// <summary>
    /// 쌓인 도커 이미지를 정리한다 (D17). <b>관리자만</b> — 판정은 서버가 한다.
    ///
    /// <para>
    /// <paramref name="dryRun"/> 이 참이면 <b>지우지 않고 지울 목록만</b> 받는다.
    /// 화면이 그것을 확인 창에 그대로 띄운다. 목록을 화면이 스스로 계산하지
    /// 않는 이유는 서버 쪽 주석에 있다 — 규칙이 두 곳에 생기면 <b>보여 준 것과
    /// 지워지는 것이 달라진다.</b>
    /// </para>
    /// </summary>
    public Task<DockerCleanupDto?> CleanupDockerImagesAsync(bool dryRun, CancellationToken ct = default)
        => gateway.PostAsync<DockerCleanupDto>(
            $"auth/deploy-status/cleanup?dryRun={(dryRun ? "true" : "false")}", body: null, ct);

    public Task<PlayerReleaseDto?> GetPlayerReleaseStatusAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<PlayerReleaseDto>("auth/system/player-release/status", ct);

    // ── 푸시 (HelpDeskServer 의 대시보드 그룹) ──────────────────

    // 아래 넷은 **헬프데스크 서비스**의 통계다. 그쪽 봉투는 `data` 안에
    // `result` 한 겹이 없어서, 그 겹을 전제하는 GetOneAsync/GetListAsync 로는
    // 「응답을 해석하지 못했습니다」로 끝난다. 모양을 가리지 않는 쪽을 쓴다.
    public Task<PushStatsDto?> GetPushStatsAsync(int days = 7, CancellationToken ct = default)
        => gateway.GetFlexibleAsync<PushStatsDto>($"helpdesk/dashboard/push-stats?days={days}", ct);

    public Task<IReadOnlyList<PushTrendPointDto>> GetPushTrendAsync(
        string interval = "daily", int days = 30, CancellationToken ct = default)
        => gateway.GetFlexibleListAsync<PushTrendPointDto>(
            $"helpdesk/dashboard/push-success-trend?interval={interval}&days={days}", ct);

    public Task<IReadOnlyList<PushFailureReasonDto>> GetPushFailureReasonsAsync(
        int days = 7, int topN = 5, CancellationToken ct = default)
        => gateway.GetFlexibleListAsync<PushFailureReasonDto>(
            $"helpdesk/dashboard/push-failure-reasons?days={days}&topN={topN}", ct);

    public Task<IReadOnlyList<PushLogDto>> GetPushLogsAsync(
        int page = 1, int pageSize = 50, string? reason = null, CancellationToken ct = default)
        => gateway.GetFlexibleListAsync<PushLogDto>(
            "helpdesk/dashboard/push-logs" + Query(("page", page), ("pageSize", pageSize), ("failureReason", reason)), ct);

    /// <summary>내 알림 이력 (알림함).</summary>
    public Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
        => gateway.GetFlexibleListAsync<NotificationDto>(
            "helpdesk/push/notifications" + Query(("page", page), ("pageSize", pageSize)), ct);

    public Task MarkNotificationReadAsync(int id, CancellationToken ct = default)
        => gateway.PostAsync($"helpdesk/push/notifications/{id}/read", new { }, ct);

    /// <summary>게이트웨이가 서비스를 하나씩 눌러 본 결과. 자기 상태도 함께 온다.</summary>
    public Task<GatewayStatusDto?> GetGatewayStatusAsync(CancellationToken ct = default)
        => gateway.GetFlexibleAsync<GatewayStatusDto>("gateway/status", ct);

    /// <summary>서비스별 응답 상태만 꺼내 준다. 못 읽으면 빈 목록.</summary>
    public async Task<IReadOnlyList<ServiceHealth>> GetServiceHealthAsync(CancellationToken ct = default)
        => (await GetGatewayStatusAsync(ct))?.Services ?? [];

    /// <summary>
    /// AI 제공자 상태.
    ///
    /// 헬스체크와 다른 것을 본다 — 컨테이너가 떠 있어도 <b>키가 없거나 하루
    /// 한도를 다 썼으면</b> 대화가 안 되고, 그것은 응답 확인으로 안 잡힌다.
    /// </summary>
    public Task<AiProviderStatusDto?> GetAiProvidersAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<AiProviderStatusDto>("ai/providers", ct);

    /// <summary>제공자별로 고를 수 있는 모델 목록.</summary>
    public Task<IReadOnlyList<AiProviderModelsDto>> GetAiModelsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<AiProviderModelsDto>("ai/models", ct);

    /// <summary>
    /// 제공자를 하나 실제로 눌러 본다 (「정밀 확인」).
    ///
    /// <b>돈이 드는 호출이다</b> — 유료 제공자에게 질문을 하나 던진다.
    /// 그래서 화면이 자동으로 부르지 않고 사람이 눌렀을 때만 나간다.
    /// </summary>
    public Task<AiDeepCheckDto?> DeepCheckAiAsync(string provider, CancellationToken ct = default)
        => gateway.PostAsync<AiDeepCheckDto>(
            $"ai/health/deep{Query(("provider", provider))}", new { }, ct);

    /// <summary>
    /// 한글 이름을 주면 AI 가 쓸 만한 <b>영문 코드</b>를 하나 골라 준다.
    /// 「사용 여부」→ <c>USE_YN</c> 같은 식이다.
    ///
    /// <para>
    /// <b>제공자·모델을 지정하지 않는다.</b> 옛 Vue 화면은 사용자가 환경설정에서
    /// 고른 것을 실어 보냈는데, 지금 포털에는 그 설정 자체가 없다. 비워 두면
    /// 서버가 기본 제공자를 쓴다 — 나중에 설정이 생기면 여기에 붙인다.
    /// </para>
    ///
    /// <para>
    /// <paramref name="natural"/> 은 축약형(<c>USE_YN</c>) 대신 풀어 쓴 영문을
    /// 달라는 뜻이다. 코드 값에는 축약형이 맞아서 기본이 <c>false</c> 다.
    /// </para>
    ///
    /// <para><b>돈이 드는 호출이다.</b> 사람이 이름을 다 친 뒤에만 나가게 한다.</para>
    /// </summary>
    public Task<string?> SuggestCodeAsync(string word, bool natural = false, CancellationToken ct = default)
        => gateway.GetOneAsync<string>(
            $"ai/suggest-code{Query(("word", word), ("natural", natural))}", ct);

    // ── 알림 설정 (NotificationServer) ─────────────────────────

    // 알림 서비스의 게이트웨이 접두사는 **`notification`** 이고, 서비스 안의
    // 묶음은 **`/notifications`** 다. 둘 다 있어야 한다 — `notify/...` 로
    // 부르던 동안 네 화면이 조용히 404 였다.

    public Task<NotificationSettingsDto?> GetMyPreferencesAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<NotificationSettingsDto>("notification/notifications/preferences/me", ct);

    /// <summary>
    /// 설정을 저장한다. <b>감싸지 않고 설정만</b> 보낸다 — 서버가 받는 것은
    /// 응답의 <c>preference</c> 자리에 해당하는 모양이다.
    /// </summary>
    public Task SaveMyPreferencesAsync(NotificationPreferenceDto pref, CancellationToken ct = default)
        => gateway.PutAsync("notification/notifications/preferences/me", pref, ct);

    public Task<PushSubscriptionListDto?> GetMySubscriptionsAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<PushSubscriptionListDto>("notification/notifications/subscriptions/me", ct);

    /// <summary>시험 발송. 내 기기로 한 통 보낸다.</summary>
    public Task SendTestPushAsync(CancellationToken ct = default)
        => gateway.PostAsync("notification/notifications/push/test", new { }, ct);

    // ── 메뉴 ────────────────────────────────────────────────────

    /// <summary>
    /// 메뉴 전체를 나무 모양으로. 권한으로 거르지 않은 <b>원본</b>이다 —
    /// 관리 화면은 자기가 못 보는 메뉴도 고칠 수 있어야 한다.
    ///
    /// <paramref name="locale"/> 을 주면 제목의 다국어 키를 서버가 옮겨
    /// <c>meta.titleText</c> 에 담아 준다.
    /// </summary>
    public Task<IReadOnlyList<SystemMenuDto>> GetSystemMenusAsync(
        string? locale = null, CancellationToken ct = default)
        => gateway.GetListAsync<SystemMenuDto>("auth/system/menu/list" + Query(("locale", locale)), ct);

    public Task CreateSystemMenuAsync(SaveSystemMenuDto menu, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/menu", menu, ct);

    public Task UpdateSystemMenuAsync(string id, SaveSystemMenuDto menu, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/menu/{id}", menu, ct);

    public Task DeleteSystemMenuAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/menu/{id}", ct);

    /// <summary>
    /// 이름이 이미 쓰이고 있는지. 등록 폼이 저장 전에 묻는다.
    ///
    /// <paramref name="excludeId"/> 는 수정할 때 <b>자기 자신을 빼기</b> 위한
    /// 것이다. 안 빼면 이름을 안 바꾸고 저장해도 "이미 있다" 가 된다.
    /// </summary>
    public Task<bool> MenuNameExistsAsync(string name, string? excludeId = null, CancellationToken ct = default)
        => ExistsAsync("auth/system/menu/name-exists" + Query(("name", name), ("id", excludeId)), ct);

    public Task<bool> MenuPathExistsAsync(string path, string? excludeId = null, CancellationToken ct = default)
        => ExistsAsync("auth/system/menu/path-exists" + Query(("path", path), ("id", excludeId)), ct);

    // ── 공통코드 ────────────────────────────────────────────────

    public Task<IReadOnlyList<CommonCodeGroupDto>> GetCodeGroupsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<CommonCodeGroupDto>("auth/system/common-code/groups", ct);

    public Task CreateCodeGroupAsync(SaveCommonCodeGroupDto group, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/common-code/groups", group, ct);

    public Task UpdateCodeGroupAsync(string id, SaveCommonCodeGroupDto group, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/common-code/groups/{id}", group, ct);

    public Task DeleteCodeGroupAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/common-code/groups/{id}", ct);

    /// <summary>
    /// 한 묶음의 코드들. <paramref name="hierarchical"/> 이면 <c>Children</c> 을
    /// 채운 나무로, 아니면 평평한 목록으로 온다.
    ///
    /// <b>주소가 묶음 <i>코드</i>다</b> — 식별자가 아니다. 서버가 그렇게 열어
    /// 두었고, 코드가 사람이 정하는 값이라 링크로 주고받기 좋다.
    /// </summary>
    public Task<IReadOnlyList<CommonCodeDto>> GetCodesAsync(
        string groupCode, bool hierarchical = false, CancellationToken ct = default)
        => gateway.GetListAsync<CommonCodeDto>(
            $"auth/system/common-code/{Uri.EscapeDataString(groupCode)}" + Query(("hierarchical", hierarchical)), ct);

    public Task CreateCodeAsync(SaveCommonCodeDto code, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/common-code", code, ct);

    public Task UpdateCodeAsync(string id, SaveCommonCodeDto code, CancellationToken ct = default)
        => gateway.PutAsync($"auth/system/common-code/{id}", code, ct);

    public Task DeleteCodeAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/system/common-code/{id}", ct);

    // ── 배포 도구 ───────────────────────────────────────────────
    //
    // **거는 것은 여기 없다.** 실행(`POST auth/release/{key}`)은 운영 서버에
    // 스크립트를 돌리는 일이라 되돌릴 수 없다. 결정 목록 D2 가 정해질 때까지
    // 조회만 붙인다.

    public Task<ReleaseTargetListDto?> GetReleaseTargetsAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<ReleaseTargetListDto>("auth/release/targets", ct);

    public Task<IReadOnlyList<ReleaseRunDto>> GetReleaseRunsAsync(int take = 30, CancellationToken ct = default)
        => gateway.GetListAsync<ReleaseRunDto>("auth/release/runs" + Query(("take", take)), ct);

    /// <summary>실행 한 건. 진행 기록(<c>Events</c>)이 함께 온다.</summary>
    public Task<ReleaseRunDto?> GetReleaseRunAsync(string id, CancellationToken ct = default)
        => gateway.GetOneAsync<ReleaseRunDto>($"auth/release/runs/{id}", ct);

    // ── 역할 범위 (회사 · 부서 · 사람) ──────────────────────────

    /// <summary>회사 하나의 조직 나무와 각 단계에 걸린 역할.</summary>
    public Task<RoleScopeTreeDto?> GetRoleScopeTreeAsync(string companyId, CancellationToken ct = default)
        => gateway.GetOneAsync<RoleScopeTreeDto>(
            "auth/system/role-scope/tree" + Query(("companyId", companyId)), ct);

    /// <summary>검색용 사람 목록. 회사·부서 이름이 함께 담겨 온다.</summary>
    public Task<IReadOnlyList<AccountPickDto>> GetRoleScopeAccountsAsync(CancellationToken ct = default)
        => gateway.GetListAsync<AccountPickDto>("auth/system/role-scope/accounts", ct);

    /// <summary>
    /// 그 계정에 <b>실제로</b> 적용되는 역할과 그것이 온 단계.
    ///
    /// 회사 + 부서 + 사람을 합친 결과다. 사람 단계에서 뺐는데도 남아 있는
    /// 역할이 있다면 위 단계에서 온 것이고, 그 사실을 <c>Sources</c> 가 말해 준다.
    /// </summary>
    public Task<EffectiveRolesDto?> GetEffectiveRolesAsync(string accountId, CancellationToken ct = default)
        => gateway.GetOneAsync<EffectiveRolesDto>(
            "auth/system/role-scope/effective" + Query(("accountId", accountId)), ct);

    /// <summary>그 계정이 볼 수 있는 메뉴와 볼 수 없는 메뉴.</summary>
    public Task<AccountMenuAccessDto?> GetAccountMenuAccessAsync(string accountId, CancellationToken ct = default)
        => gateway.GetOneAsync<AccountMenuAccessDto>(
            "auth/system/role-scope/menus" + Query(("accountId", accountId)), ct);

    /// <summary>대상에 역할을 건다. 이미 걸려 있으면 그대로 둔다.</summary>
    public Task AssignRoleScopeAsync(string kind, string targetId, string roleId, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/role-scope/assign",
            new RoleAssignRequest { Kind = kind, TargetId = targetId, RoleId = roleId }, ct);

    /// <summary>대상에서 역할을 푼다. 걸려 있지 않아도 오류가 아니다.</summary>
    public Task RemoveRoleScopeAsync(string kind, string targetId, string roleId, CancellationToken ct = default)
        => gateway.PostAsync("auth/system/role-scope/remove",
            new RoleAssignRequest { Kind = kind, TargetId = targetId, RoleId = roleId }, ct);

    // ── 내 정보 ─────────────────────────────────────────────────

    /// <summary>
    /// 비밀번호를 바꾼다.
    ///
    /// <para>
    /// <b>쿠키를 다시 굽지 않는다.</b> 서버는 비밀번호 해시와 만료 시계만 고치고
    /// 새 토큰을 주지 않는다 — 로그인 상태는 그대로다. 한동안 「쿠키를 다시 구워야
    /// 해서 회로 안에서 못 한다」고 적혀 있었는데, 서버를 읽어 보면 그렇지 않다.
    /// </para>
    /// <para>
    /// 실패 이유를 구분해 준다(이전 비밀번호 불일치 · 지금 것과 같음 …).
    /// 90일 만료 때문에 어쩔 수 없이 이 화면에 오는 경우가 있어, 뭉뚱그린 문구를
    /// 주면 무엇을 고쳐야 할지 알 수 없다.
    /// </para>
    /// </summary>
    public Task ChangePasswordAsync(string oldPassword, string newPassword, CancellationToken ct = default)
        => gateway.PostAsync("auth/user/change-password", new { oldPassword, newPassword }, ct);

    public Task<UserInfoDto?> GetMyInfoAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<UserInfoDto>("auth/user/info", ct);

    public Task<IReadOnlyList<UserActivityDto>> GetMyActivityAsync(CancellationToken ct = default)
        => gateway.GetListAsync<UserActivityDto>("auth/user/activity", ct);

    /// <summary>
    /// 내 정보를 고친다.
    ///
    /// 이메일·전화가 남과 겹치면 서버가 <b>409</b> 로 이유를 말해 준다 —
    /// 그 문구를 그대로 보여 줘야 사용자가 무엇을 고칠지 안다.
    /// </summary>
    public Task UpdateMyProfileAsync(UpdateProfileDto profile, CancellationToken ct = default)
        => gateway.PostAsync("auth/user/profile", profile, ct);

    /// <summary>
    /// 확인용 주소를 불러 참·거짓만 꺼낸다. 서버가 봉투에 <c>bool</c> 하나를
    /// 실어 준다(<c>{ result: [true] }</c>).
    ///
    /// 못 부르면 <b>거짓으로 본다.</b> 여기서 막아 봐야 저장할 때 서버가 다시
    /// 검사하므로, 확인이 안 된다고 등록을 막으면 잃는 것만 있다.
    /// </summary>
    private async Task<bool> ExistsAsync(string url, CancellationToken ct)
        => await gateway.GetOneAsync<bool>(url, ct);

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
