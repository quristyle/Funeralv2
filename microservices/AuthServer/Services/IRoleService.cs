using AuthServer.DTOs;

namespace AuthServer.Services;

public interface IRoleService
{
    Task<List<RoleDto>> GetRoleListAsync();
    Task<RoleDto> CreateRoleAsync(CreateRoleDto request);
    Task<bool> UpdateRoleAsync(string id, CreateRoleDto request);
    Task<bool> DeleteRoleAsync(string id);
}
