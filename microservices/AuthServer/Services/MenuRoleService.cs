using AuthServer.Data;
using AuthServer.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 메뉴를 기준으로 권한 현황을 거꾸로 읽는다.
/// </summary>
/// <remarks>
/// 읽기 전용이다. 쓰기는 이미 있는 경로를 쓴다 — <see cref="IMenuRoleService"/> 주석 참고.
/// </remarks>
public class MenuRoleService : IMenuRoleService
{
    private readonly AppDbContext _db;

    public MenuRoleService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<MenuRoleDto?> GetByMenuIdAsync(string menuId)
    {
        var menu = await _db.SystemMenus
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == menuId && !m.IsDeleted);

        if (menu is null) return null;

        // ── 1. 이 메뉴에 걸린 역할 권한 ────────────────────────
        var grants = await _db.RoleMenus
            .AsNoTracking()
            .Where(rm => rm.MenuId == menuId && !rm.IsDeleted)
            .ToListAsync();
        var grantByRole = grants.ToDictionary(g => g.RoleId);

        // 걸리지 않은 역할도 담아야 화면에서 새로 켜 줄 수 있다.
        var roles = await _db.Roles
            .AsNoTracking()
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name })
            .ToListAsync();

        // ── 2. 역할별로 걸린 회사·부서·사람 ────────────────────
        var roleIds = roles.Select(r => r.Id).ToList();

        var roleCompanies = await _db.RoleCompanies
            .AsNoTracking()
            .Where(rc => roleIds.Contains(rc.RoleId) && !rc.IsDeleted)
            .Select(rc => new { rc.RoleId, rc.CompanyId })
            .ToListAsync();

        var roleDepartments = await _db.RoleDepartments
            .AsNoTracking()
            .Where(rd => roleIds.Contains(rd.RoleId) && !rd.IsDeleted)
            .Select(rd => new { rd.RoleId, rd.DepartmentId })
            .ToListAsync();

        var roleAccounts = await _db.RoleAccounts
            .AsNoTracking()
            .Where(ra => roleIds.Contains(ra.RoleId) && !ra.IsDeleted)
            .Select(ra => new { ra.RoleId, ra.AccountId })
            .ToListAsync();

        // ── 3. 이 메뉴를 '열람' 할 수 있게 하는 역할만 골라낸다 ──
        //
        // 권한 한 줄이 걸려 있어도 메뉴가 그 항목을 쓰지 않으면 효과가 없다.
        // MenuService 가 메뉴의 use_* 와 AND 로 묶기 때문이다. 여기서도 같은 규칙을 쓴다 —
        // 화면이 "닿는다" 고 말한 대상이 실제로는 못 들어오는 일이 없어야 한다.
        var viewingRoleIds = grants
            .Where(g => g.CanView && menu.UseView)
            .Select(g => g.RoleId)
            .ToHashSet();

        var roleNameById = roles.ToDictionary(r => r.Id, r => r.Name);

        // ── 4. 대상 목록 ──────────────────────────────────────
        var companyTargets = await BuildCompanyTargetsAsync(roleCompanies
            .Where(rc => viewingRoleIds.Contains(rc.RoleId))
            .Select(rc => (rc.CompanyId, rc.RoleId)), roleNameById);

        var deptTargets = await BuildDepartmentTargetsAsync(roleDepartments
            .Where(rd => viewingRoleIds.Contains(rd.RoleId))
            .Select(rd => (rd.DepartmentId, rd.RoleId)), roleNameById);

        var accountTargets = await BuildAccountTargetsAsync(roleAccounts
            .Where(ra => viewingRoleIds.Contains(ra.RoleId))
            .Select(ra => (ra.AccountId, ra.RoleId)), roleNameById);

        // ── 5. 실제 열람 가능 사용자 수 ────────────────────────
        //
        // 회사·부서·사람 세 갈래를 합친 뒤 사람 단위로 중복을 없앤다.
        // 목록 건수를 그냥 더하면 회사와 부서 양쪽에 걸린 사람을 두 번 세게 된다.
        var effective = await CountEffectiveUsersAsync(
            companyTargets.Select(t => t.Id).ToList(),
            deptTargets.Select(t => t.Id).ToList(),
            accountTargets.Select(t => t.Id).ToList());

        return new MenuRoleDto
        {
            MenuId = menu.Id,
            MenuName = string.IsNullOrWhiteSpace(menu.Title) ? menu.Name : menu.Title!,
            MenuPath = menu.Path,
            Used = new MenuUsedPermissionDto
            {
                View = menu.UseView,
                Search = menu.UseSearch,
                Create = menu.UseCreate,
                Update = menu.UseUpdate,
                Delete = menu.UseDelete,
                Print = menu.UsePrint,
                Excel = menu.UseExcel,
                Cust1 = menu.UseCust1,
                Cust2 = menu.UseCust2,
                Cust3 = menu.UseCust3,
                Cust4 = menu.UseCust4,
                Cust5 = menu.UseCust5,
                Cust6 = menu.UseCust6,
                Cust7 = menu.UseCust7,
                Cust8 = menu.UseCust8,
                Cust1Name = menu.Cust1Name,
                Cust2Name = menu.Cust2Name,
                Cust3Name = menu.Cust3Name,
                Cust4Name = menu.Cust4Name,
                Cust5Name = menu.Cust5Name,
                Cust6Name = menu.Cust6Name,
                Cust7Name = menu.Cust7Name,
                Cust8Name = menu.Cust8Name,
            },
            Roles = roles.Select(r =>
            {
                grantByRole.TryGetValue(r.Id, out var g);
                return new MenuRoleGrantDto
                {
                    RoleId = r.Id,
                    RoleName = r.Name,
                    Granted = g is not null,
                    CanView = g?.CanView ?? false,
                    CanSearch = g?.CanSearch ?? false,
                    CanCreate = g?.CanCreate ?? false,
                    CanUpdate = g?.CanUpdate ?? false,
                    CanDelete = g?.CanDelete ?? false,
                    CanPrint = g?.CanPrint ?? false,
                    CanExcel = g?.CanExcel ?? false,
                    CanCust1 = g?.CanCust1 ?? false,
                    CanCust2 = g?.CanCust2 ?? false,
                    CanCust3 = g?.CanCust3 ?? false,
                    CanCust4 = g?.CanCust4 ?? false,
                    CanCust5 = g?.CanCust5 ?? false,
                    CanCust6 = g?.CanCust6 ?? false,
                    CanCust7 = g?.CanCust7 ?? false,
                    CanCust8 = g?.CanCust8 ?? false,
                    CompanyCount = roleCompanies.Count(rc => rc.RoleId == r.Id),
                    DepartmentCount = roleDepartments.Count(rd => rd.RoleId == r.Id),
                    AccountCount = roleAccounts.Count(ra => ra.RoleId == r.Id),
                };
            }).ToList(),
            Companies = companyTargets,
            Departments = deptTargets,
            Accounts = accountTargets,
            EffectiveUserCount = effective,
        };
    }

    /// <summary>같은 대상에 여러 역할이 걸릴 수 있다. 역할 이름을 모아 한 줄로 만든다.</summary>
    private static Dictionary<string, (List<string> Ids, List<string> Names)> GroupByTarget(
        IEnumerable<(string TargetId, string RoleId)> pairs,
        IReadOnlyDictionary<string, string> roleNameById)
    {
        var map = new Dictionary<string, (List<string>, List<string>)>();
        foreach (var (targetId, roleId) in pairs)
        {
            if (!map.TryGetValue(targetId, out var entry))
            {
                entry = (new List<string>(), new List<string>());
                map[targetId] = entry;
            }
            if (!entry.Item1.Contains(roleId))
            {
                entry.Item1.Add(roleId);
                entry.Item2.Add(roleNameById.GetValueOrDefault(roleId, roleId));
            }
        }
        return map.ToDictionary(kv => kv.Key, kv => (kv.Value.Item1, kv.Value.Item2));
    }

    private async Task<List<MenuRoleTargetDto>> BuildCompanyTargetsAsync(
        IEnumerable<(string, string)> pairs,
        IReadOnlyDictionary<string, string> roleNameById)
    {
        var grouped = GroupByTarget(pairs, roleNameById);
        if (grouped.Count == 0) return new List<MenuRoleTargetDto>();

        var ids = grouped.Keys.ToList();
        var companies = await _db.Companies
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        var userCounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.CompanyId != null && ids.Contains(a.CompanyId))
            .GroupBy(a => a.CompanyId!)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return companies
            .Select(c => new MenuRoleTargetDto
            {
                Id = c.Id,
                Name = c.Name,
                ViaRoleIds = grouped[c.Id].Ids,
                ViaRoleNames = grouped[c.Id].Names,
                UserCount = userCounts.GetValueOrDefault(c.Id),
            })
            .OrderBy(t => t.Name)
            .ToList();
    }

    private async Task<List<MenuRoleTargetDto>> BuildDepartmentTargetsAsync(
        IEnumerable<(string, string)> pairs,
        IReadOnlyDictionary<string, string> roleNameById)
    {
        var grouped = GroupByTarget(pairs, roleNameById);
        if (grouped.Count == 0) return new List<MenuRoleTargetDto>();

        var ids = grouped.Keys.ToList();
        var depts = await _db.Departments
            .AsNoTracking()
            .Include(d => d.Company)
            .Where(d => ids.Contains(d.Id) && !d.IsDeleted)
            .Select(d => new { d.Id, d.Name, CompanyName = d.Company != null ? d.Company.Name : null })
            .ToListAsync();

        var userCounts = await _db.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted && a.DepartmentId != null && ids.Contains(a.DepartmentId))
            .GroupBy(a => a.DepartmentId!)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return depts
            .Select(d => new MenuRoleTargetDto
            {
                Id = d.Id,
                Name = d.Name,
                CompanyName = d.CompanyName,
                ViaRoleIds = grouped[d.Id].Ids,
                ViaRoleNames = grouped[d.Id].Names,
                UserCount = userCounts.GetValueOrDefault(d.Id),
            })
            .OrderBy(t => t.CompanyName).ThenBy(t => t.Name)
            .ToList();
    }

    private async Task<List<MenuRoleTargetDto>> BuildAccountTargetsAsync(
        IEnumerable<(string, string)> pairs,
        IReadOnlyDictionary<string, string> roleNameById)
    {
        var grouped = GroupByTarget(pairs, roleNameById);
        if (grouped.Count == 0) return new List<MenuRoleTargetDto>();

        var ids = grouped.Keys.ToList();
        var accounts = await _db.Accounts
            .AsNoTracking()
            .Include(a => a.Company)
            .Where(a => ids.Contains(a.Id) && !a.IsDeleted)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                a.UserName,
                CompanyName = a.Company != null ? a.Company.Name : null,
            })
            .ToListAsync();

        return accounts
            .Select(a => new MenuRoleTargetDto
            {
                Id = a.Id,
                Name = a.UserName ?? a.UserId,
                LoginId = a.UserId,
                CompanyName = a.CompanyName,
                ViaRoleIds = grouped[a.Id].Ids,
                ViaRoleNames = grouped[a.Id].Names,
                UserCount = 1,
            })
            .OrderBy(t => t.CompanyName).ThenBy(t => t.Name)
            .ToList();
    }

    /// <summary>
    /// 세 갈래를 합쳐 사람 단위로 중복을 없앤 수.
    /// </summary>
    private async Task<int> CountEffectiveUsersAsync(
        List<string> companyIds, List<string> deptIds, List<string> accountIds)
    {
        if (companyIds.Count == 0 && deptIds.Count == 0 && accountIds.Count == 0) return 0;

        return await _db.Accounts
            .AsNoTracking()
            .Where(a => !a.IsDeleted && (
                (a.CompanyId != null && companyIds.Contains(a.CompanyId)) ||
                (a.DepartmentId != null && deptIds.Contains(a.DepartmentId)) ||
                accountIds.Contains(a.Id)))
            .Select(a => a.Id)
            .Distinct()
            .CountAsync();
    }
}
