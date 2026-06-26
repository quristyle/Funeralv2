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
    public async Task<List<DepartmentDto>> GetDeptListAsync(string? companyId, UserContext? userContext)
    {
        var targetCompanyId = companyId ?? userContext?.CompanyId;
        var query = _db.Departments
            .Include(d => d.Company)
            .AsQueryable();

        if (!string.IsNullOrEmpty(targetCompanyId))
        {
            query = query.Where(d => d.CompanyId == targetCompanyId);
        }

        var departments = await query.ToListAsync();
        return BuildDeptTree(departments, null);
    }

    /// <summary>부서 데이터를 트리로 빌드하는 내부 메서드</summary>
    private List<DepartmentDto> BuildDeptTree(List<Department> depts, string? parentId)
    {
        return depts
            .Where(d => d.ParentId == parentId)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Pid = d.ParentId,
                Status = d.Status,
                Remark = d.Remark,
                SortOrder = d.SortOrder,
                CompanyId = d.CompanyId,
                CompanyName = d.Company?.Name,
                Children = BuildDeptTree(depts, d.Id).Any() ? BuildDeptTree(depts, d.Id) : null
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
                RoleNames = rolesInfo?.RoleNames ?? new List<string>()
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
                RoleNames = rolesInfo?.RoleNames ?? new List<string>()
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
