using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>
/// 역할(권한) 및 사용자, 메뉴 세부 권한 지정을 위한 비즈니스 서비스 구현체
/// </summary>
public class RolePermissionService : IRolePermissionService
{
    private readonly AppDbContext _db;

    public RolePermissionService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>특정 역할에 매핑된 사용자 계정 목록 조회</summary>
    public async Task<List<RoleUserDto>> GetUsersByRoleAsync(string roleId)
    {
        var matchedAccountIds = await _db.RoleAccounts
            .Where(ra => ra.RoleId == roleId)
            .Select(ra => ra.AccountId)
            .ToListAsync();

        var accounts = await _db.Accounts
            .Where(a => matchedAccountIds.Contains(a.Id))
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .ToListAsync();

        return MapAccountsToDto(accounts);
    }

    /// <summary>특정 역할에 지정 가능한(아직 해당 역할이 없는) 전체 계정 목록 조회</summary>
    public async Task<List<RoleUserDto>> GetEligibleUsersForRoleAsync(string roleId)
    {
        var matchedAccountIds = await _db.RoleAccounts
            .Where(ra => ra.RoleId == roleId)
            .Select(ra => ra.AccountId)
            .ToListAsync();

        var accounts = await _db.Accounts
            .Where(a => !matchedAccountIds.Contains(a.Id))
            .Include(a => a.Company)
            .Include(a => a.Department)
            .Include(a => a.ProfileDetails)
            .ToListAsync();

        return MapAccountsToDto(accounts);
    }

