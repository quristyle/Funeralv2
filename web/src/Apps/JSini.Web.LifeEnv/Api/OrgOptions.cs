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
/// [회로 바깥에서 캐싱한다]
///
/// 한동안 이 클래스가 두 목록을 필드로 들고 있었다. 이 서비스가 scoped 라
/// <b>업무를 넘나들면 통째로 사라졌고</b>(Piral 모듈 컨테이너가 갈린다)
/// 접속자 수만큼 같은 표를 읽었다. 지금은 <see cref="ReferenceData"/> 에 맡긴다.
///
/// 「싱글턴으로 두면 조직을 고친 사람만 새 값을 본다」는 걱정이 scoped 를 고른
/// 이유로 적혀 있었는데, <b>통이 하나면 그 반대다</b> —
/// <see cref="ReferenceData.Invalidate"/> 한 번으로 모두가 새 값을 보고,
/// 아무도 안 부르더라도 TTL 이 지나면 반영된다.
///
/// [둘 다 사용자와 무관하다 — 근거를 적어 둔다]
///
/// <list type="bullet">
///   <item><c>GET /auth/system/companies</c> 는 <c>GetAllCompaniesAsync(usageLocation)</c>
///         를 부르고 그 서비스는 신원을 보지 않는다.</item>
///   <item><c>GET /auth/system/dept/list</c> 는 <c>UserContext</c> 를 <b>받는다</b>.
///         다만 그것을 쓰는 분기가 <c>if (!allCompanies)</c> 안에 있어서,
///         <b><c>allCompanies=true</c> 로 부르는 동안에는</b> 전 회사를 그대로 준다.
///         아래 호출이 그 값을 싣는 이유가 이것이기도 하다 —
///         <b>빼면 응답이 사람마다 갈리므로 공용 통에 담을 수 없게 된다.</b></item>
/// </list>
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
    /// 부서 목록. <b>전 회사를 한 번에 읽어 두고 화면에서 거른다.</b>
    ///
    /// 회사를 바꿀 때마다 다시 읽게 하면 드롭다운을 만질 때마다 왕복이 생기고,
    /// 그 사이 목록이 비어 있어 이미 고른 부서가 풀린다. 부서는 많아야 수십 개다.
    /// </summary>
    public async Task<IReadOnlyList<OrgDepartment>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        // **펴 둔 것을 담는다.** 나무를 담고 화면마다 펴면 편 결과가 회로마다
        // 새로 생겨 아끼는 것이 왕복뿐이 된다.
        //
        // 담긴 객체를 여러 회로가 함께 본다. `OrgDepartment` 의 속성이 `set` 이라
        // 고칠 수는 있지만 **고치는 화면이 없다** — 이 목록은 고르개를 채우는
        // 데만 쓴다. 고치는 화면이 생기면 그때 복사해서 넘겨야 한다.
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
