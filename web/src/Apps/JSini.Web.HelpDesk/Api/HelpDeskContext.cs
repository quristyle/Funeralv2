namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 헬프데스크 공용 상태 — Vue 의 <c>store/helpdesk.ts</c> 를 잇는 자리.
///
/// - 로그인한 funeralv2 계정이 어떤 헬프데스크 사용자로 해석되는지(신원)
/// - 화면 곳곳의 셀렉트에 쓰이는 조직 목록(회사·고객·담당자)
///
/// 둘 다 여러 화면이 반복해서 필요로 하는데 자주 바뀌지 않아 한 번 받아 캐싱한다.
/// Blazor Server 의 scoped 는 회로(사용자) 하나에 대응하므로 수명이 Pinia 스토어와 같다.
/// </summary>
public sealed class HelpDeskContext(HelpDeskApi api, BizOptionService bizOptions)
{
    private Task? _identityLoading;
    private Task? _orgLoading;

    /// <summary>현재 계정이 해석된 신원. 연결이 없으면 null.</summary>
    public HelpdeskIdentity? Identity { get; private set; }

    /// <summary>신원 조회를 시도했는지. '연결 없음' 과 '아직 안 불러옴' 을 구분한다.</summary>
    public bool IdentityChecked { get; private set; }

    /// <summary>
    /// <b>관리자인가.</b> 포털 역할이 <c>ADMINISTRATOR</c> · <c>SYSTEM_ADMINISTRATOR</c>
    /// 중 하나면 참이고, 그것이 전부다(2026-10-05 규칙).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 판정은 <b>서버가 한다</b>(<c>auth-links/me</c> 의 <c>isAdmin</c>). 역할 목록이
    /// 설정에 있어(<c>HelpdeskIdentityOptions.AdminRoles</c>) 거기서만 읽을 수 있고,
    /// 화면이 역할 이름을 또 적어 두면 설정을 고쳤을 때 <b>서버는 고객으로 보는데
    /// 화면은 관리자로 그리는</b> 쪽으로 어긋난다.
    /// </para>
    /// <para>
    /// <b>연결 종류(<c>loginType</c>)로 떨어지지 않는다.</b> 연결이 <c>admin</c> 이라고
    /// 관리자로 치던 것을 걷었다 — 관리자를 세우고 거두는 자리는 포털 역할표 하나다.
    /// 신원을 못 받았으면 거짓이다(고객으로 본다) — 틀리는 방향이 그쪽이어야 한다.
    /// </para>
    /// </remarks>
    public bool IsAdmin => Identity?.IsAdmin == true;