    /// <summary>특정 역할에 사용자 계정들을 매핑</summary>
    public async Task AssignUsersToRoleAsync(string roleId, List<string> accountIds)
    {
        if (accountIds == null || !accountIds.Any()) return;

        // 역할이 실제 존재하는지 검증
        var roleExists = await _db.Roles.AnyAsync(r => r.Id == roleId);
        if (!roleExists)
        {
            throw new ArgumentException("존재하지 않는 역할 아이디입니다.");
        }

        // 이미 할당된 사용자와 중복 추가를 방지하기 위해 필터링
        var existingAccountIds = await _db.RoleAccounts
            .Where(ra => ra.RoleId == roleId && accountIds.Contains(ra.AccountId))
            .Select(ra => ra.AccountId)
            .ToListAsync();

        var targetAccountIds = accountIds.Except(existingAccountIds).ToList();

        foreach (var accountId in targetAccountIds)
        {
            var mapping = new RoleAccount
            {
                RoleId = roleId,
                AccountId = accountId
            };
            _db.RoleAccounts.Add(mapping);
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>특정 역할에서 사용자 계정 매핑을 해제</summary>
    public async Task RemoveUserFromRoleAsync(string roleId, string accountId)
    {
        var mapping = await _db.RoleAccounts
            .FirstOrDefaultAsync(ra => ra.RoleId == roleId && ra.AccountId == accountId);

        if (mapping != null)
        {
            _db.RoleAccounts.Remove(mapping);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>특정 역할의 전체 메뉴에 대한 세부 권한 지정 정보 목록 조회</summary>
    public async Task<List<RoleMenuDto>> GetMenusByRoleAsync(string roleId)
    {
        // 1. 전체 메뉴 목록 조회
        var allMenus = await _db.SystemMenus
            .OrderBy(m => m.OrderNo)
            .ToListAsync();

        // 2. 해당 역할의 이미 지정된 메뉴 세부 권한 정보 맵핑 조회
        var roleMenus = await _db.RoleMenus
            .Where(rm => rm.RoleId == roleId)
            .ToDictionaryAsync(rm => rm.MenuId);

        var result = new List<RoleMenuDto>();

        foreach (var menu in allMenus)
        {
            roleMenus.TryGetValue(menu.Id, out var pm);
            result.Add(new RoleMenuDto
            {
                MenuId = menu.Id,
                MenuName = menu.Name,
                ParentId = menu.Pid,
                CanView = pm?.CanView ?? false,
                CanSearch = pm?.CanSearch ?? false,
                CanCreate = pm?.CanCreate ?? false,
                CanDelete = pm?.CanDelete ?? false,
                CanUpdate = pm?.CanUpdate ?? false,
                CanPrint = pm?.CanPrint ?? false,
                CanExcel = pm?.CanExcel ?? false,
                CanCust1 = pm?.CanCust1 ?? false,
                CanCust2 = pm?.CanCust2 ?? false,
                CanCust3 = pm?.CanCust3 ?? false,
                CanCust4 = pm?.CanCust4 ?? false,
                CanCust5 = pm?.CanCust5 ?? false,
                CanCust6 = pm?.CanCust6 ?? false,
                CanCust7 = pm?.CanCust7 ?? false,
                CanCust8 = pm?.CanCust8 ?? false
            });
        }

        return result;
    }

    /// <summary>특정 역할의 메뉴 세부 권한 설정 일괄 저장</summary>
    public async Task SaveRoleMenusAsync(string roleId, List<SaveRoleMenuDto> dtos)
    {
        if (dtos == null) return;

        // 역할 존재 유무 검사
        var roleExists = await _db.Roles.AnyAsync(r => r.Id == roleId);
        if (!roleExists)
        {
            throw new ArgumentException("존재하지 않는 역할 아이디입니다.");
        }

        // 기존 설정 조회
        var existingMappings = await _db.RoleMenus
            .Where(rm => rm.RoleId == roleId)
            .ToDictionaryAsync(rm => rm.MenuId);

        foreach (var dto in dtos)
        {
            if (existingMappings.TryGetValue(dto.MenuId, out var mapping))
            {
                // 기존 데이터가 존재하면 필드 수정
                mapping.CanView = dto.CanView;
                mapping.CanSearch = dto.CanSearch;
                mapping.CanCreate = dto.CanCreate;
                mapping.CanDelete = dto.CanDelete;
                mapping.CanUpdate = dto.CanUpdate;
                mapping.CanPrint = dto.CanPrint;
                mapping.CanExcel = dto.CanExcel;
                mapping.CanCust1 = dto.CanCust1;
                mapping.CanCust2 = dto.CanCust2;
                mapping.CanCust3 = dto.CanCust3;
                mapping.CanCust4 = dto.CanCust4;
                mapping.CanCust5 = dto.CanCust5;
                mapping.CanCust6 = dto.CanCust6;
                mapping.CanCust7 = dto.CanCust7;
                mapping.CanCust8 = dto.CanCust8;

                _db.Entry(mapping).State = EntityState.Modified;
            }
            else
            {
                // 데이터가 존재하지 않으면 새로 인서트
                var newMapping = new RoleMenu
                {
                    RoleId = roleId,
                    MenuId = dto.MenuId,
                    CanView = dto.CanView,
                    CanSearch = dto.CanSearch,
                    CanCreate = dto.CanCreate,
                    CanDelete = dto.CanDelete,
                    CanUpdate = dto.CanUpdate,
                    CanPrint = dto.CanPrint,
                    CanExcel = dto.CanExcel,
                    CanCust1 = dto.CanCust1,
                    CanCust2 = dto.CanCust2,
                    CanCust3 = dto.CanCust3,
                    CanCust4 = dto.CanCust4,
                    CanCust5 = dto.CanCust5,
                    CanCust6 = dto.CanCust6,
                    CanCust7 = dto.CanCust7,
                    CanCust8 = dto.CanCust8
                };
                _db.RoleMenus.Add(newMapping);
            }
        }

        await _db.SaveChangesAsync();
    }

    private List<RoleUserDto> MapAccountsToDto(List<Account> accounts)
    {
        return accounts.Select(a =>
        {
            var emailDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Email");
            var phoneDetail = a.ProfileDetails?.FirstOrDefault(p => p.DetailType == "Phone");

            return new RoleUserDto
            {
                Id = a.Id,
                LoginId = a.UserId,
                UserName = a.UserName ?? string.Empty,
                Email = emailDetail?.Content,
                Phone = phoneDetail?.Content,
                CompanyName = a.Company?.Name,
                DeptName = a.Department?.Name
            };
        }).ToList();
    }
}
