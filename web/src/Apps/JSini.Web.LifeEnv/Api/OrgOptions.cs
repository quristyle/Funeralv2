using JSini.Web.Components.Data;
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
/// 부서 한 곳 (<c>auth/system/dept/list</c>). 서버는 나무로 주고(<see cref="Children"/>) 여기서는 펴서 쓴다 — 접힌 채면 하위 부서를 고를 수 없다.
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
/// 소속 필터(회사 → 부서)에 쓰는 목록. 정본은 포털이고 이 앱은 게이트웨이로 읽기만 한다 — 포털관리 모듈은 참조하지 않는다.
/// 회로 밖에서 캐싱한다 — 통은 ReferenceData 에 두어 업무를 넘나들어도 살고, Invalidate 한 번으로 모두가 새 값을 본다.
/// 두 호출은 사용자와 무관해야 공용 통에 담는다: companies 는 신원을 보지 않고, dept/list 는 allCompanies=true 일 때 신원을 쓰지 않는다(아래 호출이 그 값을 싣는다).
/// </summary>
public sealed class OrgOptions(GatewayClient gateway, ReferenceData data)
{
    /// <summary>참조자료 통 안에서의 묶음 이름. 조직을 고치는 화면이 이 이름으로 버린다.</summary>
    public const string Group = "portal.org";

    /// <summary>회사 목록. 한 번만 읽는다.</summary>
    public async Task<IReadOnlyList<OrgCompany>> GetCompaniesAsync(CancellationToken ct = default)
        => await data.SharedAsync<IReadOnlyList<OrgCompany>>(
               Group, "companies",
               async () => await gateway.GetListAsync<OrgCompany>("auth/system/companies", ct))
           ?? [];

    /// <summary>
    /// 부서 목록. 전 회사를 한 번에 읽어 두고 화면에서 거른다 — 회사마다 다시 읽으면 드롭다운을 만질 때마다 왕복이 생기고 고른 부서가 풀린다.
    /// </summary>
    public async Task<IReadOnlyList<OrgDepartment>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        // 펴 둔 것을 담는다. 담긴 객체는 여러 회로가 함께 보므로, 고치는 화면이 생기면 복사해서 넘긴다.
        var flat = await data.SharedAsync<IReadOnlyList<OrgDepartment>>(
            Group, "departments",
            async () =>
            {
                var tree = await gateway.GetListAsync<OrgDepartment>(
                    "auth/system/dept/list?allCompanies=true", ct);

                var into = new List<OrgDepartment>();
                Flatten(tree, into);
                return into;
            });

        return flat ?? [];
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