    /// <summary>시스템관리자인가. 일반 헬프데스크 담당자 권한과는 구분한다.</summary>
    public bool IsSystemAdmin => Identity?.JsiniRoles.Any(role =>
        string.Equals(role, "SYSTEM_ADMINISTRATOR", StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>헬프데스크 내부 레코드에 이어져 있는가.</summary>
    public bool IsLinked => Identity?.HelpdeskUserId is not null;

    /// <summary>
    /// <b>고객인가 — 관리자가 아닌 모든 사람이다.</b> 서버의
    /// <c>HelpdeskPrincipal.IsCustomer</c> 와 같은 판정이다.
    /// </summary>
    /// <remarks>
    /// 연결 종류가 <c>customer</c> 인가로 가르던 것을 2026-10-05 에 뒤집었다. 연결은
    /// 운영에 <b>한 줄뿐</b>이라, 그것으로 가르면 포털 계정 마흔몇이 고객도 관리자도
    /// 아닌 상태가 되어 <b>「고객이니 제 것만」 같은 조건이 통째로 안 걸렸다.</b>
    /// 관리자도 요청을 올리므로 <b>고객이면서 관리자</b>인 셈인데, 둘을 갈라 보여 주는
    /// 자리에서는 관리자로 적는다 — 그래서 이 값은 「관리자가 아닌가」다.
    /// </remarks>
    public bool IsCustomer => !IsAdmin;

    /// <summary>
    /// 고객의 <b>고객 번호</b>. 관리자이거나 가리킬 줄이 없으면 null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="HelpdeskUserId"/> 를 그대로 「내 고객 번호」로 쓰면 안 된다</b> —
    /// 담당자 줄에 이어진 계정은 그 값이 <c>admin.id</c> 다. 실제로 요청 등록 화면이
    /// 그것을 고객 번호로 보내 번호가 겹치는 <b>남의 이름으로 요청이 들어갔다.</b>
    /// </para>
    /// <para>
    /// <b>고객인데 이 값이 null 일 수 있다</b> — 요청을 한 번도 올린 적이 없어
    /// 가리킬 고객 줄이 아직 없는 사람이다(서버가 첫 글을 쓸 때 만든다 ·
    /// <c>RequesterProvisioner</c>). 그때는 「내 것」을 가려낼 수 없으므로
    /// <b>조회를 아예 하지 않는다</b>(<c>RequestManage</c>).
    /// </para>
    /// </remarks>
    public int? CustomerId => IsCustomer ? Identity?.HelpdeskUserId : null;

    /// <summary>담당자 권한은 있으나 연결이 없는 상태. '내 것' 기능만 못 쓴다.</summary>
    public bool IsUnlinkedAdmin => IsAdmin && !IsLinked;

    // [CanUse 를 걷어냈다 (2026-10-05)]
    //
    // `IsAdmin || IsLinked` 였다. 관리자가 아니면 모두 고객이고 고객은 요청을
    // 올릴 수 있으므로 이 값이 거짓인 사람이 없어졌다 — 가리킬 고객 줄은 서버가
    // 첫 글을 쓸 때 만든다(`RequesterProvisioner`).
    //
    // 전에는 이것이 거짓이라 **연결 없는 사람이 요청 등록 화면에서 「권한이
    // 없습니다」로 막혔다.** 운영에 연결이 한 줄뿐이라 사실상 한 사람만 글을
    // 쓸 수 있었다. 화면을 여닫는 것은 메뉴 권한이 한다.

    /// <summary>
    /// 헬프데스크 내부 사용자 ID. <b>'내 것'을 가리킬 때만</b> 쓴다
    /// (내가 쓴 댓글, 나에게 배정된 요청). 연결이 없으면 null.
    /// </summary>
    public int? HelpdeskUserId => Identity?.HelpdeskUserId;

    /// <summary>
    /// 소속 회사 식별자. 헬프데스크가 아니라 <b>포털</b>(<c>scom.companies.id</c>)의
    /// 값이라 숫자가 아니라 글자다(<c>jsini</c> · GUID).
    /// </summary>
    public string? CompanyId =>
        string.IsNullOrWhiteSpace(Identity?.CompanyId) ? null : Identity!.CompanyId;

    // ── 조직 목록 (셀렉트용) ─────────────────────────────────────

    public IReadOnlyList<BizOption> AdminOptions { get; private set; } = [];

    /// <summary>
    /// 회사 고르개에 쓰는 목록. <b>포털이 준다</b> — 헬프데스크는 회사를 스스로
    /// 관리하지 않는다. 사용처가 헬프데스크(<c>HELP_DESK</c>)로 배정된 회사만 온다.
    /// </summary>
    public IReadOnlyList<BizOption> CompanyOptions { get; private set; } = [];
    public IReadOnlyList<BizOption> CustomerOptions { get; private set; } = [];
    public IReadOnlyList<System.Text.Json.JsonElement> CustomerItems { get; private set; } = [];

    /// <summary>회사 아이디를 이름으로 바꾼다. 모르는 아이디면 아이디를 그대로 준다.</summary>
    public string CompanyName(string? companyId) =>
        string.IsNullOrWhiteSpace(companyId)
            ? "-"
            : CompanyOptions.FirstOrDefault(o => o.Value == companyId)?.Label ?? companyId;

    /// <summary>
    /// 현재 계정이 연결된 헬프데스크 사용자를 조회한다. 연결이 없으면 Identity 는
    /// null 로 남고 화면에서 안내 문구를 띄운다. 동시에 여러 화면 조각이 불러도
    /// 조회는 한 번만 나간다.
    /// </summary>
    public Task LoadIdentityAsync(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            _identityLoading = null;
            IdentityChecked = false;
        }

        return _identityLoading ??= LoadIdentityCoreAsync();
    }

    private async Task LoadIdentityCoreAsync()
    {
        try
        {
            Identity = await api.GetAsync<HelpdeskIdentity>("auth-links/me");
        }
        catch
        {
            // 연결된 헬프데스크 계정이 없는 경우. 화면에서 안내하므로 조용히 넘어간다.
            Identity = null;
        }
        finally
        {
            IdentityChecked = true;
        }
    }

    /// <summary>
    /// 조회 조건 셀렉트에 쓰는 조직 목록을 한 번에 받아 캐싱한다.
    /// 어느 API 를 부르는지는 여기 없다 — DB 메타데이터(scom.biz_select_configs 의
    /// helpdesk_admin · helpdesk_company · helpdesk_customer)가 정한다.
    ///
    /// <c>helpdesk_company</c> 는 <b>포털</b>(<c>auth</c> 의
    /// <c>/system/companies?usageLocation=HELP_DESK</c>)을 가리킨다. 헬프데스크의
    /// <c>/companys</c> 를 가리키던 것을 옮겼다 — 회사를 관리하는 곳이 포털 하나이기
    /// 때문이다. 화면 코드는 그대로 두고 메타데이터만 바꾼 것이라 여기 손댈 것이 없다.
    /// </summary>
    public Task LoadOrganizationsAsync(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            _orgLoading = null;
        }

        return _orgLoading ??= LoadOrganizationsCoreAsync();
    }

    private async Task LoadOrganizationsCoreAsync()
    {
        var admins = bizOptions.FetchOptionsAsync("helpdesk_admin");
        var companies = bizOptions.FetchOptionsAsync("helpdesk_company");
        var customers = bizOptions.FetchOptionsAsync("helpdesk_customer");
        await Task.WhenAll(admins, companies, customers);

        AdminOptions = admins.Result.Options;
        CompanyOptions = companies.Result.Options;
        CustomerOptions = customers.Result.Options;
        CustomerItems = customers.Result.Items;
    }
}
