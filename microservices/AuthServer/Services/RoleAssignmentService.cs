using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>역할을 걸 수 있는 대상의 종류.</summary>
public enum RoleScopeKind
{
    /// <summary>회사 — 그 회사 사람 전부에게 적용된다 (가장 넓다)</summary>
    Company,

    /// <summary>부서 — 그 부서 사람에게 적용된다</summary>
    Department,

    /// <summary>사람 — 그 계정에만 적용된다 (가장 좁다)</summary>
    Account,
}

/// <summary>
/// 회사·부서·사람에 역할을 걸고 푸는 서비스.
/// </summary>
public interface IRoleAssignmentService
{
    /// <summary>회사 하나의 조직 트리와 각 단계에 걸린 역할을 함께 돌려준다(화면용).</summary>
    Task<RoleScopeTreeDto> GetScopeTreeAsync(string companyId);

    /// <summary>대상에 역할을 건다. 이미 걸려 있으면 그대로 둔다.</summary>
    Task AssignAsync(RoleScopeKind kind, string targetId, string roleId);

    /// <summary>대상에서 역할을 푼다. 걸려 있지 않아도 오류가 아니다.</summary>
    Task RemoveAsync(RoleScopeKind kind, string targetId, string roleId);

    /// <summary>
    /// 그 계정에 실제로 적용되는 역할. 로그인 토큰과 화면이 이 값을 쓴다.
    /// </summary>
    Task<EffectiveRolesDto> ResolveEffectiveRolesAsync(string accountId);

    /// <summary>
    /// 검색용 사람 목록. 회사·부서 이름까지 함께 담아 한 번에 훑을 수 있게 한다.
    /// </summary>
    Task<List<AccountPickDto>> GetAccountPickListAsync();

    /// <summary>그 계정이 볼 수 있는 메뉴와 볼 수 없는 메뉴.</summary>
    Task<AccountMenuAccessDto> GetMenuAccessAsync(string accountId);
}

/// <inheritdoc />
public class RoleAssignmentService : IRoleAssignmentService
{
    private readonly AppDbContext _db;

