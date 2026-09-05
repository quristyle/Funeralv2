using JSini.Web.Http;

namespace JSini.Web.LifeEnv.Api;

/// <summary>회사 한 곳 (<c>auth/system/companies</c>).</summary>
public sealed class OrgCompany
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

/// <summary>
/// 부서 한 곳 (<c>auth/system/dept/list</c>).
///
/// 서버가 <b>나무 모양</b>으로 준다(<see cref="Children"/>). 여기서는 드롭다운에
/// 넣을 것이라 펴서 쓴다 — 접힌 채로 두면 하위 부서를 고를 수가 없다.
/// </summary>
public sealed class OrgDepartment
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? CompanyId { get; set; }

    public string? CompanyName { get; set; }

    public List<OrgDepartment>? Children { get; set; }
}

/// <summary>
/// 소속 필터(회사 → 부서)에 쓰는 목록.
///
/// [왜 생활과환경 모듈에 있나]
///
/// 생일 자료가 포털 계정에 딸려 있어서 소속으로 거른다. 목록의 정본은 포털이고
/// 이 앱은 게이트웨이로 읽기만 한다 — <b>포털관리 모듈을 참조하지 않는다.</b>
/// 업무 모듈끼리 참조하는 것은 이 저장소의 금지 사항이고, 아키텍처 테스트가 막는다.
///
/// [수명은 scoped 다]
///
/// 회로(사용자) 하나에 하나. 회사·부서는 자주 바뀌지 않지만 싱글턴으로 두면
/// 조직을 고친 사람만 새 값을 보고 나머지는 옛 값을 계속 보게 된다.
/// </summary>
public sealed class OrgOptions(GatewayClient gateway)
{
    private IReadOnlyList<OrgCompany>? _companies;
    private IReadOnlyList<OrgDepartment>? _departments;

    /// <summary>회사 목록. 한 번만 읽는다.</summary>
    public async Task<IReadOnlyList<OrgCompany>> GetCompaniesAsync(CancellationToken ct = default)
        => _companies ??= await gateway.GetListAsync<OrgCompany>("auth/system/companies", ct);

    /// <summary>
    /// 부서 목록. <b>전 회사를 한 번에 읽어 두고 화면에서 거른다.</b>
    ///
    /// 회사를 바꿀 때마다 다시 읽게 하면 드롭다운을 만질 때마다 왕복이 생기고,
    /// 그 사이 목록이 비어 있어 이미 고른 부서가 풀린다. 부서는 많아야 수십 개다.
    /// </summary>
    public async Task<IReadOnlyList<OrgDepartment>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        if (_departments is not null)
        {
            return _departments;
        }

        var tree = await gateway.GetListAsync<OrgDepartment>(
            "auth/system/dept/list?allCompanies=true", ct);

        var flat = new List<OrgDepartment>();
        Flatten(tree, flat);

        return _departments = flat;
    }

    private static void Flatten(IEnumerable<OrgDepartment> nodes, List<OrgDepartment> into)
    {
        foreach (var node in nodes)
        {
            into.Add(node);

            if (node.Children is { Count: > 0 })
            {
                Flatten(node.Children, into);
            }
        }
    }
}
