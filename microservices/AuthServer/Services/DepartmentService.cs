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
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Pid = d.ParentId,
                Status = d.Status,
                Remark = d.Remark,
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
}
