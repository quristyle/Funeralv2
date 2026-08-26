using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 조직 부서(Department) 관리 서비스 구현체
/// </summary>
public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;

    public DepartmentService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>부서 목록 트리 구조 조회</summary>
    /// <param name="companyId">조회할 회사. 비우면 요청한 사람의 회사를 쓴다.</param>
    /// <param name="userContext">게이트웨이가 넘긴 신원</param>
    /// <param name="allCompanies">
    /// 모든 회사의 부서를 함께 볼지.
    /// <para>
    /// **회사 인자를 비우는 것으로는 '전체' 를 표현할 수 없다.** 비우면 요청한 사람의
    /// 회사로 좁혀지기 때문이다(아래 fallback). 부서 관리 화면의 '전체' 가 실제로는
    /// 자기 회사만 보여 주고 있었던 것이 그래서였다.
    /// 전체를 보려면 이 값을 켜야 한다 — 뜻이 분명해지고, 기존 호출은 그대로 동작한다.
    /// </para>
    /// </param>
    public async Task<List<DepartmentDto>> GetDeptListAsync(
        string? companyId, UserContext? userContext, bool allCompanies = false)
    {
        var query = _db.Departments
            .Include(d => d.Company)
            .AsQueryable();

        if (!allCompanies)
        {
            var targetCompanyId = companyId ?? userContext?.CompanyId;
            if (!string.IsNullOrEmpty(targetCompanyId))
            {
                query = query.Where(d => d.CompanyId == targetCompanyId);
            }
        }

        var departments = await query.ToListAsync();

        // ── 부서별 소속 인원 ──────────────────────────────────
        //
        // 부서 목록·트리에서 "어디에 사람이 있는지" 를 바로 보여 주려고 함께 센다.
        // 부서마다 따로 세면 부서 수만큼 질의가 나가므로(N+1) 한 번에 묶는다.
        // 사용자는 부서 하나에만 붙으므로(accounts.department_id) 중복 계산이 없다.
        var deptIds = departments.Select(d => d.Id).ToList();
        var userCounts = await _db.Accounts
            .Where(a => !a.IsDeleted && a.DepartmentId != null && deptIds.Contains(a.DepartmentId))
            .GroupBy(a => a.DepartmentId!)
            .Select(g => new { DeptId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeptId, x => x.Count);

        return BuildDeptTree(departments, null, userCounts);
    }

    /// <summary>부서 데이터를 트리로 빌드하는 내부 메서드</summary>
    private List<DepartmentDto> BuildDeptTree(
        List<Department> depts,
        string? parentId,
        IReadOnlyDictionary<string, int> userCounts)
    {
        return depts
            .Where(d => d.ParentId == parentId)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .Select(d =>
            {
                // 자식 트리는 한 번만 만든다. 예전에는 같은 호출을 두 번 해서
                // (Any() 확인 + 실제 값) 깊은 트리에서 지수적으로 늘어났다.
                var children = BuildDeptTree(depts, d.Id, userCounts);
                var own = userCounts.GetValueOrDefault(d.Id);

                return new DepartmentDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Pid = d.ParentId,
                    Status = d.Status,
                    Remark = d.Remark,
                    SortOrder = d.SortOrder,
                    CompanyId = d.CompanyId,
                    CompanyName = d.Company?.Name,
                    UserCount = own,
                    // 접어 둔 상태에서도 조직 전체 인원을 알 수 있게 합계를 함께 준다.
                    TotalUserCount = own + children.Sum(c => c.TotalUserCount),
                    Children = children.Count > 0 ? children : null
                };
            })
            .ToList();
    }

    /// <summary>부서 생성</summary>
    public async Task<DepartmentDto> CreateDeptAsync(CreateDepartmentDto request, UserContext? userContext)
    {
        var dept = new Department
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            ParentId = request.Pid,
            Status = request.Status,
            Remark = request.Remark,
            SortOrder = request.SortOrder,
            CompanyId = request.CompanyId ?? userContext?.CompanyId
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();
        
        return new DepartmentDto 
        { 
            Id = dept.Id, 
            Name = dept.Name, 
            Pid = dept.ParentId, 
            Status = dept.Status, 
            Remark = dept.Remark,
            SortOrder = dept.SortOrder,
            CompanyId = dept.CompanyId
        };
    }

    /// <summary>부서 수정</summary>
    public async Task<bool> UpdateDeptAsync(string id, CreateDepartmentDto request, UserContext? userContext)
    {
        var companyId = userContext?.CompanyId;
        var dept = await _db.Departments
            .FirstOrDefaultAsync(d => d.Id == id && (string.IsNullOrEmpty(companyId) || d.CompanyId == companyId));
        
        if (dept == null) return false;

        dept.Name = request.Name;
        dept.ParentId = request.Pid;
        dept.Status = request.Status;
        dept.Remark = request.Remark;
        dept.SortOrder = request.SortOrder;
        if (!string.IsNullOrEmpty(request.CompanyId))
        {
            dept.CompanyId = request.CompanyId;
        }
        
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>부서 삭제</summary>
    public async Task<bool> DeleteDeptAsync(string id, UserContext? userContext)
    {
        var companyId = userContext?.CompanyId;
        var dept = await _db.Departments
            .FirstOrDefaultAsync(d => d.Id == id && (string.IsNullOrEmpty(companyId) || d.CompanyId == companyId));
            
        if (dept == null) return false;

        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>특정 부서 소속 사용자 목록 조회</summary>
    public async Task<IEnumerable<AccountDto>> GetDeptUsersAsync(string departmentId)
    {
        var accounts = await _db.Accounts
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .Where(a => a.DepartmentId == departmentId)
            .ToListAsync();

        var roleAccounts = await _db.RoleAccounts
            .Include(ra => ra.Role)
            .ToListAsync();

        var roleMap = roleAccounts
            .Where(ra => ra.Role != null)
            .GroupBy(ra => ra.AccountId)
            .ToDictionary(
                g => g.Key,
                g => new {
                    RoleIds = g.Select(ra => ra.RoleId).ToList(),
                    RoleNames = g.Select(ra => ra.Role!.Name).ToList()
                }
            );

        return accounts.Select(a => {
            var emailDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
            var phoneDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");
            var statusDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Status");
            var avatarDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Avatar");

            roleMap.TryGetValue(a.Id, out var rolesInfo);

            return new AccountDto
            {
                Id = a.Id,
                LoginId = a.UserId,
                UserName = a.UserName ?? string.Empty,
                Email = emailDetail?.Content,
                Phone = phoneDetail?.Content,
                Status = statusDetail?.Content ?? "ACTIVE",
                CompanyId = a.CompanyId,
                CompanyName = a.Company?.Name,
                DeptId = a.DepartmentId,
                DeptName = a.Department?.Name,
                CreatedAt = a.CreatedAt,
                RoleIds = rolesInfo?.RoleIds ?? new List<string>(),
                RoleNames = rolesInfo?.RoleNames ?? new List<string>(),
                Avatar = avatarDetail?.Content,
                AvatarGroupId = a.AvatarGroupId
            };
        }).ToList();
    }

    /// <summary>부서 소속이 없는 사용자 조회 (부서 배정용)</summary>
    public async Task<IEnumerable<AccountDto>> GetEligibleUsersAsync(string? companyId)
    {
        var query = _db.Accounts
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .Where(a => a.DepartmentId == null);

        // 만약 특정 회사가 지정되어 있다면 해당 회사 소속이되 부서가 없는 사용자도 포함
        if (!string.IsNullOrEmpty(companyId))
        {
            query = query.Where(a => a.CompanyId == null || a.CompanyId == companyId);
        }
        else
        {
            query = query.Where(a => a.CompanyId == null);
        }

        var accounts = await query.ToListAsync();

        var roleAccounts = await _db.RoleAccounts
            .Include(ra => ra.Role)
            .ToListAsync();

        var roleMap = roleAccounts
            .Where(ra => ra.Role != null)
            .GroupBy(ra => ra.AccountId)
            .ToDictionary(
                g => g.Key,
                g => new {
                    RoleIds = g.Select(ra => ra.RoleId).ToList(),
                    RoleNames = g.Select(ra => ra.Role!.Name).ToList()
                }
            );

        return accounts.Select(a => {
            var emailDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
            var phoneDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");
            var statusDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Status");
            var avatarDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Avatar");

            roleMap.TryGetValue(a.Id, out var rolesInfo);

            return new AccountDto
            {
                Id = a.Id,
                LoginId = a.UserId,
                UserName = a.UserName ?? string.Empty,
                Email = emailDetail?.Content,
                Phone = phoneDetail?.Content,
                Status = statusDetail?.Content ?? "ACTIVE",
                CompanyId = a.CompanyId,
                CompanyName = a.Company?.Name,
                DeptId = a.DepartmentId,
                DeptName = a.Department?.Name,
                CreatedAt = a.CreatedAt,
                RoleIds = rolesInfo?.RoleIds ?? new List<string>(),
                RoleNames = rolesInfo?.RoleNames ?? new List<string>(),
                Avatar = avatarDetail?.Content,
                AvatarGroupId = a.AvatarGroupId
            };
        }).ToList();
    }

    /// <summary>특정 부서에 사용자 추가 등록 (일괄)</summary>
    public async Task<bool> AssignUsersToDeptAsync(string departmentId, List<string> userIds)
    {
        if (userIds == null || !userIds.Any()) return false;

        var dept = await _db.Departments.FindAsync(departmentId);
        if (dept == null) return false;

        var accounts = await _db.Accounts
            .Where(a => userIds.Contains(a.Id))
            .ToListAsync();

        foreach (var account in accounts)
        {
            account.DepartmentId = departmentId;
            account.CompanyId = dept.CompanyId; // 소속 부서의 상위 회사와 매핑 싱크
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>부서에서 사용자 소속 해제 (일괄)</summary>
    public async Task<bool> RemoveUsersFromDeptAsync(List<string> userIds)
    {
        if (userIds == null || !userIds.Any()) return false;

        var accounts = await _db.Accounts
            .Where(a => userIds.Contains(a.Id))
            .ToListAsync();

        foreach (var account in accounts)
        {
            account.DepartmentId = null;
            account.CompanyId = null; // 소속 회사도 함께 해제하여 무소속으로 지정
        }

        // 이 한 줄이 빠져 있었다. 응답은 "해제되었습니다" 였지만 실제로는 아무것도 저장되지 않았다
        // (같은 서비스의 다른 메서드는 모두 부르고 있다). 화면의 '해제' 버튼도 그동안 동작하지 않았다.
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>부서 위치 이동 (하위 부서 이동 포함)</summary>
    public async Task<bool> MoveDeptAsync(string id, string? parentId, UserContext? userContext)
    {
        var companyId = userContext?.CompanyId;
        var dept = await _db.Departments
            .FirstOrDefaultAsync(d => d.Id == id && (string.IsNullOrEmpty(companyId) || d.CompanyId == companyId));

        if (dept == null) return false;

        // 순환 참조 방지 및 자기 자신으로 이동 방지
        if (id == parentId || await IsCircularReference(id, parentId))
        {
            return false;
        }

        dept.ParentId = string.IsNullOrEmpty(parentId) ? null : parentId;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>사용자 부서 이동</summary>
    public async Task<bool> MoveUserDeptAsync(string accountId, string? departmentId, UserContext? userContext)
    {
        var account = await _db.Accounts.FindAsync(accountId);
        if (account == null) return false;

        if (string.IsNullOrEmpty(departmentId))
        {
            account.DepartmentId = null;
            account.CompanyId = null; // 무소속 지정
        }
        else
        {
            var dept = await _db.Departments.FindAsync(departmentId);
            if (dept == null) return false;

            account.DepartmentId = departmentId;
            account.CompanyId = dept.CompanyId; // 회사 ID 동기화
        }

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>부서 이동 시 순환 참조 여부 검사</summary>
    private async Task<bool> IsCircularReference(string deptId, string? parentId)
    {
        if (string.IsNullOrEmpty(parentId)) return false;
        if (deptId == parentId) return true;

        var current = await _db.Departments.FindAsync(parentId);
        while (current != null)
        {
            if (current.ParentId == deptId) return true;
            if (string.IsNullOrEmpty(current.ParentId)) break;
            current = await _db.Departments.FindAsync(current.ParentId);
        }
        return false;
    }
}
