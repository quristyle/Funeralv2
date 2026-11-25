using JSini.Web.Http;

namespace JSini.Web.HelpDesk.Api;

/// <summary>헬프데스크 공용 상태 — 로그인 계정의 헬프데스크 신원과 셀렉트용 조직 목록을 한 번 받아 캐싱한다. 회로(사용자)마다 하나라 수명이 회로와 같다.</summary>
public sealed class HelpDeskContext(HelpDeskApi api, BizOptionService bizOptions, GatewayClient gateway)
{
    private Task? _identityLoading;
    private Task? _orgLoading;
    private Task<IReadOnlyList<string>>? _managedLoading;

    /// <summary>포털관리 「사용처 담당자」에서 이 사용처의 처리 담당자로 걸린 사람만 그 회사를 받는다.</summary>
    private const string HelpDeskUsage = "HELP_DESK";

    /// <summary>로그인한 사람이 헬프데스크 처리 담당자로 걸린 회사들(scom.companies.id). 못 읽으면 빈 목록 — 미리 고르기만 못 할 뿐 화면은 그대로 쓴다.</summary>
    public Task<IReadOnlyList<string>> LoadManagedCompanyIdsAsync() =>
        _managedLoading ??= LoadManagedCompanyIdsCoreAsync();

    private async Task<IReadOnlyList<string>> LoadManagedCompanyIdsCoreAsync()
    {
        try
        {
            return await gateway.GetListAsync<string>(
                $"auth/system/company-usage-managers/mine?codeValue={HelpDeskUsage}");
        }
        catch
        {
            return [];
        }
    }

    private Task<IReadOnlyList<BizOption>>? _allCompaniesLoading;

    /// <summary>포털의 회사 전부(이름 순). 사용처로 좁히지 않는다 — 요청 등록의 시스템관리자 고르개와 회사 이름 풀기에 쓴다. 못 읽으면 빈 목록.</summary>
    public Task<IReadOnlyList<BizOption>> LoadAllCompaniesAsync() =>
        _allCompaniesLoading ??= LoadAllCompaniesCoreAsync();

    private async Task<IReadOnlyList<BizOption>> LoadAllCompaniesCoreAsync()
    {
        try
        {
            var rows = await gateway.GetListAsync<PortalCompany>("auth/system/companies");
            return [.. rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Id))
                .Select(r => new BizOption(string.IsNullOrWhiteSpace(r.Name) ? r.Id! : r.Name!, r.Id))
                .OrderBy(o => o.Label, StringComparer.CurrentCulture)];
        }
        catch
        {
            return [];
        }
    }

    private sealed class PortalCompany
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    /// <summary>
    /// 요청 등록에서 고를 수 있는 회사. 서버의 RequestCompanyPolicy 와 같은 규칙이다 —
    /// 시스템관리자는 모든 회사, 헬프데스크 관리자는 「사용처 담당자」(HELP_DESK)로 맡은 회사, 그 밖에는 자기 회사 하나.
    /// 맡은 회사가 없는 관리자는 자기 회사로 떨어진다.
    /// </summary>
    public async Task<IReadOnlyList<BizOption>> LoadRequestCompanyOptionsAsync()
    {
        await LoadIdentityAsync();
        var all = await LoadAllCompaniesAsync();

        BizOption Option(string id) =>
            new(all.FirstOrDefault(o => o.Value == id)?.Label ?? id, id);

        if (IsSystemAdmin && all.Count > 0)
        {
            return all;
        }

        if (IsHelpdeskManager)
        {
            var managed = await LoadManagedCompanyIdsAsync();
            if (managed.Count > 0)
            {
                return [.. managed.Distinct().Select(Option).OrderBy(o => o.Label, StringComparer.CurrentCulture)];
            }
        }

        return CompanyId is { } mine ? [Option(mine)] : [];
    }

    /// <summary>
    /// 헬프데스크 관리자가 처리할 수 있는 회사들 — 「사용처 담당자」(HELP_DESK)로 맡은 회사다. 서버의 RequestCompanyPolicy.CanHandleAsync 와 같은 규칙이다.
    /// 관리자(IsAdmin)는 이미 모든 글을 다루므로 여기에 담지 않는다. 관리자 역할이 아니면 빈 목록.
    /// </summary>
    public async Task<IReadOnlyList<string>> LoadHandledCompanyIdsAsync()
    {
        await LoadIdentityAsync();
        return IsHelpdeskManager ? await LoadManagedCompanyIdsAsync() : [];
    }

    /// <summary>이 회사의 요청을 접수·완료할 수 있는가 — 관리자이거나, 그 회사를 맡은 헬프데스크 관리자다.</summary>
    public async Task<bool> CanHandleCompanyAsync(string? companyId)
    {
        await LoadIdentityAsync();
        if (IsAdmin) return true;
        if (string.IsNullOrWhiteSpace(companyId)) return false;

        var handled = await LoadHandledCompanyIdsAsync();
        return handled.Contains(companyId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 요청 처리 화면에서 헬프데스크 관리자가 고를 회사 — 자기 회사와 맡은 회사(이름 순). 맡은 회사가 없으면 자기 회사 하나뿐이다.
    /// 시스템관리자는 이 목록을 쓰지 않는다(CompanyOptions 전체).
    /// </summary>
    public async Task<IReadOnlyList<BizOption>> LoadManageCompanyOptionsAsync()
    {
        var handled = await LoadHandledCompanyIdsAsync();
        var ids = handled.ToList();
        if (CompanyId is { } mine) ids.Insert(0, mine);

        if (ids.Count == 0) return [];

        var all = await LoadAllCompaniesAsync();
        string Label(string id) =>
            all.FirstOrDefault(o => o.Value == id)?.Label
            ?? CompanyOptions.FirstOrDefault(o => o.Value == id)?.Label
            ?? id;

        return [.. ids.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => new BizOption(Label(id), id))
            .OrderBy(o => o.Label, StringComparer.CurrentCulture)];
    }

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

    /// <summary>헬프데스크 관리자(포털 역할 HELPDESK_MANAGER)인가. 「사용처 담당자」로 맡은 회사의 이름으로 요청을 올릴 수 있다.</summary>
    public bool IsHelpdeskManager => Identity?.JsiniRoles.Any(role =>
        string.Equals(role, "HELPDESK_MANAGER", StringComparison.OrdinalIgnoreCase)) == true;

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
