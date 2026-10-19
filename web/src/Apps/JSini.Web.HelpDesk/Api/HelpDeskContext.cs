namespace JSini.Web.HelpDesk.Api;

/// <summary>헬프데스크 공용 상태 — 로그인 계정의 헬프데스크 신원과 셀렉트용 조직 목록을 한 번 받아 캐싱한다. 회로(사용자)마다 하나라 수명이 회로와 같다.</summary>
public sealed class HelpDeskContext(HelpDeskApi api, BizOptionService bizOptions)
{
    private Task? _identityLoading;
    private Task? _orgLoading;

    /// <summary>현재 계정이 해석된 신원. 연결이 없으면 null.</summary>
    public HelpdeskIdentity? Identity { get; private set; }

    /// <summary>신원 조회를 시도했는지. '연결 없음' 과 '아직 안 불러옴' 을 구분한다.</summary>
    public bool IdentityChecked { get; private set; }

    /// <summary>관리자인가 — 포털 역할이 ADMINISTRATOR · SYSTEM_ADMINISTRATOR 중 하나면 참이고 그것이 전부다. 판정은 서버(auth-links/me 의 isAdmin)가 한다 — 화면이 역할 이름을 또 적으면 설정을 고쳤을 때 서버와 화면이 어긋난다.
    /// 연결 종류(loginType)로 떨어지지 않는다. 신원을 못 받았으면 거짓(고객으로 본다) — 틀리는 방향이 그쪽이어야 한다.</summary>
    public bool IsAdmin => Identity?.IsAdmin == true;

    /// <summary>시스템관리자인가. 일반 헬프데스크 담당자 권한과는 구분한다.</summary>
    public bool IsSystemAdmin => Identity?.JsiniRoles.Any(role =>
        string.Equals(role, "SYSTEM_ADMINISTRATOR", StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>헬프데스크 내부 레코드에 이어져 있는가.</summary>
    public bool IsLinked => Identity?.HelpdeskUserId is not null;

    /// <summary>고객인가 — 관리자가 아닌 모든 사람. 서버의 HelpdeskPrincipal.IsCustomer 와 같은 판정이다.
    /// 연결이 한 줄뿐이라 연결 종류로 가르면 대부분이 고객도 관리자도 아니게 되어 「제 것만」 조건이 안 걸린다.</summary>
    public bool IsCustomer => !IsAdmin;

    /// <summary>고객의 고객 번호. 관리자이거나 가리킬 줄이 없으면 null. HelpdeskUserId 를 그대로 쓰면 안 된다 — 담당자 줄에 이어진 계정은 그 값이 admin.id 라 번호가 겹치는 남의 이름으로 요청이 들어간다.
    /// 고객인데 null 일 수 있다 — 요청을 한 번도 안 올린 사람은 고객 줄이 아직 없다(첫 글에서 만든다). 그때는 조회를 아예 하지 않는다.</summary>
    public int? CustomerId => IsCustomer ? Identity?.HelpdeskUserId : null;

    /// <summary>담당자 권한은 있으나 연결이 없는 상태. '내 것' 기능만 못 쓴다.</summary>
    public bool IsUnlinkedAdmin => IsAdmin && !IsLinked;

    // 요청 등록을 막는 조건은 두지 않는다 — 관리자가 아니면 모두 고객이라 요청을 올릴 수 있다(고객 줄은 첫 글에서 만든다).
    // 화면을 여닫는 것은 메뉴 권한이 한다.

    /// <summary>헬프데스크 내부 사용자 ID. 내 것을 가리킬 때만 쓴다(내 댓글, 나에게 배정된 요청). 연결이 없으면 null.</summary>
    public int? HelpdeskUserId => Identity?.HelpdeskUserId;

    /// <summary>소속 회사 식별자. 헬프데스크가 아니라 포털(scom.companies.id)의 값이라 숫자가 아니라 글자다.</summary>
    public string? CompanyId =>
        string.IsNullOrWhiteSpace(Identity?.CompanyId) ? null : Identity!.CompanyId;

    // ── 조직 목록 (셀렉트용) ─────────────────────────────────────

    public IReadOnlyList<BizOption> AdminOptions { get; private set; } = [];

    /// <summary>회사 고르개 목록. 포털이 준다 — 헬프데스크는 회사를 스스로 관리하지 않는다. 사용처가 HELP_DESK 로 배정된 회사만 온다.</summary>
    public IReadOnlyList<BizOption> CompanyOptions { get; private set; } = [];
    public IReadOnlyList<BizOption> CustomerOptions { get; private set; } = [];
    public IReadOnlyList<System.Text.Json.JsonElement> CustomerItems { get; private set; } = [];

    /// <summary>회사 아이디를 이름으로 바꾼다. 모르는 아이디면 아이디를 그대로 준다.</summary>
    public string CompanyName(string? companyId) =>
        string.IsNullOrWhiteSpace(companyId)
            ? "-"
            : CompanyOptions.FirstOrDefault(o => o.Value == companyId)?.Label ?? companyId;

    /// <summary>현재 계정이 연결된 헬프데스크 사용자를 조회한다. 연결이 없으면 Identity 는 null 로 남고, 여러 조각이 불러도 조회는 한 번만 나간다.</summary>
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

    /// <summary>조회 조건 셀렉트의 조직 목록을 한 번에 받아 캐싱한다. 어느 API 를 부르는지는 DB 메타데이터(scom.biz_select_configs 의 helpdesk_admin · helpdesk_company · helpdesk_customer)가 정한다.
    /// helpdesk_company 는 포털(auth 의 /system/companies?usageLocation=HELP_DESK)을 가리킨다 — 회사를 관리하는 곳이 포털 하나이기 때문이다.</summary>
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
