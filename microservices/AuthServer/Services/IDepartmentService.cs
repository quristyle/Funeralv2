using AuthServer.DTOs;

namespace AuthServer.Services;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetDeptListAsync(string? companyId, UserContext? userContext);
    Task<DepartmentDto> CreateDeptAsync(CreateDepartmentDto request, UserContext? userContext);
    Task<bool> UpdateDeptAsync(string id, CreateDepartmentDto request, UserContext? userContext);
    Task<bool> DeleteDeptAsync(string id, UserContext? userContext);
    
    Task<IEnumerable<AccountDto>> GetDeptUsersAsync(string departmentId);
    Task<IEnumerable<AccountDto>> GetEligibleUsersAsync(string? companyId);
    Task<bool> AssignUsersToDeptAsync(string departmentId, List<string> userIds);
    Task<bool> RemoveUsersFromDeptAsync(List<string> userIds);
    Task<bool> MoveDeptAsync(string id, string? parentId, UserContext? userContext);
    Task<bool> MoveUserDeptAsync(string accountId, string? departmentId, UserContext? userContext);
}
