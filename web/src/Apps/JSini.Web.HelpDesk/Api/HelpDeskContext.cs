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
    /// 담당자 권한이 있는가. 서버가 포털 역할까지 보고 판정한 값(<c>isAdmin</c>)을
    /// 쓴다 — 계정 연결이 없는 관리자도 조회·관리 화면을 열 수 있어야 한다.
    /// </summary>
    public bool IsAdmin => Identity?.IsAdmin ?? string.Equals(Identity?.LoginType, "admin", StringComparison.OrdinalIgnoreCase);

    /// <summary>헬프데스크 내부 레코드에 이어져 있는가.</summary>
    public bool IsLinked => Identity?.HelpdeskUserId is not null;

    /// <summary>담당자 권한은 있으나 연결이 없는 상태. '내 것' 기능만 못 쓴다.</summary>
    public bool IsUnlinkedAdmin => IsAdmin && !IsLinked;

    /// <summary>
    /// 헬프데스크 업무 화면을 열 수 있는가. 화면을 열지 말지는 이 값으로 판단하고,
    /// <see cref="HelpdeskUserId"/> 로 판단하지 않는다 — 그렇게 하면 연결 없는
    /// 관리자에게 빈 화면이 나온다.
    /// </summary>
    public bool CanUse => IsAdmin || IsLinked;

    /// <summary>
    /// 헬프데스크 내부 사용자 ID. <b>'내 것'을 가리킬 때만</b> 쓴다
    /// (내가 쓴 댓글, 나에게 배정된 요청). 연결이 없으면 null.
    /// </summary>
    public int? HelpdeskUserId => Identity?.HelpdeskUserId;

    /// <summary>고객으로 연결된 경우의 소속 회사 ID.</summary>
    public int? CompanyId =>
        int.TryParse(Identity?.CompanyId, out var id) ? id : null;

    // ── 조직 목록 (셀렉트용) ─────────────────────────────────────

    public IReadOnlyList<BizOption> AdminOptions { get; private set; } = [];
    public IReadOnlyList<BizOption> CompanyOptions { get; private set; } = [];
    public IReadOnlyList<BizOption> CustomerOptions { get; private set; } = [];

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
    }
}
