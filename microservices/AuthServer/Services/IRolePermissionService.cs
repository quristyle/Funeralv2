using System.Collections.Generic;
using System.Threading.Tasks;
using AuthServer.DTOs;

namespace AuthServer.Services;

/// <summary>
/// 역할(권한) 및 사용자, 메뉴 세부 권한 지정을 위한 비즈니스 서비스 인터페이스
/// </summary>
public interface IRolePermissionService
{
    /// <summary>특정 역할에 매핑된 사용자 계정 목록 조회</summary>
    Task<List<RoleUserDto>> GetUsersByRoleAsync(string roleId);

    /// <summary>특정 역할에 지정 가능한(아직 해당 역할이 없는) 전체 계정 목록 조회</summary>
    Task<List<RoleUserDto>> GetEligibleUsersForRoleAsync(string roleId);

    /// <summary>특정 역할에 사용자 계정들을 매핑</summary>
    Task AssignUsersToRoleAsync(string roleId, List<string> accountIds);

    /// <summary>특정 역할에서 사용자 계정 매핑을 해제</summary>
    Task RemoveUserFromRoleAsync(string roleId, string accountId);

    /// <summary>특정 역할의 전체 메뉴에 대한 세부 권한 지정 정보 목록 조회</summary>
    Task<List<RoleMenuDto>> GetMenusByRoleAsync(string roleId);

    /// <summary>특정 역할의 메뉴 세부 권한 설정 일괄 저장</summary>
    Task SaveRoleMenusAsync(string roleId, List<SaveRoleMenuDto> dtos);
}
