using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 시스템 역할(Role) 관리 서비스 구현체
/// </summary>
public class RoleService : IRoleService
{
    private readonly AppDbContext _db;

    public RoleService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>역할 목록 조회</summary>
    public async Task<List<RoleDto>> GetRoleListAsync()
    {
        var roles = await _db.Roles.ToListAsync();
        return roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Status = r.Status,
            Remark = r.Remark,
            Permissions = new List<string> { "all" } 
        }).ToList();
    }

    /// <summary>역할 생성</summary>
    public async Task<RoleDto> CreateRoleAsync(CreateRoleDto request)
    {
        var role = new Role
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Status = request.Status,
            Remark = request.Remark
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        
        return new RoleDto { Id = role.Id, Name = role.Name, Status = role.Status, Remark = role.Remark };
    }

    /// <summary>역할 정보 수정</summary>
    public async Task<bool> UpdateRoleAsync(string id, CreateRoleDto request)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return false;

        role.Name = request.Name;
        role.Status = request.Status;
        role.Remark = request.Remark;
        
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>역할 삭제</summary>
    public async Task<bool> DeleteRoleAsync(string id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role == null) return false;

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return true;
    }
}