    /// <summary>서비스를 생성한다.</summary>
    public RoleAssignmentService(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task AssignAsync(RoleScopeKind kind, string targetId, string roleId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(roleId))
        {
            throw new InvalidOperationException("대상과 역할을 모두 지정해야 합니다.");
        }

        var roleExists = await _db.Roles.AnyAsync(r => r.Id == roleId);
        if (!roleExists) throw new KeyNotFoundException($"역할 '{roleId}' 을 찾을 수 없습니다.");

        switch (kind)
        {
            case RoleScopeKind.Company:
                if (!await _db.RoleCompanies.AnyAsync(x => x.CompanyId == targetId && x.RoleId == roleId))
                {
                    _db.RoleCompanies.Add(new RoleCompany { CompanyId = targetId, RoleId = roleId });
                }
                break;

            case RoleScopeKind.Department:
                if (!await _db.RoleDepartments.AnyAsync(x => x.DepartmentId == targetId && x.RoleId == roleId))
                {
                    _db.RoleDepartments.Add(new RoleDepartment { DepartmentId = targetId, RoleId = roleId });
                }
                break;

            default:
                if (!await _db.RoleAccounts.AnyAsync(x => x.AccountId == targetId && x.RoleId == roleId))
                {
                    _db.RoleAccounts.Add(new RoleAccount { AccountId = targetId, RoleId = roleId });
                }
                break;
        }

        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(RoleScopeKind kind, string targetId, string roleId)
    {
        switch (kind)
        {
            case RoleScopeKind.Company:
                var rc = await _db.RoleCompanies.FirstOrDefaultAsync(x => x.CompanyId == targetId && x.RoleId == roleId);
                if (rc is not null) _db.RoleCompanies.Remove(rc);
                break;

            case RoleScopeKind.Department:
                var rd = await _db.RoleDepartments.FirstOrDefaultAsync(x => x.DepartmentId == targetId && x.RoleId == roleId);
                if (rd is not null) _db.RoleDepartments.Remove(rd);
                break;

            default:
                var ra = await _db.RoleAccounts.FirstOrDefaultAsync(x => x.AccountId == targetId && x.RoleId == roleId);
                if (ra is not null) _db.RoleAccounts.Remove(ra);
                break;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// 그 계정에 실제로 적용되는 역할.
    ///
    /// <para>
    /// <b>세 단계를 모두 합친다 — 회사 + 부서 + 사람.</b>
    /// 회사에 걸린 역할, 그 사람이 속한 부서에 걸린 역할, 사람에게 직접 걸린 역할을
    /// 전부 갖는다. 어느 한 단계가 다른 단계를 덮어쓰지 않는다.
    /// </para>
    ///
    /// <para>
    /// 부서는 <b>상위 부서까지 거슬러 올라가며 모두 더한다.</b> 트리 위쪽에 걸어 두면
    /// 아래 부서가 물려받는다.
    /// </para>
    ///
    /// <para>
    /// 같은 역할이 여러 단계에 걸려 있어도 결과에는 한 번만 담는다.
    /// 다만 <see cref="EffectiveRolesDto.Sources"/> 에는 <b>어느 단계에서 왔는지</b>를
    /// 모두 적어 둔다 — 화면이 "이 역할은 부서에서 온 것" 이라고 알려 주려면 필요하다.
    /// 그래야 "여기서 뺐는데 왜 아직 있지" 를 곧바로 이해할 수 있다.
    /// </para>
    ///
    /// <para>비활성 역할(<c>status != 1</c>)은 어느 단계에서든 제외한다.</para>
    /// </summary>
    public async Task<EffectiveRolesDto> ResolveEffectiveRolesAsync(string accountId)
    {
        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId);

        if (account is null)
        {
            return new EffectiveRolesDto();
        }

        // 역할 식별자 → 그 역할이 온 단계들
        var sources = new Dictionary<string, List<string>>();

        void Note(string roleId, string source)
        {
            if (!sources.TryGetValue(roleId, out var list))
            {
                list = new List<string>();
                sources[roleId] = list;
            }
            if (!list.Contains(source)) list.Add(source);
        }

        // 1) 회사
        if (!string.IsNullOrEmpty(account.CompanyId))
        {
            foreach (var r in await ActiveRolesAsync(
                _db.RoleCompanies.Where(x => x.CompanyId == account.CompanyId).Select(x => x.RoleId)))
            {
                Note(r.Id, "company");
            }
        }

        // 2) 부서 — 자기 부서부터 상위로 거슬러 올라가며 전부 더한다
        if (!string.IsNullOrEmpty(account.DepartmentId))
        {
            var byId = await _db.Departments.AsNoTracking()
                .Where(d => d.CompanyId == account.CompanyId)
                .ToDictionaryAsync(d => d.Id, d => d.ParentId);

            var seen = new HashSet<string>();
            var cursor = account.DepartmentId;

            while (!string.IsNullOrEmpty(cursor) && seen.Add(cursor))
            {
                var current = cursor;
                foreach (var r in await ActiveRolesAsync(
                    _db.RoleDepartments.Where(x => x.DepartmentId == current).Select(x => x.RoleId)))
                {
                    Note(r.Id, "department");
                }

                cursor = byId.TryGetValue(cursor, out var parent) ? parent : null;
            }
        }

        // 3) 사람에게 직접
        foreach (var r in await ActiveRolesAsync(
            _db.RoleAccounts.Where(x => x.AccountId == accountId).Select(x => x.RoleId)))
        {
            Note(r.Id, "account");
        }

        if (sources.Count == 0) return new EffectiveRolesDto();

        var names = await _db.Roles.AsNoTracking()
            .Where(r => sources.Keys.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => string.IsNullOrWhiteSpace(r.Name) ? r.Id : r.Name);

        var ids = sources.Keys.ToList();
        return new EffectiveRolesDto
        {
            RoleIds = ids,
            RoleNames = ids.Select(id => names.GetValueOrDefault(id, id)).ToList(),
            Sources = sources,
        };
    }

    /// <summary>활성 역할만 골라 (식별자, 이름) 으로 돌려준다.</summary>
    private async Task<List<(string Id, string Name)>> ActiveRolesAsync(IQueryable<string> roleIds)
    {
        return (await _db.Roles.AsNoTracking()
            .Where(r => r.Status == 1 && roleIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Name })
            .ToListAsync())
            .Select(r => (r.Id, string.IsNullOrWhiteSpace(r.Name) ? r.Id : r.Name))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<RoleScopeTreeDto> GetScopeTreeAsync(string companyId)
    {
        var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new KeyNotFoundException($"회사 '{companyId}' 을 찾을 수 없습니다.");

        var departments = await _db.Departments.AsNoTracking()
            .Where(d => d.CompanyId == companyId)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
            .ToListAsync();

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.CompanyId == companyId)
            .OrderBy(a => a.UserName)
            .ToListAsync();

        // 걸린 역할을 한 번에 읽어 표로 만든다. 대상마다 따로 조회하면 부서·사람 수만큼 왕복한다.
        var deptIds = departments.Select(d => d.Id).ToList();
        var accountIds = accounts.Select(a => a.Id).ToList();

        var companyRoles = await _db.RoleCompanies.AsNoTracking()
            .Where(x => x.CompanyId == companyId).Select(x => x.RoleId).ToListAsync();

        var deptRoles = (await _db.RoleDepartments.AsNoTracking()
            .Where(x => deptIds.Contains(x.DepartmentId)).ToListAsync())
            .GroupBy(x => x.DepartmentId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleId).ToList());

        var accountRoles = (await _db.RoleAccounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId)).ToListAsync())
            .GroupBy(x => x.AccountId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleId).ToList());

        List<RoleScopeNodeDto> BuildDepts(string? parentId) => departments
            .Where(d => d.ParentId == parentId)
            .Select(d => new RoleScopeNodeDto
            {
                Id = d.Id,
                Name = d.Name,
                Kind = "department",
                RoleIds = deptRoles.GetValueOrDefault(d.Id) ?? new(),
                Children = BuildDepts(d.Id),
                Accounts = accounts
                    .Where(a => a.DepartmentId == d.Id)
                    .Select(a => new RoleScopeNodeDto
                    {
                        Id = a.Id,
                        Name = a.RealName ?? a.UserName ?? a.UserId,
                        LoginId = a.UserId,
                        Kind = "account",
                        RoleIds = accountRoles.GetValueOrDefault(a.Id) ?? new(),
                    })
                    .ToList(),
            })
            .ToList();

        return new RoleScopeTreeDto
        {
            Company = new RoleScopeNodeDto
            {
                Id = company.Id,
                Name = company.Name,
                Kind = "company",
                RoleIds = companyRoles,
                Children = BuildDepts(null),
                // 부서가 없는 사람은 회사 바로 아래에 둔다. 화면에서 사라지면 역할을 줄 수 없다.
                Accounts = accounts
                    .Where(a => string.IsNullOrEmpty(a.DepartmentId))
                    .Select(a => new RoleScopeNodeDto
                    {
                        Id = a.Id,
                        Name = a.RealName ?? a.UserName ?? a.UserId,
                        LoginId = a.UserId,
                        Kind = "account",
                        RoleIds = accountRoles.GetValueOrDefault(a.Id) ?? new(),
                    })
                    .ToList(),
            },
        };
    }
    /// <inheritdoc />
    public async Task<List<AccountPickDto>> GetAccountPickListAsync()
    {
        // 회사·부서 이름을 계정마다 다시 조회하지 않도록 한 번에 읽어 표로 만든다.
        var companies = await _db.Companies.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);
        var departments = await _db.Departments.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name);

        var accounts = await _db.Accounts.AsNoTracking()
            .OrderBy(a => a.UserName)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                a.UserName,
                a.RealName,
                a.CompanyId,
                a.DepartmentId,
            })
            .ToListAsync();

        return accounts.Select(a => new AccountPickDto
        {
            Id = a.Id,
            LoginId = a.UserId,
            Name = a.RealName ?? a.UserName ?? a.UserId,
            CompanyId = a.CompanyId,
            CompanyName = a.CompanyId is null ? null : companies.GetValueOrDefault(a.CompanyId),
            DepartmentId = a.DepartmentId,
            DepartmentName = a.DepartmentId is null ? null : departments.GetValueOrDefault(a.DepartmentId),
        }).ToList();
    }

    /// <summary>
    /// 그 계정이 볼 수 있는 메뉴와 볼 수 없는 메뉴.
    ///
    /// <para>
    /// 기준은 <c>scom.role_menus</c> 의 <c>can_view</c> 다. 그 사람에게 적용되는 역할
    /// (회사+부서+사람을 합친 것) 중 하나라도 열어 주면 열린 것으로 본다.
    /// </para>
    ///
    /// <para>
    /// 비활성 메뉴(<c>status != 1</c>)는 아예 빼놓는다. 라우트가 만들어지지 않아
    /// 권한이 있든 없든 열 수 없기 때문이다.
    /// </para>
    /// </summary>
    public async Task<AccountMenuAccessDto> GetMenuAccessAsync(string accountId)
    {
        var effective = await ResolveEffectiveRolesAsync(accountId);
        var roleIds = effective.RoleIds;

        var menus = await _db.SystemMenus.AsNoTracking()
            .Where(m => m.Status == 1)
            .OrderBy(m => m.OrderNo)
            .ToListAsync();

        // 메뉴 → 열어 준 역할들
        var grants = roleIds.Count == 0
            ? new Dictionary<string, List<string>>()
            : (await _db.RoleMenus.AsNoTracking()
                    .Where(rm => roleIds.Contains(rm.RoleId) && rm.CanView)
                    .Select(rm => new { rm.MenuId, rm.RoleId })
                    .ToListAsync())
                .GroupBy(x => x.MenuId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RoleId).Distinct().ToList());

        // 어디에 있는 메뉴인지 알아볼 수 있게 상위 제목을 이어 붙인다.
        var byId = menus.ToDictionary(m => m.Id);
        string Breadcrumb(Entities.SystemMenu menu)
        {
            var parts = new List<string>();
            var seen = new HashSet<string>();
            var cursor = menu.Pid;
            while (!string.IsNullOrEmpty(cursor) && seen.Add(cursor) && byId.TryGetValue(cursor, out var p))
            {
                parts.Insert(0, p.Title ?? p.Name);
                cursor = p.Pid;
            }
            return string.Join(" › ", parts);
        }

        var result = new AccountMenuAccessDto();
        foreach (var m in menus)
        {
            var item = new AccountMenuItemDto
            {
                Id = m.Id,
                Path = m.Path,
                Title = m.Title ?? m.Name,
                Type = m.Type,
                Breadcrumb = Breadcrumb(m),
                GrantedBy = grants.GetValueOrDefault(m.Id) ?? new List<string>(),
            };

            if (item.GrantedBy.Count > 0) result.Assigned.Add(item);
            else result.Unassigned.Add(item);
        }

        return result;
    }

}
